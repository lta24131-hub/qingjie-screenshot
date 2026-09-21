param([switch]$Test,[string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Path $output -Force | Out-Null
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Runtime.WindowsRuntime.dll','System.Runtime.InteropServices.WindowsRuntime.dll','System.Runtime.dll','System.ObjectModel.dll','System.Threading.Tasks.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF\' + $_)) }
$refs += '/r:' + (Join-Path $framework 'Microsoft.CSharp.dll')
$refs += @('Windows.Foundation.winmd','Windows.Globalization.winmd','Windows.Media.winmd','Windows.Graphics.winmd','Windows.Storage.winmd') | ForEach-Object { '/r:' + (Join-Path $env:WINDIR ('System32\WinMetadata\' + $_)) }
$sources = Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName
$ocr = Join-Path $root 'vendor\tesseract'
if (-not (Test-Path (Join-Path $ocr 'lib\net48\Tesseract.dll'))) { throw 'Run setup-ocr.ps1 first.' }
Copy-Item (Join-Path $ocr 'lib\net48\Tesseract.dll') $output -Force
foreach ($folder in @('x64','tessdata')) { New-Item -ItemType Directory -Path (Join-Path $output $folder) -Force | Out-Null }
Copy-Item (Join-Path $ocr 'x64\*.dll') (Join-Path $output 'x64') -Force
Copy-Item (Join-Path $root 'vendor\tessdata\eng.traineddata') (Join-Path $output 'tessdata') -Force
$refs += '/r:' + (Join-Path $output 'Tesseract.dll')
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $output 'BuildIcon.exe')) @refs (Join-Path $root 'assets\BuildIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon builder compilation failed.' }
& (Join-Path $output 'BuildIcon.exe') $output
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /utf8output ('/win32manifest:' + (Join-Path $root 'app.manifest')) ('/win32icon:' + (Join-Path $output 'qingjie.ico')) ('/resource:' + (Join-Path $output 'qingjie-icon.png') + ',QingJie.Icon.png') ('/out:' + (Join-Path $output 'QingJie.exe')) @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item (Join-Path $root 'App.config') (Join-Path $output 'QingJie.exe.config') -Force
if ($Test) {
  & (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /utf8output ('/out:' + (Join-Path $output 'QingJie.Tests.exe')) @refs ('/r:' + (Join-Path $output 'QingJie.exe')) (Join-Path $root 'tests\Tests.cs')
  if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
  & (Join-Path $output 'QingJie.Tests.exe')
  if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
  & (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /utf8output ('/win32manifest:' + (Join-Path $root 'app.manifest')) ('/out:' + (Join-Path $output 'QingJie.Preview.exe')) @refs ('/r:' + (Join-Path $output 'QingJie.exe')) (Join-Path $root 'tests\VisualSmoke.cs')
  if ($LASTEXITCODE -ne 0) { throw 'UI test host compilation failed.' }
  & (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /utf8output ('/out:' + (Join-Path $output 'QingJie.MemoryProbe.exe')) @refs ('/r:' + (Join-Path $output 'QingJie.exe')) (Join-Path $root 'tests\MemoryProbe.cs')
  if ($LASTEXITCODE -ne 0) { throw 'Memory probe compilation failed.' }
}
Get-ChildItem $output -Filter 'QingJie.exe*' | Select-Object Name,Length
