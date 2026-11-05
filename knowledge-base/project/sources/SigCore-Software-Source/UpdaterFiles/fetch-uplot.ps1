# One-time development step to vendor uPlot 1.6.32 within the device website source.
# Run this on a computer with internet access, before publishing a release.
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot 'www\vendor\uplot'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$base = 'https://raw.githubusercontent.com/leeoniya/uPlot/1.6.32/dist/'
foreach ($name in @('uPlot.iife.min.js', 'uPlot.min.css')) {
    Invoke-WebRequest -Uri ($base + $name) -OutFile (Join-Path $destination $name)
    Write-Host "Downloaded $name"
}
Write-Host 'Vendor files ready for the normal website build and deployment.'
