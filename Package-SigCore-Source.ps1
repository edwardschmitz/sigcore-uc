$ErrorActionPreference = 'Stop'
$source = 'C:\Users\eddie\OneDrive\Documents\GitHub\sigcore-uc\software'
$output = Join-Path $env:USERPROFILE 'Downloads\SigCore-Software-Source.zip'
$excludedDirs = @('bin','obj','.vs','.git','node_modules','packages','TestResults','dist','build','publish','.idea','.vscode')
$extensions = @('.cs','.xaml','.resx','.csproj','.sln','.slnx','.json','.config','.xml','.props','.targets','.razor','.cshtml','.html','.htm','.css','.js','.ts','.tsx','.jsx','.php','.md','.txt','.sql','.sh','.ps1','.bat','.yml','.yaml','.ini','.iss')
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Source folder not found: $source" }
$files = @(Get-ChildItem -LiteralPath $source -File -Recurse | Where-Object {
    $relative = $_.FullName.Substring($source.Length).TrimStart('\')
    $parts = $relative -split '[\\/]'
    $blocked = @($parts | Select-Object -SkipLast 1 | Where-Object { $excludedDirs -contains $_ })
    $blocked.Count -eq 0 -and $extensions -contains $_.Extension.ToLowerInvariant() -and $_.Length -lt 5MB
})
$total = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host "Selected $($files.Count) files; total $([math]::Round($total / 1MB, 2)) MB."
$answer = Read-Host 'Create ZIP? (Y/N)'
if ($answer -notmatch '^[Yy]$') { exit }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
$zip = [System.IO.Compression.ZipFile]::Open($output, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($source.Length).TrimStart('\') -replace '\\','/'
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $zip.Dispose() }
Write-Host "Created: $output"
Write-Host "ZIP size: $([math]::Round((Get-Item -LiteralPath $output).Length / 1MB, 2)) MB"
