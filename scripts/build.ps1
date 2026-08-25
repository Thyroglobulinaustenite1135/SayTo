# Builds the portable single-folder release of SayTo into dist\SayTo
# and packages it as a zip ready for a GitHub release.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root "dist\SayTo"

Write-Host "==> Publishing SayTo ($Configuration, win-x64, self-contained)..." -ForegroundColor Cyan

dotnet publish (Join-Path $root "src\SayTo\SayTo.csproj") `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $dist

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$version = (Select-Xml -Path (Join-Path $root "src\SayTo\SayTo.csproj") -XPath "//Version").Node.InnerText
$zip = Join-Path $root "dist\SayTo-$version-portable-x64.zip"

Write-Host "==> Packaging $zip ..." -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$dist\*" -DestinationPath $zip -CompressionLevel Optimal

$exe = Get-Item (Join-Path $dist "SayTo.exe")
Write-Host ""
Write-Host ("Done.  {0}  ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green
Write-Host ("Exe:   {0}  ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)) -ForegroundColor Green
Write-Host "Note : speech models download on first run (or pre-place them in dist\SayTo\models)."
