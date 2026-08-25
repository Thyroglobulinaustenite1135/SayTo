# Connects the local SayTo repository to GitHub and pushes it.
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\publish-github.ps1
#         (asks for the repository URL on first run)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Write-Host ""
Write-Host "=== SayTo -> GitHub publisher ===" -ForegroundColor Cyan
Write-Host ""

# repo url (ask once, remember for next time)
$urlFile = Join-Path $env:LOCALAPPDATA "SayTo\github-repo.txt"
$url = $null
if (Test-Path $urlFile) { $url = (Get-Content $urlFile -ErrorAction SilentlyContinue | Select-Object -First 1) }

if ([string]::IsNullOrWhiteSpace($url)) {
    Write-Host "Example: https://github.com/USERNAME/SayTo.git" -ForegroundColor Yellow
    $url = Read-Host "Enter your GitHub repository URL"
    if ([string]::IsNullOrWhiteSpace($url)) { throw "No URL given." }
    New-Item -ItemType Directory -Force -Path (Split-Path $urlFile) | Out-Null
    Set-Content -Path $urlFile -Value $url
}
Write-Host "Repository: $url" -ForegroundColor Gray

# remote
git remote remove origin 2>$null
git remote add origin $url

# push
Write-Host ""
Write-Host "==> Pushing to GitHub (a sign-in window may open the first time)..." -ForegroundColor Cyan
git push -u origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Done! Your project is now online:" -ForegroundColor Green
    Write-Host ($url -replace "\.git$", "") -ForegroundColor Green
    Write-Host ""
    Write-Host "Next step: create a Release and upload dist\SayTo-*-portable-x64.zip"
    Write-Host "(full instructions: docs\راهنمای-انتشار-در-گیت‌هاب.md)"
} else {
    Write-Host ""
    Write-Host "Push failed. Common fix: remove the saved GitHub credential:" -ForegroundColor Yellow
    Write-Host "  Control Panel > User Accounts > Credential Manager > Windows Credentials" -ForegroundColor Yellow
    Write-Host "delete the 'git:https://github.com' entry, then run this script again." -ForegroundColor Yellow
}
