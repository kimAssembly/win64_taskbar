# LaptopBar per-user installer. Windows PowerShell 5.1 / PowerShell 7, no admin required.
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'LaptopBar'),
    [string]$ArchivePath,
    [switch]$NoStartup,
    [switch]$NoStart,
    [switch]$SkipRegistration
)
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitOperatingSystem) { throw 'LaptopBar requires 64-bit Windows.' }
$InstallDir = [IO.Path]::GetFullPath($InstallDir)
if ($InstallDir.TrimEnd('\') -eq [IO.Path]::GetPathRoot($InstallDir).TrimEnd('\')) { throw 'The install folder must not be a drive root.' }
if ((Test-Path -LiteralPath $InstallDir) -and (Get-ChildItem -LiteralPath $InstallDir -Force | Select-Object -First 1) -and -not (Test-Path -LiteralPath (Join-Path $InstallDir 'LaptopBar.exe'))) { throw 'The destination contains unrelated files. Choose an empty folder.' }
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('LaptopBar-install-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    if (-not $ArchivePath) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $headers = @{ 'User-Agent' = 'LaptopBar-installer' }
        $release = Invoke-RestMethod 'https://api.github.com/repos/kimAssembly/win64_taskbar/releases/latest' -Headers $headers
        $asset = @($release.assets | Where-Object name -eq 'LaptopBar-win-x64.zip')
        $checksum = @($release.assets | Where-Object name -eq 'SHA256SUMS.txt')
        if ($asset.Count -ne 1 -or $checksum.Count -ne 1) { throw 'This release is missing the Windows package or checksum.' }
        $ArchivePath = Join-Path $tempRoot 'LaptopBar-win-x64.zip'
        $checksumPath = Join-Path $tempRoot 'SHA256SUMS.txt'
        Write-Host ('Downloading LaptopBar ' + $release.tag_name + '...')
        Invoke-WebRequest $asset[0].browser_download_url -OutFile $ArchivePath -UseBasicParsing
        Invoke-WebRequest $checksum[0].browser_download_url -OutFile $checksumPath -UseBasicParsing
        $expected = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split '\s+')[0]
        $actual = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash
        if ($expected -notmatch '^[0-9a-fA-F]{64}$' -or $actual -ne $expected) { throw 'Package SHA256 mismatch.' }
    }
    $ArchivePath = (Resolve-Path -LiteralPath $ArchivePath).Path
    $stage = Join-Path $tempRoot 'stage'
    # Validate ZIP paths before extraction; only staged files are ever copied into the install folder.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($entry in $archive.Entries) {
            $entryPath = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
            if (-not $entryPath.StartsWith($stage.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe ZIP entry.' }
        }
    } finally { $archive.Dispose() }
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $stage
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'LaptopBar.exe'))) { throw 'LaptopBar.exe is missing from the package.' }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    $target = Join-Path $InstallDir 'LaptopBar.exe'
    # Updates only stop processes from this exact installation, never unrelated programs.
    Get-Process -Name LaptopBar -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target } | Stop-Process
    $savedSettings = Join-Path $InstallDir 'settings.json'
    foreach ($item in Get-ChildItem -LiteralPath $stage -Force) {
        if ($item.Name -notin @('settings.json', 'verification')) { Copy-Item -LiteralPath $item.FullName -Destination $InstallDir -Recurse -Force }
    }
    if (-not $SkipRegistration) {
        $programs = [Environment]::GetFolderPath('Programs')
        $shortcutPath = Join-Path $programs 'LaptopBar.lnk'
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $target
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.IconLocation = "$target,0"
        $shortcut.Description = 'LaptopBar desktop status bar'
        $shortcut.Save()
        if (-not $NoStartup) {
            $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
            New-Item -Path $runKey -Force | Out-Null
            New-ItemProperty -Path $runKey -Name LaptopBar -Value ('"' + $target + '"') -PropertyType String -Force | Out-Null
        }
        Write-Host 'Start menu search: LaptopBar'
    }
    Write-Host ('Installed to: ' + $InstallDir)
    if (-not $NoStart) { Start-Process -FilePath $target -WindowStyle Normal }
} finally {
    # Only remove this invocation's validated temporary folder.
    $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\LaptopBar-install-'
    if ($resolvedTemp.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedTemp)) { Remove-Item -LiteralPath $resolvedTemp -Recurse -Force }
}
