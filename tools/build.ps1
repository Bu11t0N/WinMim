param([switch]$PrepareNative, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.cache\dotnet'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.cache\nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    if ($PrepareNative) {
        & python (Join-Path $PSScriptRoot 'fetch_classic_runtime.py')
        if ($LASTEXITCODE -ne 0) { throw 'Classic runtime preparation failed' }
        & python (Join-Path $PSScriptRoot 'prepare.py')
        if ($LASTEXITCODE -ne 0) { throw 'Native dependency preparation failed' }
        & python (Join-Path $PSScriptRoot 'build_modules.py')
        if ($LASTEXITCODE -ne 0) { throw 'Native compilation failed' }
        & python (Join-Path $PSScriptRoot 'verify_native.py')
        if ($LASTEXITCODE -ne 0) { throw 'Native ABI verification failed' }
    }
    & dotnet build AeroForge.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw '.NET build failed' }
    if (-not $SkipTests) {
        & dotnet run --project tests/AeroForge.Tests -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    }
    # Clean only the two generated publish directories. Never follow reparse points.
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'build'))
    foreach ($name in @('app', 'cli')) {
        $publishPath = [IO.Path]::GetFullPath((Join-Path $buildRoot $name))
        if (-not $publishPath.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish target' }
        if (Test-Path -LiteralPath $publishPath) {
            $item = Get-Item -LiteralPath $publishPath -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or (Get-ChildItem -LiteralPath $publishPath -Force -Recurse -Attributes ReparsePoint).Count) { throw 'Publish directory contains a symbolic link; preserving it' }
            Remove-Item -LiteralPath $publishPath -Recurse -Force
        }
    }
    & dotnet publish src/AeroForge.App -c Release --no-restore --no-self-contained -o build/app --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed' }
    & dotnet publish src/AeroForge.Cli -c Release --no-restore --no-self-contained -o build/cli --nologo
    if ($LASTEXITCODE -ne 0) { throw 'CLI publish failed' }
    & (Join-Path $projectRoot 'build\cli\AeroForge.Cli.exe') verify --content (Join-Path $projectRoot 'content')
    if ($LASTEXITCODE -ne 0) { throw 'Content verification failed' }
    Write-Output 'Built: build/app/WinMim.exe. No shell profile has been applied or started.'
}
finally { Pop-Location }

