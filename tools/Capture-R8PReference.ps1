#Requires -Version 5.1
#Requires -PSEdition Desktop
param([Parameter(Mandatory=$true)][ValidateSet('before','after-install','after-reboot','control')][string]$Phase,[switch]$RegistrySupplement)
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'AEROFORGE-LAB' -or -not(Test-Path C:\R8P-Lab\reference.marker)){
    throw 'Only the dedicated reference clone with its marker is allowed.'
}
$destination=Join-Path C:\R8P-Lab\captures $Phase
if($RegistrySupplement){
 if(-not(Test-Path "$destination\complete.json")){throw 'A complete capture is required for registry supplementation.'}
}elseif(Test-Path $destination){throw 'Capture already exists; preserve the original evidence.'}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$issues=New-Object 'System.Collections.Generic.List[object]'
function Progress([string]$stage,[string]$path='',[int]$count=0){
 @{stage=$stage;path=$path;count=$count;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content "$destination\progress.json" -Encoding UTF8
}
Progress 'registry'
$os=Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
if(-not $RegistrySupplement){
[ordered]@{phase=$Phase;utc=[DateTime]::UtcNow.ToString('o');computer=$env:COMPUTERNAME;user=$env:USERNAME;build=$os.CurrentBuild;ubr=$os.UBR;registryView='64-bit';pointerSize=[IntPtr]::Size} | ConvertTo-Json | Set-Content "$destination\system.json" -Encoding UTF8
}elseif(-not(Test-Path "$destination\registry-index.initial.json")){
 Copy-Item -LiteralPath "$destination\registry-index.json" -Destination "$destination\registry-index.initial.json"
}
if([IntPtr]::Size -ne 8){throw 'Run the 64-bit PowerShell collector.'}
# Export targeted configuration, not SAM, SECURITY, saved passwords or user documents.
$keys=@(
 'HKLM\SOFTWARE\Revert8Plus','HKLM\SOFTWARE\Windhawk','HKLM\SOFTWARE\WOW6432Node\Windhawk',
 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options',
 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
 'HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
 'HKLM\SYSTEM\CurrentControlSet\Services',
 'HKCU\Software\Revert8Plus','HKCU\Software\OpenShell','HKCU\Software\StartIsBack',
 'HKCU\Software\Microsoft\Windows\DWM','HKCU\Software\Microsoft\Windows\CurrentVersion\Themes',
 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer',
 'HKCU\Software\Microsoft\Windows\CurrentVersion\Run','HKCU\Control Panel\Desktop','HKCU\Control Panel\Colors',
 'HKCU\AppEvents','HKLM\SOFTWARE\Classes\CLSID','HKCU\Software\Classes\CLSID')
$scopeFile=Join-Path $PSScriptRoot 'reference-scopes.json'
$extraKeys=if(Test-Path $scopeFile){@(Get-Content $scopeFile -Raw | ConvertFrom-Json)}else{@()}
foreach($extraKey in $extraKeys){
 if($extraKey -notmatch '^HKEY_(LOCAL_MACHINE|CURRENT_USER)\\SOFTWARE\\' -or $extraKey -match '(?i)winlogon|\\SAM(?:\\|$)|\\SECURITY(?:\\|$)|\\LSA(?:\\|$)|credential|vault'){throw 'Invalid extra registry scope'}
}
if($RegistrySupplement){$keys=$extraKeys}else{$keys+= $extraKeys}
$registry=@()
if($RegistrySupplement){$registry=@(Get-Content "$destination\registry-index.json" -Raw | ConvertFrom-Json)}
$index=$registry.Count
foreach($key in $keys){
 if($registry | Where-Object {$_.key -eq $key}){continue}
 $file=('registry-{0:D2}.reg' -f $index);$index++
 $providerKey='Registry::'+$key.Replace('HKLM\','HKEY_LOCAL_MACHINE\').Replace('HKCU\','HKEY_CURRENT_USER\')
 $exists=$null
 try{$exists=Test-Path -LiteralPath $providerKey -ErrorAction Stop}catch{$issues.Add(@{kind='registry';path=$key;error=$_.Exception.Message})}
 if($exists -eq $false){$registry+=@{key=$key;file=$null;exitCode=0;exists=$false};continue}
 $nativePreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
 & "$env:WINDIR\System32\reg.exe" export $key (Join-Path $destination $file) /y 1> $null 2> $null
 $code=$LASTEXITCODE;$ErrorActionPreference=$nativePreference
 $registry+=@{key=$key;file=$file;exitCode=$code;exists=$exists}
}
$registry | ConvertTo-Json -Depth 4 | Set-Content "$destination\registry-index.json" -Encoding UTF8
if($RegistrySupplement){
 @{utc=[DateTime]::UtcNow.ToString('o');scopes=$extraKeys;issues=@($issues.ToArray())} | ConvertTo-Json -Depth 6 | Set-Content "$destination\registry-supplement.json" -Encoding UTF8
 Progress 'completed'
 return
}
$files=New-Object 'System.Collections.Generic.List[object]'
$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
function Add-CapturedFile($file){
 if(-not $seen.Add($file.FullName)){return}
 if($files.Count % 50 -eq 0){Progress 'files' $file.FullName $files.Count}
 try{
  $stream=[IO.File]::Open($file.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
  $sha=[Security.Cryptography.SHA256]::Create()
  try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$stream.Dispose()}
  $security=[IO.File]::GetAccessControl($file.FullName)
  $acl=$security.GetSecurityDescriptorSddlForm(([Security.AccessControl.AccessControlSections]::Access -bor [Security.AccessControl.AccessControlSections]::Owner -bor [Security.AccessControl.AccessControlSections]::Group))
  $files.Add(@{path=$file.FullName;length=$file.Length;lastWriteUtc=$file.LastWriteTimeUtc.ToString('o');sha256=$hash;sddl=$acl})
 }catch{$issues.Add(@{kind='file';path=$file.FullName;error=$_.Exception.Message})}
}
foreach($dir in @("$env:WINDIR\System32","$env:WINDIR\SysWOW64",$env:WINDIR)){
 Get-ChildItem -LiteralPath $dir -File -ErrorAction Stop | Where-Object {$_.Extension -in '.dll','.exe','.cpl','.ini','.theme'} | ForEach-Object {Add-CapturedFile $_}
}
$directories=@("$env:WINDIR\Resources","$env:WINDIR\Revert8Plus","$env:ProgramData\Windhawk","$env:ProgramFiles\Windhawk","${env:ProgramFiles(x86)}\StartIsBack","$env:ProgramFiles\StartAllBack","$env:ProgramData\Microsoft\Windows\Start Menu","$env:APPDATA\Microsoft\Windows\Start Menu")
$links=New-Object 'System.Collections.Generic.List[object]'
function Capture-Tree([string]$directory){
 try{$entries=@(Get-ChildItem -LiteralPath $directory -Force -ErrorAction Stop)}catch{
  $issues.Add(@{kind='directory';path=$directory;error=$_.Exception.Message});return
 }
 foreach($entry in $entries){
  if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
   $links.Add(@{path=$entry.FullName;linkType=$entry.LinkType;target=$entry.Target});continue
  }
  if($entry.PSIsContainer){Capture-Tree $entry.FullName}else{Add-CapturedFile $entry}
 }
}
foreach($dir in $directories){if(Test-Path -LiteralPath $dir){try{
 $rootEntry=Get-Item -LiteralPath $dir -Force
 if(($rootEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){$links.Add(@{path=$dir;linkType=$rootEntry.LinkType;target=$rootEntry.Target});continue}
 Capture-Tree $dir
}catch{$issues.Add(@{kind='directory';path=$dir;error=$_.Exception.Message})}}}
$files | ConvertTo-Json -Depth 5 | Set-Content "$destination\files.json" -Encoding UTF8
$links | ConvertTo-Json -Depth 5 | Set-Content "$destination\links.json" -Encoding UTF8
Progress 'services' '' $files.Count
try{Get-CimInstance Win32_Service | Select-Object Name,PathName,StartMode,StartName,State | ConvertTo-Json -Depth 4 | Set-Content "$destination\services.json" -Encoding UTF8}catch{$issues.Add(@{kind='services';error=$_.Exception.Message})}
Progress 'tasks' '' $files.Count
try{Get-ScheduledTask | ForEach-Object {@{path=$_.TaskPath;name=$_.TaskName;xml=(Export-ScheduledTask -TaskName $_.TaskName -TaskPath $_.TaskPath)}} | ConvertTo-Json -Depth 6 | Set-Content "$destination\tasks.json" -Encoding UTF8}catch{$issues.Add(@{kind='tasks';error=$_.Exception.Message})}
$issues | ConvertTo-Json -Depth 5 | Set-Content "$destination\issues.json" -Encoding UTF8
@{completedUtc=[DateTime]::UtcNow.ToString('o');files=$files.Count;issues=$issues.Count} | ConvertTo-Json | Set-Content "$destination\complete.json" -Encoding UTF8
Compress-Archive -LiteralPath $destination -DestinationPath "$destination.zip"
Progress 'completed' '' $files.Count
