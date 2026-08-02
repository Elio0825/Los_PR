[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PromeRotationDir,

    [Parameter(Mandatory = $true)]
    [string]$DalamudHooksDir,

    [string]$Version = "0.1.4",

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "Los.csproj"
$testsPath = Join-Path $projectRoot "Tests\Los.Tests.csproj"
$rotationPath = Join-Path $projectRoot "BLM\BlackMageRotation.cs"
$promeAssembly = Join-Path $PromeRotationDir "PromeRotation.dll"

$parsedVersion = $null
if (-not [Version]::TryParse($Version, [ref]$parsedVersion)) {
    throw "Version must be a valid System.Version value: $Version"
}

if (-not (Test-Path -LiteralPath $promeAssembly)) {
    throw "PromeRotation.dll was not found: $promeAssembly"
}

if (-not (Test-Path -LiteralPath $DalamudHooksDir)) {
    throw "Dalamud Hooks directory was not found: $DalamudHooksDir"
}

$rotationSource = Get-Content -LiteralPath $rotationPath -Raw -Encoding UTF8
if ($rotationSource -notmatch ('"' + [Regex]::Escape($Version) + '"')) {
    throw "RotationMetadata version does not match release version: $Version"
}

if ($rotationSource -notmatch 'ContentScope\s*=\s*AcrContentScope\.All') {
    throw "RotationMetadata must declare AcrContentScope.All"
}

$commonProperties = @(
    "-p:PromeRotationDir=$PromeRotationDir",
    "-p:DalamudHooksDir=$DalamudHooksDir",
    "-p:TreatWarningsAsErrors=true"
)

& dotnet build $projectPath -c Release @commonProperties
if ($LASTEXITCODE -ne 0) {
    throw "Los Release build failed"
}

& dotnet build $testsPath -c Release @commonProperties
if ($LASTEXITCODE -ne 0) {
    throw "Los.Tests Release build failed"
}

& dotnet run --project $testsPath -c Release --no-build @commonProperties
if ($LASTEXITCODE -ne 0) {
    throw "Los.Tests failed"
}

$buildDirectory = Join-Path $projectRoot "bin\Release\net10.0-windows"
$dllPath = Join-Path $buildDirectory "Los.dll"
$depsPath = Join-Path $buildDirectory "Los.deps.json"
if (-not (Test-Path -LiteralPath $dllPath) -or -not (Test-Path -LiteralPath $depsPath)) {
    throw "Release output must contain Los.dll and Los.deps.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = "artifacts\release\v$Version"
}

if (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot $OutputDirectory
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$zipPath = Join-Path $OutputDirectory "Los.zip"
$manifestPath = Join-Path $OutputDirectory "repo.json"

Compress-Archive -LiteralPath @($dllPath, $depsPath) -DestinationPath $zipPath -Force
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$referencePromeVersion = [Reflection.AssemblyName]::GetAssemblyName($promeAssembly).Version.ToString()
$description = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String("5LiA5Liq5YWo562J57qn6buR6a2UQUNSIOaUr+aMgeaXpemajy/pq5jpmr4="))

$manifest = [ordered]@{
    author = "Los"
    version = $Version
    description = $description
    supportedJobs = @(
        [ordered]@{
            job = "BLM"
            contentScope = "All"
        }
    )
    apiVersion = 15
    referencePromeVersion = $referencePromeVersion
    downloadUrl = "https://github.com/Elio0825/Los_PR/releases/latest/download/Los.zip"
    sourceRepositoryUrl = "https://github.com/Elio0825/Los_PR"
    sha256 = $sha256
}

$json = $manifest | ConvertTo-Json -Depth 8
$utf8NoBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($manifestPath, $json, $utf8NoBom)

Write-Host "Release archive: $zipPath"
Write-Host "Manifest: $manifestPath"
Write-Host "SHA-256: $sha256"
