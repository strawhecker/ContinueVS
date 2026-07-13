export class DocumentProviderError extends Error {
  constructor(message, operationType = 'unknown', originalError = null) {
    super(message);
    this.name = 'DocumentProviderError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

export class DocumentValidationError extends Error {
  constructor(fieldName, message, value = null) {
    super(`${fieldName}: ${message}`);
    this.name = 'DocumentValidationError';
    this.fieldName = fieldName;
    this.value = value;
  }
}

export class DocumentProvider {
  constructor(options = {}) {
    if (typeof options !== 'object' || options === null || Array.isArray(options)) {
      throw new Error('DocumentProvider options must be a plain object');
    }
    this.logger = options.logger || this._createMockLogger();
    this.metrics = options.metrics || this._createMockMetrics();
    this._documents = new Map();
    this._changeListeners = [];
    this._openListeners = [];
    this._closeListeners = [];
    this._lastUpdate = Date.now();
    this.logger.debug('DocumentProvider initialized');
    this.metrics.recordEvent('document_provider_initialized', { timestamp: this._lastUpdate });
  }

  async registerMessageHandlers(server) {
    if (!server || typeof server !== 'object') {
      throw new DocumentProviderError('server must be a valid object', 'registration', null);
    }
    if (!server.messageHandler || typeof server.messageHandler.on !== 'function') {
      throw new DocumentProviderError('server.messageHandler.on() not available', 'registration', null);
    }
    try {
      server.messageHandler.on('openDocuments', (message) => this._handleOpenDocumentsMessage(message));
      server.messageHandler.on('didOpenDocument', (message) => this._handleDidOpenDocumentMessage(message));
      server.messageHandler.on('didChangeDocument', (message) => this._handleDidChangeDocumentMessage(message));
      server.messageHandler.on('didCloseDocument', (message) => this._handleDidCloseDocumentMessage(message));
      this.logger.debug('DocumentProvider registered for document lifecycle messages');
      this.metrics.recordEvent('document_provider_registered', { timestamp: Date.now() });
    } catch (error) {
      throw new DocumentProviderError(`Failed to register message handlers: ${error.message}`, 'registration', error);
    }
  }

  getDocument(filepath) {
    if (!filepath || typeof filepath !== 'string') return null;
    const doc = this._documents.get(filepath);
    return doc ? this._getDocumentCopy(doc) : null;
  }

  getAllDocuments() {
    return Array.from(this._documents.values()).map((doc) => this._getDocumentCopy(doc));
  }

  getDocumentByLanguage(language) {
    if (!language || typeof language !== 'string') return [];
    return Array.from(this._documents.values()).filter((doc) => doc.language === language).map((doc) => this._getDocumentCopy(doc));
  }

  getDocumentMetadata(filepath) {
    if (!filepath || typeof filepath !== 'string') return null;
    const doc = this._documents.get(filepath);
    if (!doc) return null;
    return { filepath: doc.filepath, language: doc.language, isDirty: doc.isDirty, lines: doc.lines, encoding: doc.encoding, lastModified: doc.lastModified, metadata: { ...doc.metadata } };
  }

  hasDocument(filepath) {
    return !!filepath && typeof filepath === 'string' && this._documents.has(filepath);
  }

  getDocumentCount() {
    return this._documents.size;
  }

  onDocumentChange(callback) {
    if (typeof callback !== 'function') throw new TypeError('onDocumentChange callback must be a function');
    this._changeListeners.push(callback);
    return () => { const idx = this._changeListeners.indexOf(callback); if (idx >= 0) this._changeListeners.splice(idx, 1); };
  }

  onDocumentOpen(callback) {
    if (typeof callback !== 'function') throw new TypeError('onDocumentOpen callback must be a function');
    this._openListeners.push(callback);
    return () => { const idx = this._openListeners.indexOf(callback); if (idx >= 0) this._openListeners.splice(idx, 1); };
  }

  onDocumentClose(callback) {
    if (typeof callback !== 'function') throw new TypeError('onDocumentClose callback must be a function');
    this._closeListeners.push(callback);
    return () => { const idx = this._closeListeners.indexOf(callback); if (idx >= 0) this._closeListeners.splice(idx, 1); };
  }

  dispose() {
    this._documents.clear();
    this._changeListeners = [];
    this._openListeners = [];
    this._closeListeners = [];
    this.logger.debug('DocumentProvider disposed');
    this.metrics.recordEvent('document_provider_disposed', { timestamp: Date.now() });
  }

  _handleOpenDocumentsMessage(message) {
    try {
      if (!message || !message.data || !Array.isArray(message.data.documents)) { this.logger.error('Invalid openDocuments message structure'); return; }
      this._documents.clear();
      const documents = message.data.documents;
      for (const docData of documents) {
        try {
          const doc = this._normalizeDocument(docData);
          this._documents.set(doc.filepath, doc);
          this._notifyOpenListeners(doc);
        } catch (error) {
          this.logger.error(`Failed to process document in openDocuments: ${error.message}`);
        }
      }
      this._lastUpdate = Date.now();
      this.metrics.recordEvent('document_provider_bulk_opened', { count: this._documents.size });
    } catch (error) {
      this.logger.error(`Error handling openDocuments message: ${error.message}`);
    }
  }

  _handleDidOpenDocumentMessage(message) {
    try {
      if (!message || !message.data) { this.logger.error('Invalid didOpenDocument message structure'); return; }
      const doc = this._normalizeDocument(message.data);
      this._documents.set(doc.filepath, doc);
      this._notifyOpenListeners(doc);
      this._lastUpdate = Date.now();
      this.metrics.recordEvent('document_provider_document_opened', { filepath: doc.filepath });
    } catch (error) {
      this.logger.error(`Error handling didOpenDocument message: ${error.message}`);
    }
  }

  _handleDidChangeDocumentMessage(message) {
    try {
      if (!message || !message.data || !message.data.filepath) { this.logger.error('Invalid didChangeDocument message structure'); return; }
      const filepath = message.data.filepath;
      const oldDoc = this._documents.get(filepath);
      if (!oldDoc) { this.logger.debug(`didChangeDocument received for unknown document: ${filepath}`); return; }
      const updatedData = { ...oldDoc, ...message.data, lastModified: Date.now() };
      const newDoc = this._normalizeDocument(updatedData);
      this._documents.set(filepath, newDoc);
      this._notifyChangeListeners(newDoc, oldDoc);
      this._lastUpdate = Date.now();
      this.metrics.recordEvent('document_provider_document_changed', { filepath });
    } catch (error) {
      this.logger.error(`Error handling didChangeDocument message: ${error.message}`);
    }
  }

  _handleDidCloseDocumentMessage(message) {
    try {
      if (!message || !message.data || !message.data.filepath) { this.logger.error('Invalid didCloseDocument message structure'); return; }
      const filepath = message.data.filepath;
      this._documents.delete(filepath);
      this._notifyCloseListeners(filepath);
      this._lastUpdate = Date.now();
      this.metrics.recordEvent('document_provider_document_closed', { filepath });
    } catch (error) {
      this.logger.error(`Error handling didCloseDocument message: ${error.message}`);
    }
  }

  _normalizeDocument(doc) {
    if (!doc || typeof doc !== 'object') throw new DocumentValidationError('document', 'must be a valid object', doc);
    if (!doc.filepath || typeof doc.filepath !== 'string') throw new DocumentValidationError('filepath', 'must be a non-empty string', doc.filepath);
    if (doc.contents === undefined || doc.contents === null) throw new DocumentValidationError('contents', 'must be defined', doc.contents);
    if (typeof doc.contents !== 'string') throw new DocumentValidationError('contents', 'must be a string', typeof doc.contents);
    if (!doc.language || typeof doc.language !== 'string') throw new DocumentValidationError('language', 'must be a non-empty string', doc.language);
    const lines = doc.contents ? doc.contents.split('\n').length : 0;
    return { filepath: doc.filepath, contents: doc.contents, language: doc.language, isDirty: doc.isDirty === true || doc.isDirty === false ? doc.isDirty : false, encoding: doc.encoding || 'utf-8', lines, lastModified: typeof doc.lastModified === 'number' ? doc.lastModified : Date.now(), metadata: (doc.metadata && typeof doc.metadata === 'object') ? { ...doc.metadata } : {} };
  }

  _getDocumentCopy(doc) {
    return { filepath: doc.filepath, contents: doc.contents, language: doc.language, isDirty: doc.isDirty, encoding: doc.encoding, lines: doc.lines, lastModified: doc.lastModified, metadata: { ...doc.metadata } };
  }

  _notifyChangeListeners(newDoc, oldDoc) {
    const listeners = [...this._changeListeners];
    for (const listener of listeners) {
      try {
        listener(this._getDocumentCopy(newDoc), oldDoc ? this._getDocumentCopy(oldDoc) : null);
      } catch (error) {
        this.logger.error(`Error in onDocumentChange listener: ${error.message}`);
      }
    }
  }

  _notifyOpenListeners(doc) {
    const listeners = [...this._openListeners];
    for (const listener of listeners) {
      try {
        listener(this._getDocumentCopy(doc));
      } catch (error) {
        this.logger.error(`Error in onDocumentOpen listener: ${error.message}`);
      }
    }
  }

  _notifyCloseListeners(filepath) {
    const listeners = [...this._closeListeners];
    for (const listener of listeners) {
      try {
        listener(filepath);
      } catch (error) {
        this.logger.error(`Error in onDocumentClose listener: ${error.message}`);
      }
    }
  }

  _createMockLogger() {
    return { debug: () => {}, error: () => {} };
  }

  _createMockMetrics() {
    return { recordEvent: () => {} };
  }
}
