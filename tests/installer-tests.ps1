$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('LaptopBar-installer-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
$installer = [scriptblock]::Create((Get-Content (Join-Path $PSScriptRoot '../install.ps1') -Raw))
try {
    $package = Join-Path $root 'package'
    $destination = Join-Path $root 'installed'
    New-Item -ItemType Directory $package | Out-Null
    Set-Content (Join-Path $package 'LaptopBar.exe') 'installer-test-only'
    Set-Content (Join-Path $package 'settings.json') '{"source":"package"}'
    $zip = Join-Path $root 'test.zip'
    Compress-Archive -Path "$package/*" -DestinationPath $zip
    & $installer -ArchivePath $zip -InstallDir $destination -SkipRegistration -NoStart
    if (-not (Test-Path (Join-Path $destination 'LaptopBar.exe'))) { throw 'Fresh install failed' }
    if (Test-Path (Join-Path $destination 'settings.json')) { throw 'Package preferences must not be installed' }
    Set-Content (Join-Path $destination 'settings.json') '{"user":"preserved"}'
    & $installer -ArchivePath $zip -InstallDir $destination -SkipRegistration -NoStart
    if ((Get-Content (Join-Path $destination 'settings.json') -Raw) -notmatch 'preserved') { throw 'User settings lost' }
    $unrelated = Join-Path $root 'unrelated'
    New-Item -ItemType Directory $unrelated | Out-Null
    Set-Content (Join-Path $unrelated 'keep.txt') 'keep'
    $rejected = $false
    try { & $installer -ArchivePath $zip -InstallDir $unrelated -SkipRegistration -NoStart } catch { $rejected = $true }
    if (-not $rejected) { throw 'Unrelated destination was not rejected' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $unsafe = Join-Path $root 'unsafe.zip'
    $archive = [IO.Compression.ZipFile]::Open($unsafe,[IO.Compression.ZipArchiveMode]::Create)
    try { $null = $archive.CreateEntry('../escape.txt') } finally { $archive.Dispose() }
    $rejected = $false
    try { & $installer -ArchivePath $unsafe -InstallDir (Join-Path $root 'unsafe-destination') -SkipRegistration -NoStart } catch { $rejected = $true }
    if (-not $rejected) { throw 'Unsafe archive was not rejected' }
    Write-Output 'PASS: fresh install; update preserves user settings; unrelated folder rejected; ZIP traversal rejected.'
} finally {
    $prefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\LaptopBar-installer-test-'
    if ([IO.Path]::GetFullPath($root).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $root -Recurse -Force }
}
