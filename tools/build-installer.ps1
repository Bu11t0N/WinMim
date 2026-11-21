[CmdletBinding()]
param(
    [string]$Wix = 'wix',
    [string]$UiExtension = 'WixToolset.UI.wixext',
    [string]$Output,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$NetFxExtension = 'WixToolset.Netfx.wixext'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$Version) {
    $projectProperties = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)
    $Version = $projectProperties.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid MSI version' }
if (!$Output) { $Output = Join-Path $root "build/installer/WinMim-Preview-$Version-x64.msi" }
$Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
$staging = Join-Path $root ('build/installer/staging-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $staging 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null

function Copy-PayloadFile([string]$Source, [string]$Relative, [string]$Sha256 = '') {
    $target = [IO.Path]::GetFullPath((Join-Path $payload $Relative))
    if (!$target.StartsWith($payload + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Invalid payload path: $Relative"
    }
    if ($Sha256 -and (Get-FileHash -LiteralPath $Source).Hash -ne $Sha256) {
        throw "Payload checksum mismatch: $Relative"
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $target
}

Push-Location -LiteralPath $root
try {
    $app = Join-Path $staging 'app'
    $cli = Join-Path $staging 'cli'
    dotnet publish src/AeroForge.App -c Release --no-self-contained -o $app --nologo
    if ($LASTEXITCODE -ne 0) { throw 'WinMim publish failed' }
    dotnet publish src/AeroForge.Cli -c Release --no-self-contained -o $cli --nologo
    if ($LASTEXITCODE -ne 0) { throw 'WinMim CLI publish failed' }

    foreach ($file in @('WinMim.exe', 'WinMim.dll', 'WinMim.deps.json', 'WinMim.runtimeconfig.json', 'AeroForge.Core.dll')) {
        Copy-PayloadFile (Join-Path $app $file) $file
    }
    foreach ($file in @('AeroForge.Cli.exe', 'AeroForge.Cli.dll', 'AeroForge.Cli.deps.json', 'AeroForge.Cli.runtimeconfig.json', 'AeroForge.Core.dll')) {
        Copy-PayloadFile (Join-Path $cli $file) "cli/$file"
    }
    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'sbom.cdx.json', 'third-party.lock.json',
                        'tools/Set-SevenDesktop.ps1', 'docs/XP-IMPLEMENTATION.md')) {
        Copy-PayloadFile $file $file
    }

    $tracked = git ls-files -- content/catalog.json content/classic95 content/licenses content/xp
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate tracked content' }
    foreach ($file in $tracked) { Copy-PayloadFile $file $file }

    $catalog = Get-Content -LiteralPath content/catalog.json -Raw | ConvertFrom-Json
    $lock = Get-Content -LiteralPath third-party.lock.json -Raw | ConvertFrom-Json -AsHashtable
    $managedFiles = @($catalog.runtimeFiles) + @($catalog.modules | ForEach-Object { $_.binaries })
    foreach ($file in $managedFiles) {
        $path = "content/$($file.path)"
        Copy-PayloadFile $path $path $file.sha256
    }
    foreach ($file in $lock.classicRuntime.files.GetEnumerator()) {
        $path = "content/classic-runtime/retrobar/$($file.Key)"
        Copy-PayloadFile $path $path $file.Value
    }
    $engineSource = "vendor/sources/windhawk-v$($lock.windhawk.version).zip"
    Copy-PayloadFile $engineSource $engineSource $lock.windhawk.sourceArchiveSha256
    $panelSource = 'vendor/sources/retrobar-source.zip'
    Copy-PayloadFile $panelSource $panelSource $lock.classicRuntime.sourceArchiveSha256

    & (Join-Path $payload 'cli/AeroForge.Cli.exe') verify --content (Join-Path $payload 'content')
    if ($LASTEXITCODE -ne 0) { throw 'Staged WinMim catalog verification failed' }

    $licenseRtf = Join-Path $staging 'LICENSE.rtf'
    $licenseText = (Get-Content -LiteralPath LICENSE -Raw).Replace('\', '\\').Replace('{', '\{').Replace('}', '\}')
    $licenseText = $licenseText -replace '\r?\n', '\par '
    $rtf = '{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\fs20 Preview for Windows 10/11 x64. Requires the x64 .NET 10 Desktop Runtime.\par\par ' + $licenseText + '}'
    Set-Content -LiteralPath $licenseRtf -Value $rtf -Encoding ascii
    New-Item -ItemType Directory -Path (Split-Path -Parent $Output) -Force | Out-Null

    & $Wix build installer/WinMim.wxs -arch x64 -ext $UiExtension -ext $NetFxExtension -d "Version=$Version" -d "Payload=$payload" -d "LicenseRtf=$licenseRtf" -pdbtype none -out $Output
    if ($LASTEXITCODE -ne 0) { throw 'WiX MSI build failed' }
    Get-FileHash -LiteralPath $Output -Algorithm SHA256
}
finally {
    Pop-Location
}
