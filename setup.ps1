# setup.ps1 — JKW Monitor first-time setup
# Run from the repo root after cloning.
# 1. Publishes the app to ./Publish
# 2. Registers Claude Code hooks in %USERPROFILE%\.claude\settings.json

#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$repoRoot        = $PSScriptRoot
$hookScriptPath  = (Join-Path $repoRoot "hooks\jkw-monitor.ps1").Replace('\', '/')
$hookCommand     = "powershell -NoProfile -File $hookScriptPath"
$publishDir      = Join-Path $repoRoot "Publish"

# ─── 1. Publish ───────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Publishing JKW Monitor ===" -ForegroundColor Cyan

dotnet publish -c Release -r win-x64 --no-self-contained -o $publishDir -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish failed." -ForegroundColor Red
    exit 1
}

Write-Host "Published to: $publishDir" -ForegroundColor Green

# ─── 2. Claude Code hooks ─────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Configuring Claude Code hooks ===" -ForegroundColor Cyan

$settingsPath = Join-Path $env:USERPROFILE ".claude\settings.json"
$settingsDir  = Split-Path $settingsPath

if (-not (Test-Path $settingsDir)) {
    New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
}

if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
} else {
    $settings = [PSCustomObject]@{}
}

# Ensure top-level "hooks" object
if (-not ($settings.PSObject.Properties.Name -contains "hooks")) {
    $settings | Add-Member -NotePropertyName "hooks" -NotePropertyValue ([PSCustomObject]@{})
}

$hookEvents = @("PreToolUse", "PostToolUse", "Notification", "Stop", "SubagentStop")
$added      = @()

foreach ($event in $hookEvents) {
    # Ensure the event array exists
    if (-not ($settings.hooks.PSObject.Properties.Name -contains $event)) {
        $settings.hooks | Add-Member -NotePropertyName $event -NotePropertyValue @()
    }

    # Check whether our command is already registered
    $alreadyExists = $false
    foreach ($entry in @($settings.hooks.$event)) {
        foreach ($h in @($entry.hooks)) {
            if ($h.command -eq $hookCommand) {
                $alreadyExists = $true
                break
            }
        }
        if ($alreadyExists) { break }
    }

    if ($alreadyExists) {
        Write-Host "  [$event] already configured — skipped." -ForegroundColor DarkGray
        continue
    }

    $newEntry = [PSCustomObject]@{
        matcher = ""
        hooks   = @(
            [PSCustomObject]@{
                type    = "command"
                command = $hookCommand
                timeout = 3
            }
        )
    }

    $settings.hooks.$event = @($settings.hooks.$event) + $newEntry
    $added += $event
    Write-Host "  [$event] hook added." -ForegroundColor Green
}

$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8

Write-Host ""
if ($added.Count -gt 0) {
    Write-Host "Hooks registered: $($added -join ', ')" -ForegroundColor Green
} else {
    Write-Host "All hooks were already configured — nothing changed." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
Write-Host "Run Publish\JKWMonitor.exe to start the monitor before using Claude Code." -ForegroundColor White
Write-Host ""
