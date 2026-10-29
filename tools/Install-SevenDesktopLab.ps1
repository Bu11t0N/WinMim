$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'AEROFORGE-LAB'){throw 'Dedicated VM only'}
if(-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Dependency installation requires elevated guest token'}
$root='C:\AeroForgeLab'
Start-Transcript "$root\install-desktop.log" -Append
try {
 $expected=@{
 'OpenShellSetup_4_4_198.exe'='a4d2d4459de55b5e962ba2a14f7bb794170511649138173dfa72949837b48c3f'
 'OpenGlassSetup.Signed.exe'='191553cf594af2459631a8d00a48ffe0375868210f5e84c60ff75a668529405f'
 'SecureUxTheme_x64.msi'='af6ab67e0a283a0138b827b6731d5bb1f4ffc59e6d905f1c41567eb3305a9537'
 }
 foreach($name in $expected.Keys){if((Get-FileHash -LiteralPath "$root\$name" -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected[$name]){throw "Installer hash mismatch: $name"}}
 $os=Start-Process "$root\OpenShellSetup_4_4_198.exe" -ArgumentList '/qn /norestart ADDLOCAL=StartMenu /l*v C:\AeroForgeLab\openshell-install.log' -Wait -PassThru
 "OpenShell exit: $($os.ExitCode)" | Out-File "$root\desktop-install-status.txt" -Append
 if($os.ExitCode -notin 0,3010){throw 'OpenShell failed'}
 $ux=Start-Process msiexec.exe -ArgumentList '/i C:\AeroForgeLab\SecureUxTheme_x64.msi /qn /norestart /l*v C:\AeroForgeLab\secureuxtheme-install.log' -Wait -PassThru
 "SecureUxTheme exit: $($ux.ExitCode)" | Out-File "$root\desktop-install-status.txt" -Append
 if($ux.ExitCode -notin 0,3010){throw 'SecureUxTheme failed'}
 $og=Start-Process "$root\OpenGlassSetup.Signed.exe" -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=C:\AeroForgeLab\openglass-install.log' -Wait -PassThru
 "OpenGlass exit: $($og.ExitCode)" | Out-File "$root\desktop-install-status.txt" -Append
 if($og.ExitCode -notin 0,3010){throw 'OpenGlass failed'}
 Expand-Archive "$root\aero10-seven.zip" "$root\Aero10Source" -Force
 Copy-Item -LiteralPath "$root\Aero10Source\Themes\Aero10" -Destination 'C:\Windows\Resources\Themes\Aero10' -Recurse -Force
 Copy-Item -LiteralPath "$root\Aero10Source\Themes\Aero10 Seven.theme" -Destination 'C:\Windows\Resources\Themes\Aero10 Seven.theme' -Force
 'installed'|Out-File "$root\desktop-installed.txt"
}catch{$_|Out-File "$root\desktop-install-error.txt"}
Stop-Transcript
