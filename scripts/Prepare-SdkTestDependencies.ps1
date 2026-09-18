[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("CN", "TC")]
    [string]$ClientRegion,

    [Parameter(Mandatory = $true)]
    [string]$SdkReferenceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ECommonsAssembly,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# These SDKs contain executable managed assemblies. Keep the test fixture away
# from both game installations and release packages.
$sdkAssemblies = @(Get-ChildItem -LiteralPath $SdkReferenceDirectory -Filter '*.dll' -File)
if ($sdkAssemblies.Count -eq 0 -or
    -not (Test-Path -LiteralPath (Join-Path $SdkReferenceDirectory 'PromeRotation.dll'))) {
    throw "The resolved SDK directory contains no PromeRotation test dependency."
}
foreach ($assembly in $sdkAssemblies) {
    Copy-Item -LiteralPath $assembly.FullName -Destination $OutputDirectory -Force
}
Copy-Item -LiteralPath $ECommonsAssembly -Destination $OutputDirectory -Force

function Expand-PinnedTestAssembly {
    param([string]$Package, [string]$Version, [string]$Assembly, [string]$ExpectedHash)

    $packageUrl = "https://api.nuget.org/v3-flatcontainer/$Package/$Version/$Package.$Version.nupkg"
    $packagePath = Join-Path $OutputDirectory "$Package.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        Invoke-WebRequest -Uri $packageUrl -OutFile $packagePath
    }
    $packageBytes = [IO.File]::ReadAllBytes($packagePath)
    $actualHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData($packageBytes))
    if ($actualHash -cne $ExpectedHash) {
        throw "The $Package test dependency failed pinned NuGet SHA-512 verification."
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entry = $archive.GetEntry("lib/net8.0/$Assembly")
        if ($null -eq $entry) {
            throw "The pinned $Package package does not contain its net8.0 assembly."
        }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $OutputDirectory $Assembly), $true)
    } finally {
        $archive.Dispose()
    }
}

# Hashes are pinned to the official NuGet catalog packageHash values. These are
# test-only fixtures; product compilation still uses the unmodified SDK.
$serilogPath = Join-Path $OutputDirectory 'Serilog.dll'
if (-not (Test-Path -LiteralPath $serilogPath)) {
    Expand-PinnedTestAssembly -Package 'serilog' -Version '4.0.0' -Assembly 'Serilog.dll' `
        -ExpectedHash 'H2J5vPqTypwTGA3Xx+WUTVNRNYuc06Ngy78S8t2lCHAMuf+t7/1N/ncqbdssAp4sNHPmVaO2YKZE3lHWcTMmjA=='
}

$luminaPath = Join-Path $OutputDirectory 'Lumina.dll'
if ($ClientRegion -eq 'TC' -and
    [Reflection.AssemblyName]::GetAssemblyName($luminaPath).Version -eq [Version]'0.0.0.0') {
    # TC Lumina.Excel explicitly references Lumina 6.0.0.0, but the SDK ships
    # Lumina 0.0.0.0. Supply compatible managed test methods from Lumina 6.7.0.
    Expand-PinnedTestAssembly -Package 'lumina' -Version '6.7.0' -Assembly 'Lumina.dll' `
        -ExpectedHash '8+/r7sQjG6n9N851o8Arz8xXc/agXsTbiD0VpPCw+bkEeqRHslqq4lG/er65r+sVII4cuEYg0Pj0RaJVLZAIew=='
    if ([Reflection.AssemblyName]::GetAssemblyName($luminaPath).Version -ne [Version]'6.0.0.0') {
        throw "The TC Lumina test dependency does not match Lumina.Excel's referenced assembly version."
    }
}

Write-Output $OutputDirectory
