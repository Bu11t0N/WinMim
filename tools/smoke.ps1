param([Parameter(Mandatory=$true)][string]$ScratchDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$scratch = [IO.Path]::GetFullPath($ScratchDirectory)
if (Test-Path -LiteralPath $scratch) { throw 'Choose a new empty scratch directory, not an existing directory.' }
$cli = Join-Path $projectRoot 'build\cli\AeroForge.Cli.exe'
$content = Join-Path $projectRoot 'content'
$hostPath = Join-Path $scratch 'Host'
$statePath = Join-Path $scratch 'State'
& $cli verify --content $content
if ($LASTEXITCODE -ne 0) { throw 'Verification failed' }
$receiptLines = & $cli stage --content $content --root $hostPath --state $statePath --profile seven --accept-experimental
if ($LASTEXITCODE -ne 0) { throw 'Staging failed' }
$receiptPath = $receiptLines | Select-Object -Last 1
& $cli rollback --content $content --root $hostPath --state $statePath --receipt $receiptPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Rollback failed' }
if ((Get-ChildItem -LiteralPath $hostPath -Recurse -File -Force).Count -ne 0) { throw 'Rollback left managed files behind' }
Write-Output ('PASS full package stage/rollback in ' + $scratch)
