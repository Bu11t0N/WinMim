$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'AEROFORGE-LAB'){throw 'Dedicated VM only'}
$records=@(Get-ChildItem "$env:LOCALAPPDATA\AeroForgeLab" -Recurse -Filter '*.ini' | Where-Object {$_.FullName -like '*\ModsWritable\*'} | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName;Text=[IO.File]::ReadAllText($_.FullName)}
})
ConvertTo-Json -InputObject $records -Depth 4 | Out-File C:\AeroForgeLab\native-metadata.json -Encoding utf8


