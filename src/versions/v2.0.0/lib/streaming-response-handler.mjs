#!/usr/bin/env node

/**
 * Streaming-Response Handler for Continue Bridge
 */

export class StreamingError extends Error {
  constructor(message, errorCode = 'STREAMING_ERROR', originalError = null, retryable = false) {
    super(message);
    this.name = 'StreamingError';
    this.errorCode = errorCode;
    this.originalError = originalError;
    this.retryable = retryable;
  }
}

export class InvalidStreamingRequestError extends Error {
  constructor(message, field = null, value = null) {
    super(message);
    this.name = 'InvalidStreamingRequestError';
    this.field = field;
    this.value = value;
  }
}

export class ModelNotFoundError extends Error {
  constructor(title = '') {
    super(No LLM model configured for title: "");
    this.name = 'ModelNotFoundError';
    this.title = title;
  }
}

export class StreamInterruptedError extends Error {
  constructor(message = 'Stream was interrupted', chunksReceived = 0) {
    super(message);
    this.name = 'StreamInterruptedError';
    this.chunksReceived = chunksReceived;
  }
}

function validateStreamingRequest(message) {
  if (!message || typeof message !== 'object') {
    throw new InvalidStreamingRequestError('Message must be an object', 'message', message);
  }

  const { data } = message;
  if (!data || typeof data !== 'object') {
    throw new InvalidStreamingRequestError('Message.data must be an object', 'data', data);
  }

  const { title, messages } = data;

  if (typeof title !== 'string' || title.trim().length === 0) {
    throw new InvalidStreamingRequestError('title must be a non-empty string', 'title', title);
  }

  if (!Array.isArray(messages)) {
    throw new InvalidStreamingRequestError('messages must be an array', 'messages', typeof messages);
  }

  if (messages.length === 0) {
    throw new InvalidStreamingRequestError('messages array cannot be empty', 'messages.length', 0);
  }

  for (let i = 0; i < messages.length; i++) {
    const msg = messages[i];
    if (!msg || typeof msg !== 'object') {
      throw new InvalidStreamingRequestError(messages[] must be an object, messages[], msg);
    }
    if (typeof msg.role !== 'string' || msg.role.trim().length === 0) {
      throw new InvalidStreamingRequestError(messages[].role must be a non-empty string, messages[].role, msg.role);
    }
    if (typeof msg.content !== 'string') {
      throw new InvalidStreamingRequestError(messages[].content must be a string, messages[].content, typeof msg.content);
    }
  }

  return { title: title.trim(), messages };
}

function estimateTokens(text) {
  if (!text || typeof text !== 'string') return 0;
  return Math.ceil(text.length / 4);
}

function createDefaultLogger() {
  return {
    debug: (msg) => console.debug([DEBUG] ),
    info: (msg) => console.log([INFO] ),
    warn: (msg) => console.warn([WARN] ),
    error: (msg) => console.error([ERROR] )
  };
}

function createDefaultMetrics() {
  return {
    recordLatency: (metric, value, status) => {},
    recordEvent: (event, data) => {}
  };
}

export function createStreamingResponseHandler(options = {}) {
  const logger = options.logger || createDefaultLogger();
  const metrics = options.metrics || createDefaultMetrics();
  const resolveIpcClient = options.ipcClient || (() => globalThis.ideIpcClient);

  async function handleStreamingRequest(message, context = {}) {
    const startTime = Date.now();
    let accumulatedContent = '';
    let chunkCount = 0;
    let estimatedTokenCount = 0;
    const requestId = message?.messageId || 'unknown';

    try {
      const { title, messages } = validateStreamingRequest(message);
      logger.debug([Streaming] Request validated: title="", messages=, requestId=);

      const ipcClient = resolveIpcClient();
      if (!ipcClient) {
        throw new StreamingError(
          'IPC client not available; cannot communicate with C# backend',
          'IPC_NOT_AVAILABLE',
          null,
          true
        );
      }

      const modelConfigured = await ipcClient.hasModel?.(title) ?? true;
      if (!modelConfigured) {
        throw new ModelNotFoundError(title);
      }

      logger.info([Streaming] Initiating stream for model "", requestId=);

      const streamPromise = new Promise((resolve, reject) => {
        let finished = false;

        const onChunk = (chunk) => {
          try {
            if (!chunk || typeof chunk !== 'object') {
              logger.warn([Streaming] Received invalid chunk structure: );
              return;
            }

            const { content = '', role = 'assistant', done = false } = chunk;
            chunkCount += 1;

            if (!done && content) {
              accumulatedContent += content;
              estimatedTokenCount += estimateTokens(content);
            }

            logger.debug([Streaming] Chunk :  chars, done=);

            if (context.onChunk && typeof context.onChunk === 'function') {
              try {
                context.onChunk({
                  sequenceNumber: chunkCount - 1,
                  chunk: { role, content, done },
                  timestamp: Date.now()
                });
              } catch (err) {
                logger.warn([Streaming] onChunk callback threw: );
              }
            }

            if (done) {
              finished = true;
              logger.debug([Streaming] Stream finished after  chunks);
              const latency = Date.now() - startTime;
              resolve({
                role: 'assistant',
                content: accumulatedContent,
                chunks: chunkCount,
                latency,
                estimatedTokens: estimatedTokenCount,
                model: title,
                title
              });
            }
          } catch (err) {
            logger.error([Streaming] Error in onChunk handler: );
            reject(err);
          }
        };

        const onError = (err) => {
          if (finished) return;
          logger.error([Streaming] Stream error: , chunks received: );
          reject(new StreamInterruptedError(err.message, chunkCount));
        };

        const onComplete = () => {
          if (!finished) {
            finished = true;
            const latency = Date.now() - startTime;
            logger.info([Streaming] Stream completed:  chunks,  chars, ms);
            resolve({
              role: 'assistant',
              content: accumulatedContent,
              chunks: chunkCount,
              latency,
              estimatedTokens: estimatedTokenCount,
              model: title,
              title
            });
          }
        };

        try {
          if (ipcClient.streamChat && typeof ipcClient.streamChat === 'function') {
            ipcClient.streamChat(title, messages, onChunk, onError, onComplete);
          } else {
            throw new StreamingError('IPC client does not support streamChat method', 'IPC_METHOD_NOT_FOUND');
          }
        } catch (err) {
          logger.error([Streaming] Failed to initiate stream: );
          reject(err);
        }
      });

      const result = await streamPromise;

      metrics.recordLatency('streaming.request', result.latency, 'success');
      metrics.recordEvent('streaming.completed', {
        chunks: result.chunks,
        latency: result.latency,
        estimatedTokens: result.estimatedTokens,
        contentLength: result.content.length
      });

      logger.info([Streaming] Request completed successfully:  chunks, ms, requestId=);
      return result;

    } catch (err) {
      const latency = Date.now() - startTime;

      if (err instanceof InvalidStreamingRequestError) {
        logger.warn([Streaming] Invalid request (field=): );
        metrics.recordEvent('streaming.error.invalid_request', { field: err.field, latency });
        throw err;
      }

      if (err instanceof ModelNotFoundError) {
        logger.warn([Streaming] Model not found: "");
        metrics.recordEvent('streaming.error.model_not_found', { title: err.title, latency });
        throw err;
      }

      if (err instanceof StreamInterruptedError) {
        logger.error([Streaming] Stream interrupted after  chunks: );
        metrics.recordLatency('streaming.request', latency, 'interrupted');
        metrics.recordEvent('streaming.error.interrupted', {
          chunksReceived: err.chunksReceived,
          accumulatedLength: accumulatedContent.length,
          latency
        });
        throw err;
      }

      if (err instanceof StreamingError) {
        const errorLevel = err.retryable ? 'warn' : 'error';
        logger[errorLevel]([Streaming] Stream error (retryable=): );
        metrics.recordLatency('streaming.request', latency, err.retryable ? 'retryable' : 'failed');
        metrics.recordEvent('streaming.error.streaming', {
          errorCode: err.errorCode,
          retryable: err.retryable,
          latency,
          chunksReceived: chunkCount
        });
        throw err;
      }

      logger.error([Streaming] Unknown error: );
      metrics.recordLatency('streaming.request', latency, 'failed');
      metrics.recordEvent('streaming.error.unknown', { latency, chunksReceived: chunkCount });
      throw new StreamingError(
        Unexpected error during streaming: ,
        'UNKNOWN_ERROR',
        err,
        false
      );
    }
  }

  return handleStreamingRequest;
}

export default createStreamingResponseHandler;
