#!/usr/bin/env pwsh
# PostToolUse hook: format C# files after Edit/Write/MultiEdit.
# Reads the Claude Code hook payload from stdin, runs `dotnet format` scoped to the touched file when it is a .cs file.
# Failures are non-blocking — the hook prints a warning and exits 0 so edits are not rolled back.

param()

$ErrorActionPreference = 'Continue'

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

    $payload = $raw | ConvertFrom-Json -ErrorAction Stop
    $filePath = $payload.tool_input.file_path
    if (-not $filePath) { exit 0 }
    if ([System.IO.Path]::GetExtension($filePath) -ne '.cs') { exit 0 }

    $repoRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
    $solution = Join-Path $repoRoot 'BlitzBridge.slnx'
    if (-not (Test-Path $solution)) { exit 0 }

    Push-Location $repoRoot
    try {
        & dotnet format $solution --include $filePath --no-restore --verbosity quiet 2>&1 | Out-Null
    } finally {
        Pop-Location
    }
}
catch {
    Write-Warning "format-csharp hook: $($_.Exception.Message)"
}

exit 0
