<#
.SYNOPSIS
	Sends a minimal chat completion request to the DeepSeek API for each model name and reports the HTTP result.

.DESCRIPTION
	Reads the API key from the DEEPSEEK_API_KEY environment variable. If it is not set, prompts for it
	(input is masked and not stored). The key is never printed.

.EXAMPLE
	.\tools\Test-DeepSeekModels.ps1

.EXAMPLE
	.\tools\Test-DeepSeekModels.ps1 -Models deepseek-flash, deepseek-v4-pro
#>
[CmdletBinding()]
param(
	[string]$BaseUrl = 'https://api.deepseek.com',
	[string[]]$Models = @('deepseek-flash', 'deepseek-v4-pro', 'deepseek-v4.1-flash-1017'),
	[int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 6) {
	[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}
Add-Type -AssemblyName System.Net.Http

$apiKey = $env:DEEPSEEK_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
	$secure = Read-Host 'DeepSeek API key' -AsSecureString
	$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
	try {
		$apiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
	}
	finally {
		[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
	}
}

$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $apiKey)
$url = $BaseUrl.TrimEnd('/') + '/v1/chat/completions'

$results = foreach ($model in $Models) {
	$payload = @{
		model      = $model
		messages   = @(@{ role = 'user'; content = 'Reply with the single word OK.' })
		stream     = $false
		max_tokens = 8
	} | ConvertTo-Json -Depth 5

	$content = New-Object System.Net.Http.StringContent($payload, [System.Text.Encoding]::UTF8, 'application/json')
	$status = 0
	$detail = ''
	try {
		$response = $client.PostAsync($url, $content).GetAwaiter().GetResult()
		$status = [int]$response.StatusCode
		$detail = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
	}
	catch {
		$detail = $_.Exception.Message
	}
	finally {
		$content.Dispose()
	}

	if ($detail.Length -gt 300) {
		$detail = $detail.Substring(0, 300) + '...'
	}

	Write-Host ("{0,-28} HTTP {1}" -f $model, $status) -ForegroundColor $(if ($status -eq 200) { 'Green' } else { 'Red' })

	[pscustomobject]@{
		Model  = $model
		Status = $status
		Detail = $detail
	}
}

$client.Dispose()

Write-Host ''
$results | Format-Table Model, Status, Detail -Wrap
