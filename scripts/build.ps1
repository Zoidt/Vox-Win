[CmdletBinding()]
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$voxRoot = Split-Path -Parent $PSScriptRoot
Push-Location $voxRoot
try {
    if (-not $SkipTests) {
        dotnet test tests/Vox.Core.Tests/Vox.Core.Tests.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Vox tests failed.' }
    }
    dotnet publish src/Vox.App/Vox.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/Vox --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Vox publish failed.' }
    Copy-Item -LiteralPath README.md, THIRD_PARTY_NOTICES.md -Destination artifacts/publish/Vox
    Compress-Archive -Path artifacts/publish/Vox -DestinationPath artifacts/Vox-win-x64.zip -Force
    Write-Output "Ready: $voxRoot\artifacts\publish\Vox\Vox.exe"
    Write-Output "Archive: $voxRoot\artifacts\Vox-win-x64.zip"
}
finally { Pop-Location }
