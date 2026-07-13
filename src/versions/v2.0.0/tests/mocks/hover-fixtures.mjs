/**
 * Test Fixtures for Hover-Info Handler (Step 59)
 * Provides mock data and expected outputs for hover testing
 *
 * @module src/versions/v2.0.0/tests/mocks/hover-fixtures.mjs
 */

/**
 * Mock class symbol
 */
export function getClassSymbol() {
  return {
    name: 'UserService',
    kind: 'class',
    signature: 'public class UserService',
    documentation: 'Service for managing user operations and authentication.',
    deprecated: false,
    range: {
      start: { line: 5, column: 0 },
      end: { line: 150, column: 1 },
    },
  };
}

/**
 * Mock method symbol
 */
export function getMethodSymbol() {
  return {
    name: 'GetUserById',
    kind: 'method',
    signature: 'public async Task<User> GetUserById(int id)',
    documentation: 'Retrieves a user by their unique identifier.',
    deprecated: false,
    range: {
      start: { line: 20, column: 4 },
      end: { line: 35, column: 5 },
    },
  };
}

/**
 * Mock property symbol
 */
export function getPropertySymbol() {
  return {
    name: 'Id',
    kind: 'property',
    signature: 'public int Id { get; set; }',
    documentation: 'The unique identifier for the entity.',
    deprecated: false,
    range: {
      start: { line: 10, column: 8 },
      end: { line: 10, column: 30 },
    },
  };
}

/**
 * Mock deprecated symbol
 */
export function getDeprecatedSymbol() {
  return {
    name: 'OldMethod',
    kind: 'method',
    signature: 'public void OldMethod()',
    documentation: 'Use NewMethod instead.',
    deprecated: true,
    range: {
      start: { line: 40, column: 4 },
      end: { line: 42, column: 5 },
    },
  };
}

/**
 * Mock function symbol
 */
export function getFunctionSymbol() {
  return {
    name: 'calculateSum',
    kind: 'function',
    signature: 'function calculateSum(a: number, b: number): number',
    documentation: 'Calculates the sum of two numbers.',
    deprecated: false,
    range: {
      start: { line: 15, column: 0 },
      end: { line: 20, column: 1 },
    },
  };
}

/**
 * Mock error diagnostic
 */
export function getErrorDiagnostic() {
  return {
    message: "Type 'string' is not assignable to type 'number'.",
    severity: 'error',
    code: 'TS2322',
    source: 'TypeScript',
    range: {
      start: { line: 25, column: 10 },
      end: { line: 25, column: 15 },
    },
  };
}

/**
 * Mock warning diagnostic
 */
export function getWarningDiagnostic() {
  return {
    message: 'Variable is assigned but never used.',
    severity: 'warning',
    code: 'noUnusedVariables',
    source: 'ESLint',
    range: {
      start: { line: 30, column: 6 },
      end: { line: 30, column: 12 },
    },
  };
}

/**
 * Mock deprecation diagnostic
 */
export function getDeprecationDiagnostic() {
  return {
    message: "'String.prototype.split' is deprecated.",
    severity: 'hint',
    code: 'deprecation',
    source: 'TypeScript',
    range: {
      start: { line: 50, column: 8 },
      end: { line: 50, column: 13 },
    },
  };
}

/**
 * Mock class source code
 */
export function getClassSourceCode() {
  return `public class UserService {
  private IUserRepository _repository;

  public UserService(IUserRepository repository) {
    _repository = repository;
  }

  public async Task<User> GetUserById(int id) {
    return await _repository.FindAsync(id);
  }

  public async Task<IEnumerable<User>> GetAll() {
    return await _repository.GetAllAsync();
  }
}`;
}

/**
 * Mock method source code
 */
export function getMethodSourceCode() {
  return `public async Task<User> GetUserById(int id) {
  if (id <= 0) {
    throw new ArgumentException("Invalid user ID");
  }
  return await _repository.FindAsync(id);
}`;
}

/**
 * Mock JSDoc comment
 */
export function getJSDocComment() {
  return `/**
 * Validates user input against business rules
 * @param {string} email - The user email to validate
 * @param {string} password - The user password to validate
 * @returns {boolean} True if validation passes
 * @throws {ValidationError} If validation fails
 */`;
}

/**
 * Mock XmlDoc comment
 */
export function getXmlDocComment() {
  return `/// <summary>
/// Retrieves a user by their unique identifier
/// </summary>
/// <param name="id">The user ID</param>
/// <returns>A User object if found; null otherwise</returns>
/// <exception cref="ArgumentException">Thrown if id is invalid</exception>`;
}

/**
 * Expected hover info for class
 */
export function getExpectedClassHover() {
  return {
    kind: 'class',
    text: 'public class UserService',
    documentation: 'Service for managing user operations and authentication.',
    source: 'symbol',
    range: {
      start: { line: 5, column: 0 },
      end: { line: 150, column: 1 },
    },
  };
}

/**
 * Expected hover info for method
 */
export function getExpectedMethodHover() {
  return {
    kind: 'method',
    text: 'public async Task<User> GetUserById(int id)',
    signature: 'public async Task<User> GetUserById(int id)',
    documentation: 'Retrieves a user by their unique identifier.',
    source: 'symbol',
    range: {
      start: { line: 20, column: 4 },
      end: { line: 35, column: 5 },
    },
  };
}

/**
 * Expected hover info for diagnostic error
 */
export function getExpectedDiagnosticHover() {
  return {
    kind: 'diagnostic',
    text: "Type 'string' is not assignable to type 'number'.",
    documentation: 'TS2322',
    source: 'diagnostic',
    range: {
      start: { line: 25, column: 10 },
      end: { line: 25, column: 10 },
    },
  };
}

/**
 * Expected hover info for deprecated symbol
 */
export function getExpectedDeprecatedHover() {
  return {
    kind: 'method',
    text: 'public void OldMethod()',
    signature: 'public void OldMethod()',
    documentation: 'Use NewMethod instead.',
    deprecated: true,
    source: 'symbol',
    range: {
      start: { line: 40, column: 4 },
      end: { line: 42, column: 5 },
    },
  };
}

/**
 * Expected hover info when no info available
 */
export function getExpectedEmptyHover() {
  return {
    kind: 'unknown',
    text: '',
    source: 'none',
    range: {
      start: { line: 10, column: 5 },
      end: { line: 10, column: 5 },
    },
  };
}

/**
 * Example position that should have symbol hover
 */
export function getSymbolPosition() {
  return { filepath: '/src/UserService.cs', line: 20, column: 10 };
}

/**
 * Example position that should have diagnostic hover
 */
export function getDiagnosticPosition() {
  return { filepath: '/src/file.ts', line: 25, column: 10 };
}

/**
 * Example position with no hover info
 */
export function getEmptyPosition() {
  return { filepath: '/src/empty.ts', line: 100, column: 50 };
}

/**
 * Valid hover request
 */
export function getValidHoverRequest() {
  return {
    filepath: '/src/UserService.cs',
    line: 20,
    column: 10,
    includeDocumentation: true,
    includeSignature: true,
    includeDeprecation: true,
  };
}

/**
 * Hover request without documentation
 */
export function getHoverRequestWithoutDocs() {
  return {
    filepath: '/src/UserService.cs',
    line: 20,
    column: 10,
    includeDocumentation: false,
    includeSignature: true,
    includeDeprecation: true,
  };
}

/**
 * Hover request with out-of-bounds position
 */
export function getOutOfBoundsHoverRequest() {
  return {
    filepath: '/src/file.ts',
    line: 99999,
    column: 99999,
    includeDocumentation: true,
    includeSignature: true,
    includeDeprecation: true,
  };
}

/**
 * Hover request with invalid filepath
 */
export function getInvalidFilepathHoverRequest() {
  return {
    filepath: '',
    line: 0,
    column: 0,
  };
}

/**
 * Bridge message for hover request
 */
export function getHoverBridgeMessage() {
  return {
    type: 'bridge:hoverInfo',
    id: 'msg-001',
    data: getValidHoverRequest(),
  };
}

/**
 * Multiline hover text (generic type, complex signature)
 */
export function getComplexSignature() {
  return {
    name: 'Process',
    kind: 'function',
    signature: 'public static async Task<Result<Dictionary<string, List<T>>>> Process<T>(IEnumerable<T> items, Func<T, Task<bool>> predicate) where T : class',
    documentation: 'Processes a collection of items with async filtering.',
    deprecated: false,
  };
}

/**
 * Generic type symbol
 */
export function getGenericTypeSymbol() {
  return {
    name: 'Repository<T>',
    kind: 'class',
    signature: 'public class Repository<T> where T : class',
    documentation: 'Generic repository for data access.',
    deprecated: false,
  };
}

/**
 * Nested class symbol
 */
export function getNestedClassSymbol() {
  return {
    name: 'UserService.Configuration',
    kind: 'class',
    signature: 'private static class Configuration',
    documentation: 'Configuration for the UserService.',
    deprecated: false,
  };
}
