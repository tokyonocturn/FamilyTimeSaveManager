param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root

try {
    Write-Host "== Tests ==" -ForegroundColor Cyan
    dotnet test "tests/FamilyTimeSaveManager.Core.Tests/FamilyTimeSaveManager.Core.Tests.csproj" `
        -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

    Write-Host "== Publish ==" -ForegroundColor Cyan
    $publish = Join-Path $root "artifacts/publish"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish "src/FamilyTimeSaveManager.App/FamilyTimeSaveManager.App.csproj" `
        -c $Configuration -r win-x64 --self-contained true -p:Version=$Version -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

    Write-Host "== Portable package ==" -ForegroundColor Cyan
    $stage = Join-Path $root "artifacts/stage"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item (Join-Path $publish "FamilyTimeSaveManager.exe") $stage
    Copy-Item "README.md", "LICENSE", "CHANGELOG.md" $stage

    $zip = Join-Path $root "artifacts/FamilyTimeSaveManager-$Version-portable-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal

    Write-Host "== Installer ==" -ForegroundColor Cyan
    $iscc = Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 6/ISCC.exe"
    if (-not (Test-Path $iscc)) {
        $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    }
    if (-not (Test-Path $iscc)) {
        throw "Inno Setup 6 (ISCC.exe) was not found. Install it from https://jrsoftware.org/isinfo.php or skip packaging."
    }

    & $iscc "/DAppVersion=$Version" (Join-Path $root "installer/FamilyTimeSaveManager.iss")
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

    Write-Host "== Checksums ==" -ForegroundColor Cyan
    $checksumFile = Join-Path $root "artifacts/SHA256SUMS.txt"
    if (Test-Path $checksumFile) { Remove-Item $checksumFile -Force }
    $files = @(
        $zip,
        (Join-Path $root "artifacts/FamilyTimeSaveManager-$Version-setup-win-x64.exe")
    )
    foreach ($file in $files) {
        if (-not (Test-Path $file)) { throw "Expected artefact missing: $file" }
        $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
        "{0}  {1}" -f $hash, (Split-Path -Leaf $file) | Add-Content $checksumFile
    }

    Write-Host "== Done ==" -ForegroundColor Green
    Get-ChildItem (Join-Path $root "artifacts") -File |
        Where-Object { $_.Name -match $Version -or $_.Name -eq "SHA256SUMS.txt" } |
        Select-Object Name, Length |
        Format-Table -AutoSize
    Get-Content $checksumFile
}
finally {
    Pop-Location
}
