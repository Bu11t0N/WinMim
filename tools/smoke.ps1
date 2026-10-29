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
# Exercise the redistributable package without requiring private lab resources.
$catalog = Get-Content -LiteralPath (Join-Path $content 'catalog.json') -Raw | ConvertFrom-Json
$seven = $catalog.profiles | Where-Object id -eq 'seven'
$selected = @($catalog.modules | Where-Object { $_.id -in $seven.modules -and -not $_.requiresResources -and -not $_.criticalProcess } | ForEach-Object { $_.id }) -join ','
if (-not $selected) { throw 'No standalone Seven modules available for the smoke check' }
$receiptLines = & $cli stage --content $content --root $hostPath --state $statePath --profile seven --modules $selected --accept-experimental
if ($LASTEXITCODE -ne 0) { throw 'Staging failed' }
$receiptPath = $receiptLines | Select-Object -Last 1
& $cli rollback --content $content --root $hostPath --state $statePath --receipt $receiptPath | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Rollback failed' }
if ((Get-ChildItem -LiteralPath $hostPath -Recurse -File -Force).Count -ne 0) { throw 'Rollback left managed files behind' }
Write-Output ('PASS full package stage/rollback in ' + $scratch)
