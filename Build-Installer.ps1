param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$toolsPath = Join-Path $PSScriptRoot 'obj/vpk'
$vpk = Join-Path $toolsPath 'vpk.exe'
$output = Join-Path $PSScriptRoot "bin/DisplayDeck/$Version"
$publish = Join-Path $output 'publish'
$releases = Join-Path $output 'releases'
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }

if (-not (Test-Path -LiteralPath $vpk)) {
    dotnet tool install vpk --tool-path $toolsPath --version 1.2.161
    if ($LASTEXITCODE -ne 0) { throw 'Velopack CLI installation failed.' }
}

dotnet publish (Join-Path $PSScriptRoot 'DisplayManagerClone.csproj') -c Release -r win-x64 --self-contained -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw 'DisplayDeck publish failed.' }

& $vpk pack --packId DisplayDeck --packTitle DisplayDeck --packVersion $Version --runtime win-x64 --packDir $publish --mainExe DisplayManagerClone.exe --outputDir $releases
if ($LASTEXITCODE -ne 0) { throw 'DisplayDeck packaging failed.' }
