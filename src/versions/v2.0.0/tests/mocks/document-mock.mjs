#!/usr/bin/env node

/**
 * Document Provider - Test Fixtures & Mocks
 *
 * Provides mock document data, logger, metrics, and server objects for testing.
 *
 * @module src/versions/v2.0.0/tests/mocks/document-mock.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

/**
 * Create a mock C# document
 * @param {Object} [overrides] - Override properties
 * @returns {Object} Mock C# document
 */
export function getMockCSharpDocument(overrides = {}) {
  return {
    filepath: '/path/to/Example.cs',
    contents: `using System;

namespace Example
{
    public class HelloWorld
    {
        public static void Main()
        {
            Console.WriteLine("Hello, World!");
        }
    }
}`,
    language: 'csharp',
    isDirty: false,
    encoding: 'utf-8',
    metadata: {
      projectPath: '/path/to/project.csproj',
      framework: 'net7.0',
      compiler: 'roslyn'
    },
    ...overrides
  };
}

/**
 * Create a mock JavaScript document
 * @param {Object} [overrides] - Override properties
 * @returns {Object} Mock JavaScript document
 */
export function getMockJavaScriptDocument(overrides = {}) {
  return {
    filepath: '/path/to/index.js',
    contents: `function greet(name) {
  return \`Hello, \${name}!\`;
}

module.exports = { greet };`,
    language: 'javascript',
    isDirty: false,
    encoding: 'utf-8',
    metadata: {
      projectPath: '/path/to/package.json'
    },
    ...overrides
  };
}

/**
 * Create a mock Python document
 * @param {Object} [overrides] - Override properties
 * @returns {Object} Mock Python document
 */
export function getMockPythonDocument(overrides = {}) {
  return {
    filepath: '/path/to/main.py',
    contents: `def greet(name):
    return f"Hello, {name}!"

if __name__ == "__main__":
    print(greet("World"))`,
    language: 'python',
    isDirty: false,
    encoding: 'utf-8',
    metadata: {
      projectPath: '/path/to/project'
    },
    ...overrides
  };
}

/**
 * Create a generic mock document
 * @param {string} [language] - Programming language
 * @param {boolean} [isDirty] - Whether document has unsaved changes
 * @param {Object} [overrides] - Override properties
 * @returns {Object} Mock document
 */
export function getMockDocument(language = 'csharp', isDirty = false, overrides = {}) {
  const basePath = `/path/to/file.${language === 'csharp' ? 'cs' : language === 'python' ? 'py' : 'js'}`;
  return {
    filepath: overrides.filepath || basePath,
    contents: overrides.contents || `// Sample ${language} code`,
    language,
    isDirty,
    encoding: 'utf-8',
    metadata: {},
    ...overrides
  };
}

/**
 * Create a very large mock document (>100k lines)
 * @param {string} [language] - Programming language
 * @returns {Object} Large mock document
 */
export function getLargeDocument(language = 'csharp') {
  const lines = [];
  for (let i = 0; i < 50000; i++) {
    lines.push(`// Line ${i + 1}: This is a test line with some content`);
  }

  return {
    filepath: `/path/to/large-file.${language === 'csharp' ? 'cs' : 'js'}`,
    contents: lines.join('\n'),
    language,
    isDirty: false,
    encoding: 'utf-8',
    metadata: { isLargeFile: true }
  };
}

/**
 * Create a mock logger
 * @returns {Object} Mock logger with debug() and error() methods
 */
export function createMockLogger() {
  const logs = {
    debug: [],
    error: []
  };

  return {
    debug: (msg, ...args) => {
      logs.debug.push({ msg, args, timestamp: Date.now() });
    },
    error: (msg, ...args) => {
      logs.error.push({ msg, args, timestamp: Date.now() });
    },
    getLogs: () => logs,
    getDebugLogs: () => logs.debug,
    getErrorLogs: () => logs.error,
    clear: () => {
      logs.debug = [];
      logs.error = [];
    }
  };
}

/**
 * Create a mock metrics collector
 * @returns {Object} Mock metrics with recordEvent() method
 */
export function createMockMetrics() {
  const events = [];

  return {
    recordEvent: (eventName, data = {}) => {
      events.push({ eventName, data, timestamp: Date.now() });
    },
    getEvents: () => events,
    getEventsByName: (name) => events.filter((e) => e.eventName === name),
    clear: () => {
      events.length = 0;
    }
  };
}

/**
 * Create a mock message handler
 * @returns {Object} Mock message handler with on() method
 */
export function createMockMessageHandler() {
  const handlers = new Map();

  return {
    on: (messageType, callback) => {
      if (!handlers.has(messageType)) {
        handlers.set(messageType, []);
      }
      handlers.get(messageType).push(callback);
    },
    emit: (messageType, message) => {
      if (handlers.has(messageType)) {
        handlers.get(messageType).forEach((callback) => callback(message));
      }
    },
    getHandlers: () => handlers,
    clear: () => {
      handlers.clear();
    }
  };
}

/**
 * Create a mock bridge server
 * @returns {Object} Mock server with messageHandler
 */
export function createMockServer() {
  return {
    messageHandler: createMockMessageHandler()
  };
}

/**
 * Create a complete test context with logger, metrics, and server
 * @returns {Object} Complete test context
 */
export function createTestContext() {
  return {
    logger: createMockLogger(),
    metrics: createMockMetrics(),
    server: createMockServer(),
    messageHandler: null
  };
}

/**
 * Helper to create a document with dirty changes
 * @param {string} [language] - Programming language
 * @returns {Object} Mock document with isDirty=true
 */
export function getMockDirtyDocument(language = 'csharp') {
  return getMockDocument(language, true);
}

/**
 * Helper to create multiple mock documents
 * @param {number} [count] - Number of documents
 * @param {string} [baseLanguage] - Base language for all documents
 * @returns {Object[]} Array of mock documents
 */
export function getMockDocuments(count = 3, baseLanguage = 'csharp') {
  const docs = [];
  const languages = ['csharp', 'javascript', 'python'];
  for (let i = 0; i < count; i++) {
    const lang = baseLanguage === 'mixed' ? languages[i % languages.length] : baseLanguage;
    docs.push(getMockDocument(lang, i % 2 === 0, {
      filepath: `/path/to/file${i}.${lang === 'csharp' ? 'cs' : lang === 'python' ? 'py' : 'js'}`
    }));
  }
  return docs;
}
