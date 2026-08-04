<#
.SYNOPSIS
  installer/phase.md (M6, SPEC §8): release packaging + signing pipeline for MeticulousResearch.

.DESCRIPTION
  Produces a single signed MSIX installer artifact from a Release build:
    1. Publishes the WPF app (self-contained x64) so the .NET runtime ships inside the package.
    2. Stages the bundled Agent SDK sidecar single-file binary (SPEC §7.2) next to the app so the
       primary generation path works after install with no separate install step.
    3. Stages the app-branding-icon asset (Assets/AppIcon.ico) referenced by the manifest.
    4. Packs the staged layout + installer/AppxManifest.xml into an .msix.
    5. Authenticode-signs the executable and the .msix with a timestamped trusted certificate so
       SmartScreen shows a verified publisher.

  Signing runs in the release pipeline, never on dev machines: the certificate is supplied out of
  band (an Azure Trusted Signing / HSM-backed code-signing cert). Pass its thumbprint via
  -CertThumbprint or the CODESIGN_THUMBPRINT environment variable; the private key never lives in
  the repo.

  The package version is single-sourced from installer/version.props (MeticulousVersion) and flows
  into both the app assembly (imported by the App .csproj) and the manifest, so the installer, the
  exe, and the About screen always report the same version.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$CertThumbprint = $env:CODESIGN_THUMBPRINT,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$OutputDir = (Join-Path $PSScriptRoot "artifacts")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$appProj  = Join-Path $repoRoot "src/MeticulousResearch.App/MeticulousResearch.App.csproj"
$stageDir = Join-Path $OutputDir "stage"

# --- Single-source version ---------------------------------------------------------------------
[xml]$versionXml = Get-Content (Join-Path $PSScriptRoot "version.props")
$version = ($versionXml.Project.PropertyGroup.MeticulousVersion | Select-Object -First 1).Trim()
Write-Host "Packaging MeticulousResearch Desktop $version ($Configuration/$Runtime)"

# --- 1. Publish the app (self-contained: .NET runtime ships in the package) ---------------------
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
dotnet publish $appProj -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false -o $stageDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# --- 2. Stage the bundled Agent SDK sidecar (SPEC §7.2) -----------------------------------------
$sidecar = Join-Path $repoRoot "scantool"
if (Test-Path $sidecar) {
    Copy-Item $sidecar (Join-Path $stageDir "sidecar") -Recurse -Force
} else {
    Write-Warning "Sidecar payload not found at $sidecar — the release build must stage it before packing."
}

# --- 3. Stage the branding icon + manifest ------------------------------------------------------
Copy-Item (Join-Path $repoRoot "src/MeticulousResearch.App/Assets/AppIcon.ico") `
          (Join-Path $stageDir "Assets/AppIcon.ico") -Force
$manifest = Join-Path $stageDir "AppxManifest.xml"
Copy-Item (Join-Path $PSScriptRoot "AppxManifest.xml") $manifest -Force

# --- 4. Pack the MSIX ---------------------------------------------------------------------------
$msix = Join-Path $OutputDir "MeticulousResearch-$version.msix"
& makeappx.exe pack /d $stageDir /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

# --- 5. Authenticode-sign the exe and the package (timestamped, trusted cert) -------------------
if ([string]::IsNullOrWhiteSpace($CertThumbprint)) {
    Write-Warning "No code-signing cert thumbprint provided — artifact is UNSIGNED. Set CODESIGN_THUMBPRINT in the release pipeline."
} else {
    & signtool.exe sign /sha1 $CertThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 `
        (Join-Path $stageDir "MeticulousResearch.App.exe")
    if ($LASTEXITCODE -ne 0) { throw "signtool (exe) failed" }
    & signtool.exe sign /sha1 $CertThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool (msix) failed" }
}

Write-Host "Installer artifact: $msix"
