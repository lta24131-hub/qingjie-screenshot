$ErrorActionPreference = 'Stop'
$vendor = Join-Path $PSScriptRoot 'vendor'
New-Item -ItemType Directory -Path $vendor -Force | Out-Null
$env:NO_PROXY = ''
$package = Join-Path $vendor 'tesseract.5.2.0.zip'
if (-not (Test-Path -LiteralPath $package)) {
    curl.exe -L --fail --max-time 90 'https://api.nuget.org/v3-flatcontainer/tesseract/5.2.0/tesseract.5.2.0.nupkg' -o $package
    if ($LASTEXITCODE -ne 0) { throw 'OCR package download failed.' }
}
if ((Get-FileHash -LiteralPath $package).Hash -ne '202D82FC7C7D8384DF7DA57206D5E1F456CCDABD648C46E67CDFAA3A911D4795') { throw 'OCR package checksum mismatch.' }
Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $vendor 'tesseract') -Force
$data = Join-Path $vendor 'tessdata'
New-Item -ItemType Directory -Path $data -Force | Out-Null
curl.exe -L --fail --max-time 90 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata' -o (Join-Path $data 'eng.traineddata')
if ($LASTEXITCODE -ne 0) { throw 'English OCR model download failed.' }
if ((Get-FileHash -LiteralPath (Join-Path $data 'eng.traineddata')).Hash -ne '7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2') { throw 'English model checksum mismatch.' }
Get-ChildItem -LiteralPath $vendor -Recurse -File | Select-Object FullName,Length
