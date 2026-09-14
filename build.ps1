$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$webDriver = Join-Path $projectRoot 'vendor\WebDriver.dll'
$chromeDriver = Join-Path $projectRoot 'vendor\chromedriver.exe'
$source = Join-Path $projectRoot 'src\Program.cs'
$updaterSource = Join-Path $projectRoot 'src\Updater.cs'
$outputDir = Join-Path $projectRoot 'dist\AKMasterSocical-Clean'
$outputExe = Join-Path $outputDir 'AKMasterSocical.exe'
$updaterExe = Join-Path $outputDir 'Updater.exe'
$icon = Join-Path $projectRoot 'app.ico'

foreach ($required in @($compiler, $webDriver, $chromeDriver, $source, $updaterSource, $icon)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Thiếu file cần thiết: $required" }
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /debug- /win32icon:$icon /out:$outputExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:$webDriver $source
if ($LASTEXITCODE -ne 0) { throw "Biên dịch thất bại với mã $LASTEXITCODE" }

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /debug- /win32icon:$icon /out:$updaterExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll $updaterSource
if ($LASTEXITCODE -ne 0) { throw "Biên dịch updater thất bại với mã $LASTEXITCODE" }

Copy-Item -LiteralPath $webDriver -Destination (Join-Path $outputDir 'WebDriver.dll') -Force
Copy-Item -LiteralPath $chromeDriver -Destination (Join-Path $outputDir 'chromedriver.exe') -Force
Copy-Item -LiteralPath $icon -Destination (Join-Path $outputDir 'app.ico') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $outputDir 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'SECURITY.md') -Destination (Join-Path $outputDir 'SECURITY.md') -Force

Get-ChildItem -LiteralPath $outputDir -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Get-FileHash -Algorithm SHA256 | ForEach-Object {
    "{0}  {1}" -f $_.Hash, (Split-Path -Leaf $_.Path)
} | Set-Content -LiteralPath (Join-Path $outputDir 'SHA256SUMS.txt') -Encoding ASCII

Write-Host "Đã tạo bản sạch tại: $outputDir"
