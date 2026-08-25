# Connects the local SayTo repository to GitHub and pushes it.
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\publish-github.ps1
#         (asks for the repository URL on first run, remembers it afterwards)

$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Write-Host ""
Write-Host "=== SayTo -> GitHub publisher ===" -ForegroundColor Cyan
Write-Host ""

# ---- repo url (ask once, remember for next time) ----
$urlFile = Join-Path $env:LOCALAPPDATA "SayTo\github-repo.txt"
$url = $null
if (Test-Path $urlFile) { $url = (Get-Content $urlFile -ErrorAction SilentlyContinue | Select-Object -First 1) }

if ([string]::IsNullOrWhiteSpace($url)) {
    Write-Host "Example: https://github.com/USERNAME/SayTo.git" -ForegroundColor Yellow
    $url = Read-Host "Enter your GitHub repository URL"
    if ([string]::IsNullOrWhiteSpace($url)) { Write-Host "No URL given." -ForegroundColor Red; exit 1 }
    New-Item -ItemType Directory -Force -Path (Split-Path $urlFile) | Out-Null
    Set-Content -Path $urlFile -Value $url
}
Write-Host "Repository: $url" -ForegroundColor Gray

# ---- git operations ----
# NOTE: git writes progress messages to stderr. Windows PowerShell turns any
# redirected stderr into a terminating error when $ErrorActionPreference is
# 'Stop', so keep it 'Continue' around git calls and check $LASTEXITCODE
# ourselves instead.
$ErrorActionPreference = "Continue"

# add or update the 'origin' remote (no error when it does not exist yet)
$existing = @(& git remote)
if ($existing -contains "origin") {
    & git remote set-url origin $url | Out-Null
} else {
    & git remote add origin $url | Out-Null
}

Write-Host ""
Write-Host "==> Pushing to GitHub (a sign-in window may open the first time)..." -ForegroundColor Cyan
& git push -u origin main
$pushExit = $LASTEXITCODE
$ErrorActionPreference = "Stop"

# ---- result ----
if ($pushExit -eq 0) {
    Write-Host ""
    Write-Host "Done! Your project is now online:" -ForegroundColor Green
    Write-Host ($url -replace "\.git$", "") -ForegroundColor Green
    Write-Host ""
    Write-Host "Next step: create a Release and upload dist\SayTo-*-portable-x64.zip"
    Write-Host "(full instructions: docs\راهنمای-انتشار-در-گیت‌هاب.md)"
    exit 0
} else {
    Write-Host ""
    Write-Host "Push failed (exit code $pushExit)." -ForegroundColor Yellow
    Write-Host "- If it asked for credentials or nothing happened:" -ForegroundColor Yellow
    Write-Host "  Control Panel > User Accounts > Credential Manager > Windows Credentials" -ForegroundColor Yellow
    Write-Host "  delete the 'git:https://github.com' entry, then run this script again." -ForegroundColor Yellow
    Write-Host "- If it says 'Repository not found': check the URL and that the repo exists." -ForegroundColor Yellow
    exit 1
}
