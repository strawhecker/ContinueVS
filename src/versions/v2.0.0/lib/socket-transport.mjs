import net from 'net';
import EventEmitter from 'events';

/**
 * SocketTransportError - Base error class for socket transport operations
 */
export class SocketTransportError extends Error {
  constructor(message, operationType = 'Unknown') {
    super(message);
    this.name = 'SocketTransportError';
    this.operationType = operationType;
  }
}

/**
 * SocketHandshakeError - Handshake protocol failures
 */
export class SocketHandshakeError extends SocketTransportError {
  constructor(message) {
    super(message, 'Handshake');
    this.name = 'SocketHandshakeError';
  }
}

/**
 * SocketMessageError - Message framing/parsing errors
 */
export class SocketMessageError extends SocketTransportError {
  constructor(message) {
    super(message, 'Message');
    this.name = 'SocketMessageError';
  }
}

/**
 * SocketConnectionError - Connection/lifecycle errors
 */
export class SocketConnectionError extends SocketTransportError {
  constructor(message) {
    super(message, 'Connection');
    this.name = 'SocketConnectionError';
  }
}

/**
 * SocketTransportServer - TCP-based bridge transport server
 * 
 * Manages incoming socket connections from IDE clients, handles handshake protocol,
 * frames messages with delimiter, and routes to message dispatcher.
 * 
 * Message framing: JSON-RPC messages delimited by \n\n (double newline)
 * Handshake: Client sends {action: "bridge:connect", version: "2.0.0"}
 *            Server responds {status: "ready", port, version}
 */
export class SocketTransportServer extends EventEmitter {
  constructor(port = 9999, logger = null, metrics = null) {
    super();

    this.port = port;
    this.logger = logger;
    this.metrics = metrics;
    this.server = null;
    this.clients = new Map();
    this.messageHandlers = [];
    this.isRunning = false;
    this.handshakeTimeout = 5000; // 5 seconds
    this.messageDelimiter = '\n\n';
  }

  /**
   * Start listening for incoming connections
   */
  async start() {
    if (this.isRunning) {
      throw new SocketConnectionError('Server already running');
    }

    return new Promise((resolve, reject) => {
      this.server = net.createServer();

      this.server.on('connection', (socket) => {
        this._handleNewConnection(socket).catch(err => {
          this._logError('Connection handler error', err);
        });
      });

      this.server.on('error', (err) => {
        this._logError('Server error', err);
        reject(new SocketConnectionError(`Server error: ${err.message}`));
      });

      this.server.listen(this.port, '127.0.0.1', () => {
        this.isRunning = true;
        this._log('info', `Socket transport server listening on port ${this.port}`);
        this.emit('started', { port: this.port });
        resolve();
      });
    });
  }

  /**
   * Stop listening and close all connections
   */
  async stop() {
    if (!this.isRunning) {
      return;
    }

    // Close all client connections gracefully
    for (const [clientId, clientData] of this.clients.entries()) {
      try {
        clientData.socket.end();
      } catch (err) {
        this._logError(`Error closing client ${clientId}`, err);
      }
    }

    this.clients.clear();

    return new Promise((resolve, reject) => {
      if (!this.server) {
        this.isRunning = false;
        resolve();
        return;
      }

      this.server.close((err) => {
        this.isRunning = false;
        this.server = null;

        if (err) {
          this._logError('Server close error', err);
          reject(new SocketConnectionError(`Server close error: ${err.message}`));
        } else {
          this._log('info', 'Socket transport server stopped');
          this.emit('stopped');
          resolve();
        }
      });
    });
  }

  /**
   * Send JSON-RPC response to a client
   */
  async send(clientId, messageId, data) {
    const clientData = this.clients.get(clientId);
    if (!clientData) {
      throw new SocketConnectionError(`Client ${clientId} not found`);
    }

    const message = {
      id: messageId,
      result: data,
      jsonrpc: '2.0'
    };

    const formatted = this._formatMessage(message);
    return new Promise((resolve, reject) => {
      clientData.socket.write(formatted, (err) => {
        if (err) {
          this._logError(`Send error to client ${clientId}`, err);
          reject(new SocketConnectionError(`Send error: ${err.message}`));
        } else {
          if (this.metrics) {
            this.metrics.recordMessageSent();
          }
          resolve();
        }
      });
    });
  }

  /**
   * Register a message handler callback
   */
  on(eventName, callback) {
    if (eventName === 'message') {
      this.messageHandlers.push(callback);
    } else {
      super.on(eventName, callback);
    }
  }

  /**
   * Register message handlers with a dispatcher
   */
  registerMessageHandlers(dispatcher) {
    if (!dispatcher || typeof dispatcher.handle !== 'function') {
      throw new Error('Dispatcher must have a handle() method');
    }

    this.messageHandlers.push(async (clientId, message) => {
      try {
        const result = await dispatcher.handle(message);
        await this.send(clientId, message.id, result);
      } catch (err) {
        this._logError('Dispatcher error', err);
        // Send error response
        const errorResponse = {
          id: message.id,
          error: {
            code: -32603,
            message: 'Internal error',
            data: err.message
          },
          jsonrpc: '2.0'
        };
        await this.send(clientId, message.id, errorResponse).catch(() => {});
      }
    });
  }

  /**
   * Handle new incoming connection
   */
  async _handleNewConnection(socket) {
    const clientId = this._generateClientId();

    this._log('info', `New connection from ${socket.remoteAddress}:${socket.remotePort} (clientId: ${clientId})`);

    const clientData = {
      socket,
      clientId,
      buffer: '',
      isHandshaken: false,
      handshakeTimeout: null
    };

    this.clients.set(clientId, clientData);

    // Set up handshake timeout
    clientData.handshakeTimeout = setTimeout(() => {
      this._log('warn', `Handshake timeout for client ${clientId}`);
      socket.destroy();
    }, this.handshakeTimeout);

    socket.on('data', (chunk) => {
      this._handleSocketData(clientData, chunk).catch(err => {
        this._logError(`Data handler error for ${clientId}`, err);
        socket.destroy();
      });
    });

    socket.on('error', (err) => {
      this._logError(`Socket error for ${clientId}`, err);
      this._cleanupClient(clientData);
    });

    socket.on('end', () => {
      this._log('info', `Connection closed: ${clientId}`);
      this._cleanupClient(clientData);
      this.emit('clientDisconnected', { clientId });
    });

    socket.on('close', () => {
      this._cleanupClient(clientData);
    });
  }

  /**
   * Handle incoming data from socket
   */
  async _handleSocketData(clientData, chunk) {
    clientData.buffer += chunk.toString('utf8');

    if (!clientData.isHandshaken) {
      await this._processHandshake(clientData);
      return;
    }

    // Process messages delimited by \n\n
    await this._processMessages(clientData);
  }

  /**
   * Process handshake protocol
   */
  async _processHandshake(clientData) {
    const delimiterIndex = clientData.buffer.indexOf(this.messageDelimiter);
    if (delimiterIndex === -1) {
      // Handshake message not complete yet
      return;
    }

    const handshakeMsg = clientData.buffer.substring(0, delimiterIndex);
    clientData.buffer = clientData.buffer.substring(delimiterIndex + this.messageDelimiter.length);

    try {
      const handshake = JSON.parse(handshakeMsg);

      if (handshake.action !== 'bridge:connect') {
        throw new SocketHandshakeError('Invalid handshake action');
      }

      if (handshake.version !== '2.0.0') {
        throw new SocketHandshakeError(`Unsupported version: ${handshake.version}`);
      }

      // Handshake successful
      clientData.isHandshaken = true;
      clearTimeout(clientData.handshakeTimeout);
      clientData.handshakeTimeout = null;

      const response = {
        status: 'ready',
        port: this.port,
        version: '2.0.0'
      };

      const formattedResponse = this._formatMessage(response);
      clientData.socket.write(formattedResponse);

      this._log('info', `Handshake complete: ${clientData.clientId}`);
      this.emit('clientConnected', { clientId: clientData.clientId });

      // Process any buffered messages after handshake
      if (clientData.buffer.length > 0) {
        await this._processMessages(clientData);
      }
    } catch (err) {
      this._logError(`Handshake error for ${clientData.clientId}`, err);
      throw new SocketHandshakeError(`Handshake failed: ${err.message}`);
    }
  }

  /**
   * Process incoming messages delimited by \n\n
   */
  async _processMessages(clientData) {
    while (true) {
      const delimiterIndex = clientData.buffer.indexOf(this.messageDelimiter);
      if (delimiterIndex === -1) {
        // No complete message yet
        break;
      }

      const messageStr = clientData.buffer.substring(0, delimiterIndex);
      clientData.buffer = clientData.buffer.substring(delimiterIndex + this.messageDelimiter.length);

      try {
        const message = JSON.parse(messageStr);

        if (this.metrics) {
          this.metrics.recordMessageReceived();
        }

        this._log('debug', `Message from ${clientData.clientId}: ${message.method || 'unknown'}`);

        // Call all registered message handlers
        for (const handler of this.messageHandlers) {
          await handler(clientData.clientId, message);
        }

        this.emit('message', { clientId: clientData.clientId, message });
      } catch (err) {
        this._logError(`Message parse error for ${clientData.clientId}`, err);
        // Continue processing remaining messages
      }
    }
  }

  /**
   * Format message with delimiter
   */
  _formatMessage(obj) {
    return JSON.stringify(obj) + this.messageDelimiter;
  }

  /**
   * Clean up client resources
   */
  _cleanupClient(clientData) {
    if (clientData.handshakeTimeout) {
      clearTimeout(clientData.handshakeTimeout);
    }

    try {
      clientData.socket.destroy();
    } catch (err) {
      // Already closed
    }

    this.clients.delete(clientData.clientId);
  }

  /**
   * Generate unique client ID
   */
  _generateClientId() {
    return `client-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Log helper
   */
  _log(level, message) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log(level, `[SocketTransport] ${message}`);
    } else {
      console.log(`[${level.toUpperCase()}] [SocketTransport] ${message}`);
    }
  }

  /**
   * Log error helper
   */
  _logError(message, error) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log('error', `[SocketTransport] ${message}`, error);
    } else {
      console.error(`[ERROR] [SocketTransport] ${message}:`, error);
    }
  }
}

export default SocketTransportServer;
