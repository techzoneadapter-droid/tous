$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $projectRoot 'dist\AKMasterSocical-Clean'
$releaseDir = Join-Path $projectRoot 'dist\release'
$zip = Join-Path $releaseDir 'AKMasterSocical-Clean.zip'
$hashFile = Join-Path $releaseDir 'AKMasterSocical-Clean.zip.sha256'
$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

& (Join-Path $projectRoot 'build.ps1')
if (-not $iscc) { throw 'Không tìm thấy Inno Setup Compiler.' }
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Get-ChildItem -LiteralPath $releaseDir -Filter 'AKMasterSocical-Setup-*.exe' -File -ErrorAction SilentlyContinue | Remove-Item -Force
Compress-Archive -LiteralPath $dist -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
"$hash  AKMasterSocical-Clean.zip" | Set-Content -LiteralPath $hashFile -Encoding ASCII
& $iscc (Join-Path $projectRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw "Tạo installer thất bại với mã $LASTEXITCODE" }
Get-ChildItem -LiteralPath $releaseDir -File | Select-Object Name, Length
