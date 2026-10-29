param(
    [ValidateSet('Apply','Restore')][string]$Action='Apply',
    [string]$StylePath='C:\Windows\Resources\Themes\Aero10\Seven\aero10_seven.msstyles',
    [string]$WallpaperPath='C:\Windows\Resources\Themes\Aero10\Wallpapers\img0.png',
    [string]$ButtonPath='C:\AeroForgeLab\Aero10Source\Extras\Start buttons\7 mid (big top-bottom taskbar)\6805.png',
    [switch]$RestartExplorer,
    [switch]$OpenMenu
)
$ErrorActionPreference='Stop'
# Compatibility entry point: all mutations and undo snapshots now belong to Core.
$cli=@(
    (Join-Path $PSScriptRoot '..\cli\AeroForge.Cli.exe'),
    (Join-Path $PSScriptRoot '..\build\cli\AeroForge.Cli.exe'),
    (Join-Path $PSScriptRoot '..\..\cli\AeroForge.Cli.exe')
) | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
if(-not $cli){throw 'Build AeroForge.Cli or use the complete portable package.'}
$desktopExit=0
try {
    # Windows PowerShell treats redirected native stderr as an error record.
    # The CLI reports progress there; use its exit code to determine success.
    $ErrorActionPreference='Continue'
if($Action -eq 'Apply'){
    $arguments=@('desktop-apply','--accept-experimental','--style',$StylePath,'--wallpaper',$WallpaperPath)
    if(Test-Path -LiteralPath $ButtonPath){$arguments+=@('--button',$ButtonPath)}
    & $cli @arguments
}else{& $cli desktop-restore}
    $desktopExit=$LASTEXITCODE
} finally {$ErrorActionPreference='Stop'}
if($desktopExit){throw 'Desktop operation failed; inspect the JSON transaction journal.'}
if($RestartExplorer){& $cli desktop-refresh;if($LASTEXITCODE){throw 'Explorer refresh failed'}}
if($OpenMenu){Start-Process (Join-Path $env:ProgramFiles 'Open-Shell\StartMenu.exe') -ArgumentList '-open'}
