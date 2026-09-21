param([string]$BuildDirectory='staging')
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot $BuildDirectory
$release=Join-Path $PSScriptRoot 'release'
$payload=Join-Path $release ('package-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$files=@('QingJie.exe','QingJie.exe.config','qingjie.ico','Tesseract.dll','x64\leptonica-1.82.0.dll','x64\tesseract50.dll','tessdata\eng.traineddata')
foreach($name in $files){$target=Join-Path $payload $name;New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $source $name) -Destination $target}
foreach($name in @('README.md','install.ps1','THIRD-PARTY-NOTICES.md','licenses')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $payload -Recurse}
$zip=Join-Path $release 'QingJie-Windows-x64.zip'
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
Get-FileHash -LiteralPath $zip
