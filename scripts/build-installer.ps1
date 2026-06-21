$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\win-x64"
$distDir = Join-Path $root "dist"
$issFile = Join-Path $root "installer\InScreenSetup.iss"

Write-Host "Publishing WinAnchor (Release, win-x64, self-contained)..." -ForegroundColor Cyan
Push-Location $root
try {
    dotnet publish "InScreenApp.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $exePath = Join-Path $publishDir "WinAnchor.exe"
    if (-not (Test-Path $exePath)) {
        throw "Published executable not found at $exePath"
    }

    Write-Host "Published: $exePath" -ForegroundColor Green

    $innoCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )

    $iscc = $innoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($null -eq $iscc) {
        Write-Host ""
        Write-Host "Inno Setup was not found. Portable EXE is ready at:" -ForegroundColor Yellow
        Write-Host "  $exePath"
        Write-Host ""
        Write-Host "Install Inno Setup 6, then rerun this script to build WinAnchor-Setup.exe" -ForegroundColor Yellow
        exit 0
    }

    if (-not (Test-Path $distDir)) {
        New-Item -ItemType Directory -Path $distDir | Out-Null
    }

    Write-Host "Building Windows installer..." -ForegroundColor Cyan
    & $iscc $issFile

    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compiler failed with exit code $LASTEXITCODE"
    }

    $installer = Get-ChildItem -Path $distDir -Filter "WinAnchor-Setup-*.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $installer) {
        throw "Installer executable was not created in $distDir"
    }

    Write-Host ""
    Write-Host "Installer created:" -ForegroundColor Green
    Write-Host "  $($installer.FullName)"
}
finally {
    Pop-Location
}
