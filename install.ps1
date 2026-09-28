param([string]$BuildDirectory = '', [switch]$NoStartup)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($BuildDirectory)) { $BuildDirectory = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'staging\QingJie.exe')) { 'staging' } else { '.' } }
$source = Join-Path $PSScriptRoot $BuildDirectory
$destination = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\QingJie'
$upgrading = Test-Path -LiteralPath (Join-Path $destination 'QingJie.exe')
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'Luma.lnk'
$legacyStartup = Join-Path ([Environment]::GetFolderPath('Startup')) '轻截.lnk'
# Keep an existing registration name, including Windows' StartupApproved state.
if (-not (Test-Path -LiteralPath $startup) -and (Test-Path -LiteralPath $legacyStartup)) { $startup = $legacyStartup }
$hadStartup = Test-Path -LiteralPath $startup
if (-not (Test-Path -LiteralPath (Join-Path $source 'QingJie.exe'))) { throw 'Build QingJie first.' }
if (Get-Process -Name QingJie -ErrorAction SilentlyContinue) { throw 'QingJie is running. Finish your screenshot and exit from the tray before installing. Nothing has been changed.' }
$shell = New-Object -ComObject WScript.Shell
$desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Luma.lnk'
$legacyDesktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '轻截.lnk'
foreach ($linkPath in @($desktop,$startup)) {
    if (Test-Path -LiteralPath $linkPath) {
        $existingLink = $shell.CreateShortcut($linkPath)
        if ($existingLink.TargetPath -ne (Join-Path $destination 'QingJie.exe') -and $existingLink.TargetPath -ne (Join-Path $source 'QingJie.exe')) { throw ('An unrelated shortcut has the same name; nothing was changed: ' + $linkPath) }
    }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$files = @('QingJie.exe','QingJie.exe.config','qingjie.ico','Tesseract.dll','x64\leptonica-1.82.0.dll','x64\tesseract50.dll','tessdata\eng.traineddata')
foreach ($name in $files) { if (-not (Test-Path -LiteralPath (Join-Path $source $name))) { throw ('Missing build dependency: ' + $name) } }
if (Test-Path -LiteralPath (Join-Path $destination 'QingJie.exe')) {
    $backup = Join-Path $destination ('backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    # Back up changed runtime files only; unchanged OCR models/DLLs need no duplicate.
    foreach ($name in $files) { $existing = Join-Path $destination $name; if ((Test-Path -LiteralPath $existing) -and (Get-FileHash -LiteralPath $existing).Hash -ne (Get-FileHash -LiteralPath (Join-Path $source $name)).Hash) { $backupFile=Join-Path $backup $name; New-Item -ItemType Directory -Path (Split-Path $backupFile) -Force | Out-Null; Copy-Item -LiteralPath $existing -Destination $backupFile } }
}
foreach ($name in $files) { $target=Join-Path $destination $name; New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null; Copy-Item -LiteralPath (Join-Path $source $name) -Destination $target -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $destination '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $destination -Recurse -Force
# Transparent lossless NTFS compression. Unsupported volumes still work uncompressed.
$compact = Join-Path $env:WINDIR 'System32\compact.exe'
if (Test-Path -LiteralPath $compact) {
    $compressTargets = $files | ForEach-Object { Join-Path $destination $_ }
    & $compact /C /I /Q @compressTargets | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning 'Disk compression was unavailable; installed files remain usable.' }
}
if (-not (Test-Path -LiteralPath $desktop) -and (Test-Path -LiteralPath $legacyDesktop)) {
    $legacyLink = $shell.CreateShortcut($legacyDesktop)
    if ($legacyLink.TargetPath -eq (Join-Path $destination 'QingJie.exe') -or $legacyLink.TargetPath -eq (Join-Path $source 'QingJie.exe')) { Move-Item -LiteralPath $legacyDesktop -Destination $desktop }
}
$shortcut = $shell.CreateShortcut($desktop)
$shortcut.TargetPath = Join-Path $destination 'QingJie.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Description = 'F1 截图、图片原位翻译，F3 贴图找回'
$shortcut.IconLocation = (Join-Path $destination 'qingjie.ico') + ',0'
$shortcut.Save()
if (-not $NoStartup -and (-not $upgrading -or $hadStartup)) {
    $shortcut = $shell.CreateShortcut($startup)
    if ($hadStartup -and $shortcut.TargetPath -ne (Join-Path $destination 'QingJie.exe') -and $shortcut.TargetPath -ne (Join-Path $source 'QingJie.exe')) { throw 'An unrelated startup shortcut has the same name; it was not changed.' }
    $shortcut.TargetPath = Join-Path $destination 'QingJie.exe'
    $shortcut.Arguments = '--background'
    $shortcut.WorkingDirectory = $destination
    $shortcut.Description = '登录后恢复 Luma 贴图'
    $shortcut.IconLocation = (Join-Path $destination 'qingjie.ico') + ',0'
    $shortcut.Save()
}
Write-Output ('Installed: ' + $destination)
Write-Output 'No app was stopped or restarted. Open the desktop shortcut when ready.'
