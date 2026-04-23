param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ComposeFile = "docker-compose.yml"
)

$ErrorActionPreference = "Stop"

function Invoke-Compose {
    param([string[]]$ComposeArgs)
    Push-Location $ProjectPath
    try {
        docker compose -f $ComposeFile @ComposeArgs
    }
    finally {
        Pop-Location
    }
}

Write-Host "Validating compose config..."
Invoke-Compose @("config", "-q")

Write-Host "Starting fresh stack..."
Invoke-Compose @("down", "--volumes", "--remove-orphans")
Invoke-Compose @("up", "--build", "-d")

Write-Host "Waiting for sql-init completion..."
$maxAttempts = 120
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    $status = (Invoke-Compose @("ps", "--format", "json", "sql-init") | ConvertFrom-Json).State
    if ($status -eq "exited") {
        break
    }

    Start-Sleep -Seconds 2
    if ($attempt -eq $maxAttempts) {
        throw "sql-init did not complete in time."
    }
}

$sqlInitExit = (Invoke-Compose @("ps", "--format", "json", "sql-init") | ConvertFrom-Json)."ExitCode"
if ($sqlInitExit -ne 0) {
    throw "sql-init failed with exit code $sqlInitExit."
}

Write-Host "Waiting for Blitz Bridge container..."
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    $bridgeState = (Invoke-Compose @("ps", "--format", "json", "blitzbridge") | ConvertFrom-Json).State
    if ($bridgeState -eq "running") {
        break
    }

    Start-Sleep -Seconds 2
    if ($attempt -eq $maxAttempts) {
        throw "blitzbridge did not reach running state."
    }
}

$envPath = Join-Path $ProjectPath ".env"
if (-not (Test-Path $envPath)) {
    throw "Missing .env at $envPath. Copy from .env.example first."
}

$tokenLine = Get-Content $envPath | Where-Object { $_ -match "^BLITZ_BRIDGE_TOKEN=" } | Select-Object -First 1
if (-not $tokenLine) {
    throw "BLITZ_BRIDGE_TOKEN is missing from .env."
}

$token = ($tokenLine -split "=", 2)[1].Trim('"')
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "BLITZ_BRIDGE_TOKEN is empty in .env."
}

$headers = @{
    "Accept"               = "application/json, text/event-stream"
    "Content-Type"         = "application/json"
    "MCP-Protocol-Version" = "2025-11-25"
    "Authorization"        = "Bearer $token"
}

Write-Host "Checking tools/list readiness..."
$toolsResponse = Invoke-RestMethod -Uri "http://localhost:5000/mcp" -Method Post -Headers $headers -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
$tools = @($toolsResponse.result.tools.name)
$requiredTools = @(
    "azure_sql_target_capabilities",
    "azure_sql_blitz_cache",
    "azure_sql_blitz_index",
    "azure_sql_health_check",
    "azure_sql_current_incident"
)

foreach ($requiredTool in $requiredTools) {
    if ($tools -notcontains $requiredTool) {
        throw "tools/list missing required tool: $requiredTool"
    }
}

Write-Host "Checking azure_sql_health_check readiness..."
$healthResponse = Invoke-RestMethod -Uri "http://localhost:5000/mcp" -Method Post -Headers $headers -Body '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"azure_sql_health_check","arguments":{"target":"demo-sql-target","maxRows":5}}}'
if (-not $healthResponse.result) {
    throw "azure_sql_health_check did not return a result payload."
}

Write-Host "Demo verification passed."
