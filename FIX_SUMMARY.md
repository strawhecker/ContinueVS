# DeepSeek API Integration - Issue Fixed

## Problem Identified

**Error:** `HTTP 400: Invalid schema for function 'read_file': true is not of type "array"`

The issue was in the tool schema JSON being sent to DeepSeek. The `ParameterDefinition` class was incorrectly serializing the `IsRequired` boolean property as `"required": true` in the JSON.

### Root Cause

In OpenAI/DeepSeek JSON Schema format:
- ✅ **CORRECT**: `required` should be an **array at the ParametersSchema level**
  ```json
  {
    "type": "object",
    "properties": { "filepath": { "type": "string" } },
    "required": ["filepath"]  // Array of required parameter names
  }
  ```

- ❌ **INCORRECT**: `required` should **NOT** be a boolean property on individual parameters
  ```json
  {
    "properties": {
      "filepath": {
        "type": "string",
        "required": true  // ← This is WRONG for DeepSeek
      }
    }
  }
  ```

## The Fix

**File:** `src/VSIXProject1/Core/Types/ToolDefinition.cs` (Line 30-34)

**Before:**
```csharp
/// <summary>
/// Whether this parameter is required for tool invocation.
/// </summary>
[JsonProperty("required")]
public bool IsRequired { get; set; } = false;
```

**After:**
```csharp
/// <summary>
/// Whether this parameter is required for tool invocation.
/// Note: This property is NOT serialized to JSON (no JsonProperty attribute).
/// Required parameters are communicated via the "required" array at the ParametersSchema level,
/// not as a property on each individual parameter.
/// </summary>
public bool IsRequired { get; set; } = false;
```

### What Changed
- **Removed** the `[JsonProperty("required")]` attribute
- This prevents `IsRequired` from being serialized into the JSON
- The `IsRequired` logic is still used internally by `MessengerService.ConvertToolsToSchema()` to populate the `ParametersSchema.Required` array correctly

## How It Works Now

1. **Tool Definition** (C# object):
   ```csharp
   new ParameterDefinition 
   { 
       Name = "filepath", 
       Type = "string", 
       IsRequired = true  // Used internally only
   }
   ```

2. **Schema Conversion** (MessengerService.cs line 1043-1059):
   ```csharp
   var paramsSchema = new ParametersSchema
   {
       Type = "object",
       Properties = new Dictionary<string, ParameterDefinition>(),
       Required = new List<string>()  // ← Collects required param names
   };

   foreach (var param in tool.Parameters)
   {
       paramsSchema.Properties[param.Name] = param;
       if (param.IsRequired)  // ← Checks IsRequired here
           paramsSchema.Required.Add(param.Name);  // ← Adds to array
   }
   ```

3. **JSON Sent to DeepSeek**:
   ```json
   {
     "type": "object",
     "properties": {
       "filepath": { "type": "string" }
     },
     "required": ["filepath"]  // ← Correct format
   }
   ```

## Testing

### Before Fix
- Request failed with HTTP 400
- Error: `true is not of type "array"`
- DeepSeek rejected the malformed schema

### After Fix
- Schema is properly formatted
- Matches OpenAI/DeepSeek JSON Schema specification
- Tools should now work correctly with DeepSeek

## Files Changed
1. `src/VSIXProject1/Core/Types/ToolDefinition.cs` - Removed `[JsonProperty("required")]`

## Code Changes Summary
- **Lines modified:** 1 (line 30-34)
- **Files affected:** 1
- **Breaking changes:** None (backward compatible)
- **Compilation errors:** 0

## Next Steps

1. **Build the project** (already passes compilation)
2. **Run the extension** in debug mode
3. **Send a test message** using the ContinueVS UI
4. **Expected result:** DeepSeek should process tools correctly now

## Verification

You should see in the logs:
```
[MessengerService.ProcessLlmStreamAsync] Starting OpenAI-compatible stream...
[gap55_1-tool-schema] Converted tool 'read_file' with 1 parameters
[gap71-messenger-openai-tools] Populated request.tools with N schemas
[Response status code: 200 (200)]
Stream started successfully...
[DONE] marker received
```

No more "Invalid schema" errors!

## Technical Details

This fix ensures compliance with:
- **OpenAI Function Calling API** specification
- **JSON Schema** standard (draft 7+)
- **DeepSeek API** requirements

The same schema format is used by:
- OpenAI (ChatGPT API)
- Anthropic Claude
- Google Gemini
- LLaMA 2/3 (vLLM)
- Ollama (compatible mode)

## Related Code
- `MessengerService.ConvertToolsToSchema()` - Generates schemas (line 1015-1079)
- `ParametersSchema` class - Defines schema structure
- `ParameterDefinition` class - Defines parameter properties (now fixed)
- Tool definitions - `BuiltInTools.cs` (all use `IsRequired` correctly)
