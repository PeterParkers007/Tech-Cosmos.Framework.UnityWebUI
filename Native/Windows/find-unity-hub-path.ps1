$path = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
if (-not (Test-Path -LiteralPath $path)) { exit 1 }
$root = (Get-Content -LiteralPath $path -Encoding UTF8 -Raw).Trim().Trim('"')
if ([string]::IsNullOrWhiteSpace($root)) { exit 1 }
Write-Output $root
