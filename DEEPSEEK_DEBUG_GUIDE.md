# DeepSeek API Connection Debug Guide

## Overview
Enhanced logging has been added to `MessengerService.cs` to help debug your DeepSeek API connection issues. The logging will now capture detailed information about:
- HTTP request/response details
- Headers and status codes
- Stream reading progress
- Error details with stack traces

## Your Current Configuration
Based on `~\.continueVS\continueVS.json`:
```json
{
  "baseUrl": "https://api.deepseek.com",
  "provider": "openai",
  "apiKey": "sk-2b613a1fb52d4c72b142ad4577c4722e",
  "ollamaModelId": "openai/deepseek-v4-flash-0731-nvfp4",
  "name": "deepseek-flash"
}
```

## Expected Behavior
When you send a message, the logs should show:
1. ✅ Model loaded successfully
2. ✅ Request URL: `https://api.deepseek.com/v1/chat/completions`
3. ✅ Authorization header sent
4. ✅ HTTP 200 response
5. ✅ Stream data received with `data: {...}` lines
6. ✅ `[DONE]` marker at end

## How to Check Logs

### Option 1: Debug Output Window (VS 2022)
1. Build the project
2. Run the extension in debug mode
3. Open **Debug > Windows > Output**
4. Select **ContinueVS** from the dropdown
5. Send a message in the ContinueVS UI
6. Look for lines starting with:
   - `[MessengerService.ProcessLlmStreamAsync]` - Initial setup
   - `==== DEEPSEEK DEBUG START ====` - Request details
   - `==== DEEPSEEK DEBUG END ====` - Response/error details

### Option 2: File Logs
Logs are also written to file. Check:
- `%TEMP%\ContinueVS\logs\` (if configured)
- Or VS Output pane logs

## What to Look for

### ✅ Success Scenario
```
[MessengerService.ProcessLlmStreamAsync] Model selected: deepseek-flash (Id:f9eb46ba-cf5a-4a4c-9e0e-3fbd59665a8c, Provider:openai, BaseUrl:https://api.deepseek.com)
[gap64-init] ==== DEEPSEEK DEBUG START ====
[gap64-init] Request URL: https://api.deepseek.com/v1/chat/completions
[gap64-init] Authorization header: Bearer sk-2b613...
[gap64-init] Response status code: 200 (200)
[gap64-init] Received line 1: data: {"choices":[{"delta":{"content":"Hello"}}]}
[gap64-init] Stream terminated with [DONE] marker after 42 lines
[gap64-init] ==== DEEPSEEK DEBUG END (SUCCESS) ====
```

### ❌ Common Failure Scenarios

#### 1. **Authentication Error (401)**
```
ERROR - HTTP 401: {"error":{"message":"Unauthorized","type":"invalid_request_error"}}
```
**Fix:** Check your API key in `continueVS.json`

#### 2. **Connection Timeout**
```
TaskCanceledException: The operation was canceled
```
**Fix:** 
- Verify DeepSeek API is accessible: `curl https://api.deepseek.com/v1/chat/completions -H "Authorization: Bearer YOUR_KEY"`
- Check firewall/proxy settings
- Increase timeout if network is slow

#### 3. **Model Not Found (404)**
```
ERROR - HTTP 404: {"error":{"message":"The model 'deepseek-v4-flash-0731-nvfp4' does not exist"}}
```
**Fix:** Update the model name in config to match available DeepSeek models

#### 4. **Empty Stream (No Response)**
```
Stream read complete. Total lines: 0
```
**Fix:** 
- API key may be invalid
- Model may be in rate limit
- Check DeepSeek account status

#### 5. **Non-SSE Response**
```
Skipping non-data line: This is not a valid SSE line
```
**Fix:** Server is not sending proper Server-Sent Events format

## Manual Testing

### Test DeepSeek Connectivity with curl
```powershell
$headers = @{
    "Authorization" = "Bearer sk-2b613a1fb52d4c72b142ad4577c4722e"
    "Content-Type" = "application/json"
}

$body = @{
    model = "deepseek-v4-flash-0731-nvfp4"
    messages = @(
        @{ role = "user"; content = "Hello" }
    )
    stream = $true
} | ConvertTo-Json

curl.exe -X POST https://api.deepseek.com/v1/chat/completions `
  -H "Authorization: Bearer sk-2b613a1fb52d4c72b142ad4577c4722e" `
  -H "Content-Type: application/json" `
  -d ($body)
```

## Enhanced Logging Details

### Request Phase Logs
- `Request URL` - Full endpoint being called
- `Request Method` - Should be POST
- `Authorization header` - First 10 chars of API key (masked for security)
- `Request headers count` - Total headers sent
- `Request content type` - Should be application/json

### Response Phase Logs
- `Response status code` - HTTP status (200 for success)
- `Content-Type` - Response format
- `Content-Length` - Response size
- Full error body for non-2xx responses

### Stream Phase Logs
- `Total lines` - Lines read from stream
- `Data lines` - Lines starting with "data:"
- `Empty lines` - Blank lines (normal)
- `Non-data lines` - Other metadata lines

## Code Changes Made

**File:** `src\VSIXProject1\Services\Implementations\MessengerService.cs`

### Changes:
1. **ProcessOpenAiStreamAsync() - HTTP Request Section**
   - Added detailed logging before sending request
   - Logs all request headers
   - Logs all response headers
   - Captures full error body on failure

2. **Response Stream Reading**
   - Tracks line counts (total, data, empty, non-data)
   - Logs first 3 lines in full detail
   - Suppresses verbose logging after line 4 for performance
   - Reports statistics at end of stream

3. **Exception Handling**
   - Logs exception type and message
   - Logs stack trace for unexpected errors
   - Marks each section with `==== DEEPSEEK DEBUG START/END ====` for easy identification

## Next Steps

1. **Build the project** (already done)
2. **Run the extension in debug mode** (F5)
3. **Send a test message** in the ContinueVS UI
4. **Check the Output window** for `==== DEEPSEEK DEBUG START ====` section
5. **Share the debug logs** if you need help troubleshooting

## Potential Issues to Check

### 1. API Key Format
- Must start with `sk-`
- Should be valid and not expired
- Verify in DeepSeek dashboard

### 2. URL Configuration
- Currently: `https://api.deepseek.com`
- Port 443 is automatic (HTTPS)
- Full endpoint: `https://api.deepseek.com/v1/chat/completions`

### 3. Model Name
- Currently: `deepseek-v4-flash-0731-nvfp4`
- May need to update to available models:
  - `deepseek-chat`
  - `deepseek-coder`
  - etc.

### 4. Rate Limiting
- DeepSeek has rate limits per account
- Check your account dashboard for quota

## Support

If you still have issues after checking these logs:
1. Enable all debug logs in the Output window
2. Try the curl command to verify DeepSeek API works directly
3. Check firewall/proxy settings
4. Verify API key hasn't been revoked
