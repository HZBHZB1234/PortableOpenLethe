#requires -Version 7
<#
  Packages the game-derived static resources as a standalone release artifact.

  Output:
    dist/PortableLetheStatic-v<version>.zip
      static-manifest.json
      StaticData/**
      Localize/**

  The server program zip no longer contains these files. Hub downloads this
  artifact as the default bootstrap source when no other static source is set.
#>
param(
    [string]$Version = '0.1.1',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$static = Join-Path $root 'static'
$out = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force $out | Out-Null

$stage = Join-Path ([IO.Path]::GetTempPath()) ("lethe-static-{0}-{1}" -f $Version, [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
try {
    foreach ($dir in @('StaticData', 'Localize')) {
        $src = Join-Path $static $dir
        if (-not (Test-Path $src)) { throw "missing static source: $src" }
        Copy-Item -Recurse -Force $src (Join-Path $stage $dir)
    }

    $files = @{}
    $total = 0
    Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        if ($rel -eq 'static-manifest.json') { return }
        $files[$rel] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $total += $_.Length
    }

    $manifest = [ordered]@{
        schema_version   = 1
        static_version   = $Version
        server_version   = $Version
        source           = 'release-snapshot'
        origin           = 'extracted-from-game-assets'
        generated_at     = (Get-Date).ToUniversalTime().ToString('o')
        file_count       = $files.Count
        total_bytes      = $total
        files            = $files
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $stage 'static-manifest.json') -Encoding UTF8

    $zip = Join-Path $out ("PortableLetheStatic-v{0}.zip" -f $Version)
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "STATIC_ZIP=$zip"
    "SIZE_MB=$([math]::Round((Get-Item $zip).Length / 1MB, 2))"
    "SHA256=$hash"
    "FILES=$($files.Count)"
} finally {
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
}
