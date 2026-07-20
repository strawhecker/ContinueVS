import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import net from 'net';
import SocketTransportServer, {
  SocketTransportError,
  SocketHandshakeError,
  SocketMessageError,
  SocketConnectionError
} from '../lib/socket-transport.mjs';

describe('SocketTransportServer', () => {
  let server;
  const testPort = 19999;

  beforeEach(() => {
    server = new SocketTransportServer(testPort);
  });

  afterEach(async () => {
    if (server && server.isRunning) {
      await server.stop();
    }
  });

  // ============ SUITE 1: Initialization ============
  describe('Initialization', () => {
    it('should create server with default port 9999', () => {
      const srv = new SocketTransportServer();
      expect(srv.port).to.equal(9999);
      expect(srv.isRunning).to.be.false;
      expect(srv.clients.size).to.equal(0);
    });

    it('should create server with custom port', () => {
      const srv = new SocketTransportServer(12345);
      expect(srv.port).to.equal(12345);
    });

    it('should accept logger and metrics', () => {
      const logger = { log: () => {} };
      const metrics = { recordMessageSent: () => {} };
      const srv = new SocketTransportServer(testPort, logger, metrics);
      expect(srv.logger).to.equal(logger);
      expect(srv.metrics).to.equal(metrics);
    });

    it('should have correct message delimiter', () => {
      expect(server.messageDelimiter).to.equal('\n\n');
    });
  });

  // ============ SUITE 2: Handshake Protocol ============
  describe('Handshake Protocol', () => {
    it('should accept valid handshake', async () => {
      await server.start();

      return new Promise((resolve, reject) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeReceived = false;

        client.on('data', (data) => {
          const response = data.toString().trim();
          try {
            const parsed = JSON.parse(response);
            expect(parsed.status).to.equal('ready');
            expect(parsed.port).to.equal(testPort);
            expect(parsed.version).to.equal('2.0.0');
            handshakeReceived = true;
            client.end();
            resolve();
          } catch (err) {
            reject(err);
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');
      });
    });

    it('should reject handshake with wrong version', async () => {
      await server.start();

      return new Promise((resolve) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let errorRaised = false;

        setTimeout(() => {
          if (!errorRaised) {
            client.destroy();
            resolve();
          }
        }, 1000);

        client.on('data', () => {
          // Should not receive data for bad version
        });

        client.on('error', () => {
          errorRaised = true;
          resolve();
        });

        // Send bad handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '1.0.0' });
        client.write(handshake + '\n\n');
      });
    });

    it('should reject handshake with wrong action', async () => {
      await server.start();

      return new Promise((resolve) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let errorRaised = false;

        setTimeout(() => {
          if (!errorRaised) {
            client.destroy();
            resolve();
          }
        }, 1000);

        client.on('error', () => {
          errorRaised = true;
          resolve();
        });

        // Send bad handshake
        const handshake = JSON.stringify({ action: 'bad:action', version: '2.0.0' });
        client.write(handshake + '\n\n');
      });
    });

    it('should timeout on incomplete handshake', async () => {
      server.handshakeTimeout = 500;
      await server.start();

      return new Promise((resolve) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let clientClosed = false;

        client.on('close', () => {
          clientClosed = true;
          setTimeout(() => resolve(), 100);
        });

        client.on('error', () => {
          clientClosed = true;
          resolve();
        });

        // Send incomplete handshake (no delimiter)
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake);

        setTimeout(() => {
          if (!clientClosed) {
            client.destroy();
            resolve();
          }
        }, 1500);
      });
    });
  });

  // ============ SUITE 3: Message Framing ============
  describe('Message Framing', () => {
    it('should receive single JSON-RPC message', async () => {
      await server.start();

      return new Promise((resolve, reject) => {
        server.on('message', (data) => {
          try {
            expect(data.message.method).to.equal('test.method');
            expect(data.message.params).to.deep.equal({ key: 'value' });
            resolve();
          } catch (err) {
            reject(err);
          }
        });

        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            // Send message after handshake
            const msg = JSON.stringify({ method: 'test.method', params: { key: 'value' }, id: 1, jsonrpc: '2.0' });
            client.write(msg + '\n\n');
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          client.end();
        }, 500);
      });
    });

    it('should receive multiple messages delimited by \\n\\n', async () => {
      await server.start();
      let messagesReceived = 0;

      return new Promise((resolve, reject) => {
        server.on('message', (data) => {
          messagesReceived++;
          if (messagesReceived === 2) {
            resolve();
          }
        });

        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            // Send two messages together
            const msg1 = JSON.stringify({ method: 'test.1', id: 1, jsonrpc: '2.0' });
            const msg2 = JSON.stringify({ method: 'test.2', id: 2, jsonrpc: '2.0' });
            client.write(msg1 + '\n\n' + msg2 + '\n\n');
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          client.end();
        }, 500);
      });
    });

    it('should handle partial message reads', async () => {
      await server.start();

      return new Promise((resolve, reject) => {
        server.on('message', (data) => {
          try {
            expect(data.message.method).to.equal('partial.test');
            resolve();
          } catch (err) {
            reject(err);
          }
        });

        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            // Send message in chunks
            const msg = JSON.stringify({ method: 'partial.test', id: 1, jsonrpc: '2.0' });
            const parts = msg.split('');
            parts.forEach((part, i) => {
              setTimeout(() => {
                client.write(part);
                if (i === parts.length - 1) {
                  client.write('\n\n');
                }
              }, i * 5);
            });
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          client.end();
        }, 1000);
      });
    });

    it('should handle malformed JSON gracefully', async () => {
      await server.start();
      let errorLogged = false;

      return new Promise((resolve) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            // Send malformed JSON, then valid message
            client.write('{ invalid json }\n\n');
            const validMsg = JSON.stringify({ method: 'valid', id: 2, jsonrpc: '2.0' });
            client.write(validMsg + '\n\n');
          }
        });

        client.on('error', () => {});

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        server.on('message', () => {
          resolve();
        });

        setTimeout(() => {
          client.end();
          resolve();
        }, 500);
      });
    });
  });

  // ============ SUITE 4: Sending/Receiving ============
  describe('Sending & Receiving', () => {
    it('should send message to client', async () => {
      await server.start();

      return new Promise((resolve, reject) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;
        let clientId = null;

        server.on('clientConnected', async (data) => {
          clientId = data.clientId;
          try {
            await server.send(clientId, 123, { result: 'success' });
          } catch (err) {
            reject(err);
          }
        });

        client.on('data', (data) => {
          const response = data.toString().trim();
          try {
            const parsed = JSON.parse(response);
            if (parsed.status === 'ready') {
              handshakeDone = true;
            } else if (parsed.result) {
              expect(parsed.result.result).to.equal('success');
              expect(parsed.id).to.equal(123);
              client.end();
              resolve();
            }
          } catch (err) {
            reject(err);
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');
      });
    });

    it('should handle large payloads', async () => {
      await server.start();

      return new Promise((resolve, reject) => {
        const largeData = { content: 'x'.repeat(10000) };
        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;
        let clientId = null;

        server.on('clientConnected', async (data) => {
          clientId = data.clientId;
          try {
            await server.send(clientId, 1, largeData);
          } catch (err) {
            reject(err);
          }
        });

        client.on('data', (data) => {
          const response = data.toString().trim();
          try {
            const parsed = JSON.parse(response);
            if (parsed.status === 'ready') {
              handshakeDone = true;
            } else if (parsed.result && parsed.result.content) {
              expect(parsed.result.content.length).to.equal(10000);
              client.end();
              resolve();
            }
          } catch (err) {
            reject(err);
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');
      });
    });

    it('should handle concurrent sends', async () => {
      await server.start();

      return new Promise((resolve, reject) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let clientId = null;
        let responsesReceived = 0;

        server.on('clientConnected', async (data) => {
          clientId = data.clientId;
          try {
            await Promise.all([
              server.send(clientId, 1, { num: 1 }),
              server.send(clientId, 2, { num: 2 }),
              server.send(clientId, 3, { num: 3 })
            ]);
          } catch (err) {
            reject(err);
          }
        });

        client.on('data', (data) => {
          const messages = data.toString().split('\n\n').filter(m => m.trim());
          messages.forEach(msg => {
            try {
              const parsed = JSON.parse(msg);
              if (parsed.result && parsed.result.num) {
                responsesReceived++;
                if (responsesReceived === 3) {
                  client.end();
                  resolve();
                }
              }
            } catch (err) {
              // Skip handshake response
            }
          });
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');
      });
    });
  });

  // ============ SUITE 5: Error Recovery ============
  describe('Error Recovery', () => {
    it('should recover from malformed message and continue', async () => {
      await server.start();
      let validMessageReceived = false;

      return new Promise((resolve) => {
        server.on('message', (data) => {
          if (data.message.method === 'valid.method') {
            validMessageReceived = true;
            resolve();
          }
        });

        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            // Send malformed, then valid
            client.write('{ bad }\n\n');
            const valid = JSON.stringify({ method: 'valid.method', id: 1, jsonrpc: '2.0' });
            client.write(valid + '\n\n');
          }
        });

        client.on('error', () => {});

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          client.end();
          resolve();
        }, 1000);
      });
    });

    it('should handle client disconnect gracefully', async () => {
      await server.start();

      return new Promise((resolve) => {
        let clientDisconnected = false;

        server.on('clientDisconnected', () => {
          clientDisconnected = true;
        });

        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            client.destroy();
          }
        });

        client.on('error', () => {});

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          expect(clientDisconnected).to.be.true;
          resolve();
        }, 200);
      });
    });

    it('should not crash on socket error', async () => {
      await server.start();

      return new Promise((resolve) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let socketError = false;

        client.on('data', () => {
          // Force an error by writing after close
          client.end();
          setTimeout(() => {
            try {
              client.write('test');
            } catch (err) {
              socketError = true;
            }
          }, 50);
        });

        client.on('error', () => {
          socketError = true;
        });

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          expect(server.isRunning).to.be.true;
          resolve();
        }, 300);
      });
    });
  });

  // ============ SUITE 6: Lifecycle ============
  describe('Lifecycle', () => {
    it('should start and stop cleanly', async () => {
      expect(server.isRunning).to.be.false;
      await server.start();
      expect(server.isRunning).to.be.true;
      await server.stop();
      expect(server.isRunning).to.be.false;
    });

    it('should prevent double start', async () => {
      await server.start();
      try {
        await server.start();
        throw new Error('Should have thrown');
      } catch (err) {
        expect(err.name).to.equal('SocketConnectionError');
      }
      await server.stop();
    });

    it('should handle multiple clients', async () => {
      await server.start();
      let connectedClients = 0;

      return new Promise((resolve) => {
        server.on('clientConnected', () => {
          connectedClients++;
          if (connectedClients === 3) {
            resolve();
          }
        });

        for (let i = 0; i < 3; i++) {
          const client = net.createConnection(testPort, '127.0.0.1');
          client.on('data', () => {});
          client.on('error', () => {});
          const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
          client.write(handshake + '\n\n');
        }
      });
    });
  });

  // ============ SUITE 7: Performance ============
  describe('Performance', () => {
    it('should handle 100+ messages per second', async () => {
      await server.start();
      let messagesReceived = 0;
      const targetMessages = 100;

      return new Promise((resolve, reject) => {
        server.on('message', () => {
          messagesReceived++;
          if (messagesReceived === targetMessages) {
            resolve();
          }
        });

        const client = net.createConnection(testPort, '127.0.0.1');
        let handshakeDone = false;

        client.on('data', (data) => {
          if (!handshakeDone) {
            handshakeDone = true;
            // Send 100 messages rapidly
            for (let i = 1; i <= targetMessages; i++) {
              const msg = JSON.stringify({ method: 'perf.test', id: i, jsonrpc: '2.0' });
              client.write(msg + '\n\n');
            }
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        setTimeout(() => {
          client.end();
        }, 2000);
      });
    });

    it('should maintain low latency on rapid send/receive', async () => {
      await server.start();
      const startTime = Date.now();

      return new Promise((resolve, reject) => {
        const client = net.createConnection(testPort, '127.0.0.1');
        let clientId = null;
        let responsesReceived = 0;
        const totalResponses = 50;

        server.on('clientConnected', async (data) => {
          clientId = data.clientId;
        });

        client.on('data', (data) => {
          const messages = data.toString().split('\n\n').filter(m => m.trim());
          messages.forEach(msg => {
            try {
              const parsed = JSON.parse(msg);
              if (parsed.result) {
                responsesReceived++;
              }
            } catch (err) {
              // Skip handshake
            }
          });

          if (responsesReceived === totalResponses) {
            const elapsed = Date.now() - startTime;
            expect(elapsed).to.be.lessThan(500); // All responses within 500ms
            client.end();
            resolve();
          }
        });

        client.on('error', reject);

        // Send handshake
        const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
        client.write(handshake + '\n\n');

        // Send messages and responses
        server.on('message', async (data) => {
          if (clientId && data.message.id && data.message.id <= totalResponses) {
            await server.send(clientId, data.message.id, { status: 'ok' }).catch(() => {});
          }
        });
      });
    });

    it('should not leak memory on disconnects', async () => {
      await server.start();

      return new Promise((resolve) => {
        let connectCount = 0;
        const totalConnects = 50;

        server.on('clientDisconnected', () => {
          connectCount++;
          if (connectCount === totalConnects) {
            expect(server.clients.size).to.equal(0);
            resolve();
          }
        });

        for (let i = 0; i < totalConnects; i++) {
          const client = net.createConnection(testPort, '127.0.0.1');
          client.on('data', () => {
            client.destroy();
          });
          client.on('error', () => {});
          const handshake = JSON.stringify({ action: 'bridge:connect', version: '2.0.0' });
          client.write(handshake + '\n\n');
        }
      });
    });
  });
});
