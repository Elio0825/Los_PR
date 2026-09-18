[CmdletBinding()]
param(
    [string]$PromeRotationDir = "",

    [string]$DalamudHooksDir = "",

    [string]$Version = "1.0.0",

    [string]$OutputDirectory = "",

    [ValidateSet("CN", "TC")]
    [string]$ClientRegion = "CN",

    [switch]$SkipTests,

    [switch]$SdkOfflineTests,

    [string]$SourceCommit = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "Los.csproj"
$testsPath = Join-Path $projectRoot "Tests\Los.Tests.csproj"
$rotationPath = Join-Path $projectRoot "BLM\BlackMageRotation.cs"
$ClientRegion = $ClientRegion.ToUpperInvariant()
$releaseProfile = if ($ClientRegion -eq "TC") {
    @{
        AssemblyName = "Los.TC"
        TargetFramework = "net9.0-windows"
        Author = "Los-TC"
        ApiVersion = 13
        Repository = "Elio0825/Los_PR-TC"
        SdkPackage = "promerotation.sdk.tc"
    }
} else {
    @{
        AssemblyName = "Los"
        TargetFramework = "net10.0-windows"
        Author = "Los"
        ApiVersion = 15
        Repository = "Elio0825/Los_PR"
        SdkPackage = "promerotation.sdk.api15"
    }
}

$parsedVersion = $null
if (-not [Version]::TryParse($Version, [ref]$parsedVersion)) {
    throw "Version must be a valid System.Version value: $Version"
}

if ($SkipTests -and $SdkOfflineTests) {
    throw "SkipTests and SdkOfflineTests are mutually exclusive."
}
if ($SdkOfflineTests -and
    (-not [string]::IsNullOrWhiteSpace($PromeRotationDir) -or
     -not [string]::IsNullOrWhiteSpace($DalamudHooksDir))) {
    throw "SdkOfflineTests resolves its own isolated dependencies; do not pass runtime directories."
}
if (-not $SkipTests -and -not $SdkOfflineTests) {
    if ([string]::IsNullOrWhiteSpace($PromeRotationDir) -or
        [string]::IsNullOrWhiteSpace($DalamudHooksDir)) {
        throw "Tests require the matching region's runtime PromeRotationDir and DalamudHooksDir. Use -SkipTests explicitly for SDK-only packaging."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PromeRotationDir "PromeRotation.dll")) -or
        -not (Test-Path -LiteralPath (Join-Path $DalamudHooksDir "Dalamud.dll"))) {
        throw "The runtime PromeRotation.dll or Dalamud.dll required by tests was not found."
    }
}

$rotationSource = Get-Content -LiteralPath $rotationPath -Raw -Encoding UTF8
if ($rotationSource -notmatch ('(?s)\[RotationMetadata\(\s*25u,\s*[^,]+,\s*[^,]+,\s*"' + [Regex]::Escape($Version) + '"\s*,')) {
    throw "RotationMetadata version does not match release version: $Version"
}

if ($rotationSource -notmatch 'ContentScope\s*=\s*AcrContentScope\.All') {
    throw "RotationMetadata must declare AcrContentScope.All"
}

$commonProperties = @(
    "-p:ClientRegion=$ClientRegion",
    "-p:Version=$Version",
    "-p:TreatWarningsAsErrors=true"
)
if (-not [string]::IsNullOrWhiteSpace($PromeRotationDir)) {
    $commonProperties += "-p:PromeRotationDir=$PromeRotationDir"
}
if (-not [string]::IsNullOrWhiteSpace($DalamudHooksDir)) {
    $commonProperties += "-p:DalamudHooksDir=$DalamudHooksDir"
}

& dotnet build $projectPath -c Release @commonProperties
if ($LASTEXITCODE -ne 0) {
    throw "Los Release build failed"
}

function Get-ReleaseBuildInfo {
    $buildInfoJson = & dotnet msbuild $projectPath -nologo -verbosity:quiet `
        -p:Configuration=Release @commonProperties `
        -target:ResolveReferences `
        -getProperty:AssemblyName,TargetFramework,TargetPath,ProjectAssetsFile `
        -getItem:ReferencePath
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve the compiled project's release metadata."
    }
    return ($buildInfoJson -join [Environment]::NewLine) | ConvertFrom-Json
}

$resolvedBuild = Get-ReleaseBuildInfo
if ($resolvedBuild.Properties.AssemblyName -ne $releaseProfile.AssemblyName -or
    $resolvedBuild.Properties.TargetFramework -ne $releaseProfile.TargetFramework) {
    throw "Build identity does not match the $ClientRegion release profile."
}

$promeReferences = @($resolvedBuild.Items.ReferencePath | Where-Object {
    [IO.Path]::GetFileName($_.Identity) -eq "PromeRotation.dll"
})
if ($promeReferences.Count -ne 1) {
    throw "Expected exactly one resolved PromeRotation.dll compiler reference."
}
$promeAssembly = $promeReferences[0].Identity
$referencePromeVersion = [Reflection.AssemblyName]::GetAssemblyName($promeAssembly).Version.ToString()
$assets = Get-Content -LiteralPath $resolvedBuild.Properties.ProjectAssetsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$sdkLibraries = @($assets.libraries.PSObject.Properties | Where-Object {
    $_.Name -like "PromeRotation.SDK.*/*"
})
if ($sdkLibraries.Count -ne 1 -or
    $sdkLibraries[0].Name.Split('/')[0] -ine $releaseProfile.SdkPackage) {
    throw "Restored SDK does not match the $ClientRegion release profile."
}

$runtimePromeVersion = $null
if ($SkipTests) {
    Write-Warning "Runtime tests were explicitly skipped. SDK compilation does not verify in-game compatibility."
} else {
    if ($SdkOfflineTests) {
        $eCommonsReference = @($resolvedBuild.Items.ReferencePath | Where-Object {
            [IO.Path]::GetFileName($_.Identity) -eq "ECommons.dll"
        })
        if ($eCommonsReference.Count -ne 1) {
            throw "Expected one ECommons compiler reference for isolated tests."
        }
        $sdkVersion = $sdkLibraries[0].Name.Split('/')[1]
        $testDependencies = & (Join-Path $PSScriptRoot "Prepare-SdkTestDependencies.ps1") `
            -ClientRegion $ClientRegion `
            -SdkReferenceDirectory (Split-Path -Parent $promeAssembly) `
            -ECommonsAssembly $eCommonsReference[0].Identity `
            -OutputDirectory (Join-Path $projectRoot "artifacts\test-dependencies\$ClientRegion\$sdkVersion")
        $PromeRotationDir = $testDependencies
        $DalamudHooksDir = $testDependencies
        $commonProperties += @(
            "-p:PromeRotationDir=$testDependencies",
            "-p:DalamudHooksDir=$testDependencies",
            "-p:TestRuntimeSupportDir=$testDependencies"
        )
        Write-Host "Running SDK offline tests; this does not substitute for in-game validation."
    }

    $runtimePromeVersion = [Reflection.AssemblyName]::GetAssemblyName(
        (Join-Path $PromeRotationDir "PromeRotation.dll")).Version.ToString()
    if ([Version]$runtimePromeVersion -lt [Version]$referencePromeVersion) {
        throw "Test runtime PR $runtimePromeVersion is older than compiled PR reference $referencePromeVersion."
    }
    $runtimeDalamudVersion = [Reflection.AssemblyName]::GetAssemblyName(
        (Join-Path $DalamudHooksDir "Dalamud.dll")).Version
    if ($runtimeDalamudVersion.Major -ne $releaseProfile.ApiVersion) {
        throw "Test runtime Dalamud $runtimeDalamudVersion does not match $ClientRegion API $($releaseProfile.ApiVersion)."
    }

    & dotnet build $testsPath -c Release @commonProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Los.Tests Release build failed"
    }

    & dotnet run --project $testsPath -c Release --no-build @commonProperties
    if ($LASTEXITCODE -ne 0) {
        throw "Los.Tests failed"
    }
}

$dllPath = $resolvedBuild.Properties.TargetPath
$depsPath = [IO.Path]::ChangeExtension($dllPath, ".deps.json")
if (-not (Test-Path -LiteralPath $dllPath) -or -not (Test-Path -LiteralPath $depsPath)) {
    throw "Release output must contain $($releaseProfile.AssemblyName).dll and its .deps.json"
}
$assemblyIdentity = [Reflection.AssemblyName]::GetAssemblyName($dllPath)
if ($assemblyIdentity.Name -ne $releaseProfile.AssemblyName -or
    $assemblyIdentity.Version -ne [Version]::new($parsedVersion.Major, $parsedVersion.Minor,
        [Math]::Max(0, $parsedVersion.Build), [Math]::Max(0, $parsedVersion.Revision))) {
    throw "Compiled assembly identity or version does not match release metadata."
}

$actualSourceCommit = & git -C $projectRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve the checked out source commit."
}
if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    $SourceCommit = $actualSourceCommit
}
if ($SourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw "SourceCommit must be a full 40-character Git commit SHA."
}
if ($SourceCommit -ne $actualSourceCommit) {
    throw "SourceCommit does not match the current checkout."
}
$sourceStatus = & git -C $projectRoot status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect the source working tree."
}
$sourceDirty = -not [string]::IsNullOrWhiteSpace(($sourceStatus -join [Environment]::NewLine))

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = if ($ClientRegion -eq "CN") {
        "artifacts\release\v$Version"
    } else {
        "artifacts\release\TC\v$Version"
    }
}

if (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot $OutputDirectory
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$zipPath = Join-Path $OutputDirectory "$($releaseProfile.AssemblyName).zip"
$manifestPath = Join-Path $OutputDirectory "repo.json"
$buildInfoPath = Join-Path $OutputDirectory "build-info.json"

Compress-Archive -LiteralPath @($dllPath, $depsPath) -DestinationPath $zipPath -Force
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $expectedEntries = @([IO.Path]::GetFileName($dllPath), [IO.Path]::GetFileName($depsPath))
    $actualEntries = @($archive.Entries | ForEach-Object { $_.FullName })
    if ($actualEntries.Count -ne 2 -or (Compare-Object $expectedEntries $actualEntries)) {
        throw "Release archive must contain only the selected region's DLL and .deps.json."
    }
} finally {
    $archive.Dispose()
}
$description = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String("5LiA5Liq5YWo562J57qn6buR6a2UQUNSIOaUr+aMgeaXpemajy/pq5jpmr4="))

$manifest = [ordered]@{
    author = $releaseProfile.Author
    version = $Version
    description = $description
    supportedJobs = @(
        [ordered]@{
            job = "BLM"
            contentScope = "All"
        }
    )
    apiVersion = $releaseProfile.ApiVersion
    referencePromeVersion = $referencePromeVersion
    downloadUrl = "https://github.com/$($releaseProfile.Repository)/releases/latest/download/$($releaseProfile.AssemblyName).zip"
    sourceRepositoryUrl = "https://github.com/Elio0825/Los_PR"
    sha256 = $sha256
}

$json = $manifest | ConvertTo-Json -Depth 8
$utf8NoBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($manifestPath, $json, $utf8NoBom)

$buildInfo = [ordered]@{
    clientRegion = $ClientRegion
    version = $Version
    assemblyName = $releaseProfile.AssemblyName
    targetFramework = $releaseProfile.TargetFramework
    sdk = $sdkLibraries[0].Name
    referencePromeVersion = $referencePromeVersion
    sourceRepositoryUrl = $manifest.sourceRepositoryUrl
    sourceCommit = $SourceCommit.ToLowerInvariant()
    sourceDirty = $sourceDirty
    testsExecuted = -not $SkipTests.IsPresent
    testEnvironment = if ($SkipTests) { "skipped" } elseif ($SdkOfflineTests) { "sdk-offline" } else { "local-runtime" }
    testRuntimePromeVersion = $runtimePromeVersion
    sha256 = $sha256
}
[IO.File]::WriteAllText($buildInfoPath, ($buildInfo | ConvertTo-Json -Depth 8), $utf8NoBom)

$verifiedManifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($verifiedManifest.author -ne $releaseProfile.Author -or
    $verifiedManifest.apiVersion -ne $releaseProfile.ApiVersion -or
    $verifiedManifest.sha256 -ne (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()) {
    throw "Release manifest failed identity or checksum verification."
}

Write-Host "Release archive: $zipPath"
Write-Host "Manifest: $manifestPath"
Write-Host "Build provenance: $buildInfoPath"
Write-Host "Region: $ClientRegion; PR reference: $referencePromeVersion; source: $SourceCommit"
Write-Host "SHA-256: $sha256"
