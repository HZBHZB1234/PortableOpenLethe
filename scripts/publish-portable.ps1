#requires -Version 7
<#
  Publishes the portable server layout:
    dist/portable/
      OpenLethe.Server(.exe) + DLLs
      static/       - runtime static data (never embedded)
      modfiles/     - launcher-served client DLLs (copy your own)
      plugins/      - admin-installed DLL plugins
      data/         - SQLite DB, admin secret, logs (created on first run)
#>
param(
    [string]$Configuration = 'Release',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist\portable')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\OpenLethe.Server\OpenLethe.Server.csproj'
$out = [IO.Path]::GetFullPath($OutDir)

Write-Host "Publishing to $out"
& dotnet publish $project -c $Configuration -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

foreach ($dir in @('modfiles', 'plugins', 'data')) {
    $p = Join-Path $out $dir
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p | Out-Null }
}

Write-Host @"
Done. Start the server from $out.
  - http://127.0.0.1:5055/health  -> ok
  - http://127.0.0.1:5055/admin/  -> local admin panel
  - Point your launcher at this address; set Lethe.cfg ServerRoot accordingly.
"@
