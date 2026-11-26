param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Notes,

    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$Repo = "PanuphongBunkum/LangFix"
$Root = $PSScriptRoot
$OutputsDir = Join-Path (Split-Path $Root -Parent) "outputs"
$InstallerPath = Join-Path $OutputsDir "LangFix-Setup-$Version.exe"
$ManifestPath = Join-Path $OutputsDir "langfix-version.json"

Push-Location $Root
try {
    # 1. Update version in LangFix.csproj and installer.iss
    (Get-Content LangFix.csproj -Raw) -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>" |
        Set-Content LangFix.csproj -NoNewline -Encoding utf8
    (Get-Content installer.iss -Raw) -replace '#define MyAppVersion "[^"]+"', "#define MyAppVersion `"$Version`"" |
        Set-Content installer.iss -NoNewline -Encoding utf8

    # 2. Build and run self-test
    dotnet build -c Release -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
    dotnet run -c Release --no-build -- --self-test
    if ($LASTEXITCODE -ne 0) { throw "self-test failed (exit $LASTEXITCODE)" }

    # 3. Publish single-file app and build installer
    dotnet publish -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o publish -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    $iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $iscc)) { throw "Inno Setup 6 not found at $iscc" }
    & $iscc /Q installer.iss
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

    $hash = (Get-FileHash $InstallerPath -Algorithm SHA256).Hash
    Write-Host "Built $InstallerPath (SHA-256: $hash)"

    # 4. Generate langfix-version.json for auto-update checks
    $manifest = [ordered]@{
        version      = $Version
        downloadUrl  = "https://github.com/$Repo/releases/tag/v$Version"
        releaseNotes = $Notes
    } | ConvertTo-Json -Depth 3
    Set-Content -Path $ManifestPath -Value $manifest -Encoding utf8

    if (-not $SkipPublish) {
        $releaseBody = @"
$Notes

### ไฟล์ติดตั้ง
- ``LangFix-Setup-$Version.exe``
- SHA-256: ``$hash``
"@
        gh release create "v$Version" $InstallerPath $ManifestPath `
            --repo $Repo `
            --title "LangFix $Version" `
            --notes $releaseBody
        if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
        Write-Host "Published https://github.com/$Repo/releases/tag/v$Version"
    }
}
finally {
    Pop-Location
}
