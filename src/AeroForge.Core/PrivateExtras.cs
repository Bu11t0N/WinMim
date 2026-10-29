using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace AeroForge.Core;

public sealed record PrivateExtra(string Id,string Name,string FileName,string Sha256,string ProductPrefix,string? ProductCode,string[] InstallArguments,string UninstallerName);
public sealed record ExtraApplication(string Key,string Version,string Location,string Uninstall);
public sealed class ExtraReceipt
{
    public string Id {get;set;}="";
    public string State {get;set;}="";
    public DateTimeOffset Started {get;set;}=DateTimeOffset.UtcNow;
    public int? ExitCode {get;set;}
    public string? Error {get;set;}
    public ExtraApplication? Installed {get;set;}
    public string? UninstallerPath {get;set;}
    public string? UninstallerSha256 {get;set;}
}

// The private components remain outside the public WinMim archives.
// Native installers retain their own uninstallers; an existing installation is never adopted.
[SupportedOSPlatform("windows")]
public static class PrivateExtras
{
    public const string Assets=@"C:\AeroForgeLab\PrivateAssets\Extras";
    public static readonly PrivateExtra[] Components=[
        new("games","Игры Windows 7","W7Games.exe","80b9ed664e92c955f8a6944075a33d96666d6400d6a6ea10977f68e64e152989","Windows 7 Games",null,["/S"],"unwin7games.exe"),
        new("gadgets","Гаджеты рабочего стола","W7Gadgets.exe","b40ca4dcf78aa28e4bfab4af0a2026319e9b29d3546a136c32c31e2715a3183b","Windows Desktop Gadgets",null,["/VERYSILENT","/NOCANCEL","/NORESTART","/SUPPRESSMSGBOXES"],"unins000.exe"),
        new("media-center","Windows Media Center","W7MediaCenter.msi","163108d69ade19ff84bac066dfa2dffde6f3ba03d2995cc369901e4a5428415d","Windows Media Center","{7DADC74F-5BEA-409B-96BB-62D57AFC131A}",[],"")];
    private static string StateRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WinMim","Extras");
    public static PrivateExtra Find(string id)=>Components.SingleOrDefault(c=>c.Id==id)??throw new InvalidDataException("Неизвестный дополнительный компонент: "+id);
    public static bool InstallerAvailable(string id)=>File.Exists(SafePath.Under(Assets,Find(id).FileName));
    public static bool IsInstalled(string id)=>InstalledApplication(Find(id))!=null;
    public static bool IsOwned(string id)=>File.Exists(ReceiptPath(id))&&JsonFile.Read<ExtraReceipt>(ReceiptPath(id)).State=="Installed";
    private static string ReceiptPath(string id){Find(id);return SafePath.Under(StateRoot,id+".json");}
    private static void EnsureLab(bool elevated)
    {
        if(Environment.MachineName!="AEROFORGE-LAB")throw new InvalidOperationException("Дополнительные компоненты пока устанавливаются только в тестовой ВМ AEROFORGE-LAB.");
        var info=WindowsBackend.Inspect();
        if(!info.KnownBase||elevated&&!info.IsAdministrator)throw new InvalidOperationException("Нужен повышенный токен тестовой Windows 10 x64.");
    }
    public static ExtraApplication? InstalledApplication(PrivateExtra component)
    {
        foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
            foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
            {
                using var root=RegistryKey.OpenBaseKey(hive,view);
                using var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if(uninstall==null)continue;
                foreach(string name in uninstall.GetSubKeyNames())
                {
                    using var key=uninstall.OpenSubKey(name);
                    if(key?.GetValue("DisplayName") is not string display || !display.StartsWith(component.ProductPrefix,StringComparison.OrdinalIgnoreCase))continue;
                    return new(hive+"/"+view+"/"+name,key.GetValue("DisplayVersion") as string??"",key.GetValue("InstallLocation") as string??"",key.GetValue("UninstallString") as string??"");
                }
            }
        return null;
    }
    public static async Task<ExtraReceipt> InstallAsync(string id)
    {
        EnsureLab(true);var component=Find(id);
        if(InstalledApplication(component) is { } existing)
        {
            if(!File.Exists(ReceiptPath(id)))return new(){Id=id,State="Preexisting"};
            var previous=JsonFile.Read<ExtraReceipt>(ReceiptPath(id));
            // Recover only an installation completed by this helper, whose registration
            // was captured before post-install validation failed. Never adopt an outsider.
            if(previous.State=="InstallFailed" && previous.ExitCode is 0 or 3010 && previous.Installed==existing)
            {
                Directory.CreateDirectory(StateRoot);
                using var recoveryLock=new FileStream(SafePath.Under(StateRoot,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
                if(component.ProductCode==null){previous.UninstallerPath=Uninstaller(component,existing);previous.UninstallerSha256=Hashing.File(previous.UninstallerPath);}
                previous.State="Installed";previous.Error=null;JsonFile.Write(ReceiptPath(id),previous);
            }
            return previous;
        }
        string installer=SafePath.Under(Assets,component.FileName);
        if(!File.Exists(installer)||!Hashing.File(installer).Equals(component.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Пакет отсутствует или его SHA-256 не совпадает: "+component.FileName);
        Directory.CreateDirectory(StateRoot);
        using var operationLock=new FileStream(SafePath.Under(StateRoot,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var receipt=new ExtraReceipt{Id=id,State="Installing"};JsonFile.Write(ReceiptPath(id),receipt);
        try
        {
            if(id=="media-center")await EnsureDvdAsync();
            var start=new ProcessStartInfo(component.ProductCode==null?installer:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"))
                {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Assets};
            if(component.ProductCode==null)foreach(string argument in component.InstallArguments)start.ArgumentList.Add(argument);
            else foreach(string argument in new[]{"/i",installer,"/qn","/norestart","REBOOT=ReallySuppress","/L*v",SafePath.Under(StateRoot,id+"-install.log")})start.ArgumentList.Add(argument);
            if(id=="media-center")start.ArgumentList.Add("DVDPLAYER=Microsoft.WindowsDVDPlayer");
            using var child=Process.Start(start)??throw new IOException("Установщик не запущен.");
            await child.WaitForExitAsync();receipt.ExitCode=child.ExitCode;
            if(child.ExitCode is not (0 or 3010))throw new IOException("Код установщика "+component.Name+": "+child.ExitCode);
            for(int attempt=0;attempt<60 && InstalledApplication(component)==null;attempt++)await Task.Delay(500);
            receipt.Installed=InstalledApplication(component)??throw new IOException("Установщик завершился, но регистрация компонента не найдена: "+component.Name);
            if(component.ProductCode==null)
            {
                receipt.UninstallerPath=Uninstaller(component,receipt.Installed);
                receipt.UninstallerSha256=Hashing.File(receipt.UninstallerPath);
            }
            receipt.State="Installed";JsonFile.Write(ReceiptPath(id),receipt);return receipt;
        }
        catch(Exception error){receipt.State="InstallFailed";receipt.Error=error.Message;JsonFile.Write(ReceiptPath(id),receipt);throw;}
    }
    public static async Task<ExtraReceipt> RemoveAsync(string id)
    {
        EnsureLab(true);var component=Find(id);
        Directory.CreateDirectory(StateRoot);
        using var operationLock=new FileStream(SafePath.Under(StateRoot,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(!File.Exists(ReceiptPath(id)))throw new InvalidOperationException("WinMim не устанавливал этот компонент.");
        var receipt=JsonFile.Read<ExtraReceipt>(ReceiptPath(id));
        if(receipt.State is not ("Installed" or "RemoveFailed"))throw new InvalidOperationException("Нет собственного установленного компонента для удаления.");
        var current=InstalledApplication(component);
        if(current==null){receipt.State="Removed";JsonFile.Write(ReceiptPath(id),receipt);return receipt;}
        if(current.Key!=receipt.Installed?.Key||current.Version!=receipt.Installed.Version)throw new IOException("Компонент позднее изменён; автоматическое удаление остановлено.");
        var start=new ProcessStartInfo(){UseShellExecute=false,CreateNoWindow=true};
        if(component.ProductCode!=null)
        {
            start.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe");
            foreach(string argument in new[]{"/x",component.ProductCode,"/qn","/norestart","REBOOT=ReallySuppress","/L*v",SafePath.Under(StateRoot,id+"-remove.log")})start.ArgumentList.Add(argument);
        }
        else
        {
            start.FileName=Uninstaller(component,current);
            if(start.FileName!=receipt.UninstallerPath || Hashing.File(start.FileName)!=receipt.UninstallerSha256)throw new IOException("Деинсталлятор изменён; он сохранён.");
            foreach(string argument in id=="games"?new[]{"/S"}:new[]{"/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART"})start.ArgumentList.Add(argument);
        }
        receipt.State="Removing";JsonFile.Write(ReceiptPath(id),receipt);
        try
        {
            using var child=Process.Start(start)??throw new IOException("Деинсталлятор не запущен.");await child.WaitForExitAsync();receipt.ExitCode=child.ExitCode;
            for(int attempt=0;attempt<60 && InstalledApplication(component)!=null;attempt++)await Task.Delay(500);
            if(child.ExitCode is not (0 or 3010)||InstalledApplication(component)!=null)throw new IOException("Удаление не завершено: "+child.ExitCode);
            receipt.State="Removed";receipt.Error=null;JsonFile.Write(ReceiptPath(id),receipt);return receipt;
        }
        catch(Exception error){receipt.State="RemoveFailed";receipt.Error=error.Message;JsonFile.Write(ReceiptPath(id),receipt);throw;}
    }
    private static async Task EnsureDvdAsync()
    {
        // Use the original licensed Appx bundle, not the modified third-party MSI wrapper.
        // Appx deployment keeps Windows signature and license validation enabled.
        const string script="""
$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'AEROFORGE-LAB'){throw 'Wrong VM'}
$package='C:\AeroForgeLab\PrivateAssets\Extras\DVDPlayer.appxbundle'
$license='C:\AeroForgeLab\PrivateAssets\Extras\DVDPlayer-license.xml'
$state='C:\ProgramData\WinMim\Extras\dvd-prerequisite.json'
$before=@(Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq 'Microsoft.WindowsDVDPlayer')
if($before.Count -eq 0){
 if((Get-FileHash -LiteralPath $package).Hash -ne '35419629da584019bce6f3e309a9c6a91e31255ea3b526565a7a162e1e5269e5'){throw 'Changed DVD bundle'}
 if((Get-FileHash -LiteralPath $license).Hash -ne 'e5571f5063a9bf5f3cd679b5569bce0f04b53dca33ce969838cf434b49460ae7'){throw 'Changed DVD license'}
 @{state='Provisioning';started=(Get-Date).ToString('o')} | ConvertTo-Json | Set-Content $state -Encoding UTF8
 Add-AppxProvisionedPackage -Online -PackagePath $package -LicensePath $license | Out-Null
 $after=@(Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq 'Microsoft.WindowsDVDPlayer')
 if($after.Count -ne 1){throw 'DVD provisioning not complete'}
 @{state='Provisioned';package=$after[0].PackageName;started=(Get-Date).ToString('o')} | ConvertTo-Json | Set-Content $state -Encoding UTF8
}
""";
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)??throw new IOException("Помощник DVD не запущен.");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
        await File.WriteAllTextAsync(SafePath.Under(StateRoot,"dvd-prerequisite.log"),await output+await error);
        if(process.ExitCode!=0)throw new IOException("Зависимость DVD Player не установлена. См. dvd-prerequisite.log.");
    }
    private static string Uninstaller(PrivateExtra component,ExtraApplication installed)
    {
        string command=installed.Uninstall.Trim(),path;
        if(command.StartsWith('"')){int end=command.IndexOf('"',1);if(end<0)throw new InvalidDataException("Неоднозначный путь деинсталлятора.");path=command[1..end];}
        else {int end=command.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(end<0)throw new InvalidDataException("Деинсталлятор EXE не найден.");path=command[..(end+4)];}
        path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
        bool knownFolder=roots.Any(r=>!string.IsNullOrEmpty(r)&&path.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
        if(component.Id=="gadgets")knownFolder|=path.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Installer","Desktop Gadgets",component.UninstallerName),StringComparison.OrdinalIgnoreCase);
        if(!Path.GetFileName(path).Equals(component.UninstallerName,StringComparison.OrdinalIgnoreCase)||!knownFolder||!File.Exists(path))
            throw new InvalidDataException("Деинсталлятор не принадлежит ожидаемому компоненту.");
        return path;
    }
    public static async Task EnsureInstalledAsync(IEnumerable<string> requested,IProgress<string>? progress=null)
    {
        EnsureLab(false);var pending=requested.Select(Find).Where(c=>!IsInstalled(c.Id)).Select(c=>c.Id).Distinct().ToArray();
        if(pending.Length==0)return;
        progress?.Report("Установка дополнительных компонентов Windows 7");
        await ElevatedAsync("private-extras-install",pending);
        if(pending.Any(id=>!IsInstalled(id)))throw new IOException("Не все дополнительные компоненты установлены.");
    }
    public static Task RemoveElevatedAsync(string id){Find(id);return ElevatedAsync("private-extras-remove",[id]);}
    private static async Task ElevatedAsync(string operation,string[] ids)
    {
        EnsureLab(false);string cli=new[]{Path.Combine(AppContext.BaseDirectory,"AeroForge.Cli.exe"),Path.Combine(AppContext.BaseDirectory,"cli","AeroForge.Cli.exe")}.FirstOrDefault(File.Exists)??throw new FileNotFoundException("CLI WinMim не найден.");
        var start=new ProcessStartInfo(cli){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        foreach(string argument in new[]{operation,"--ids",string.Join(',',ids),"--accept-experimental"})start.ArgumentList.Add(argument);
        using var child=Process.Start(start)??throw new IOException("Системный помощник не запущен.");await child.WaitForExitAsync();
        if(child.ExitCode!=0)throw new IOException("Операция с дополнительным компонентом не завершена. Подробности: "+StateRoot);
    }
    public static string? NativeApplication(string kind)
    {
        var folders=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
        var paths=kind switch
        {
            "gadgets"=>folders.Select(f=>Path.Combine(f,"Windows Sidebar","sidebar.exe")),
            "media"=>new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"ehome","ehshell.exe")}.Concat(folders.Select(f=>Path.Combine(f,"Windows Media Center","ehshell.exe"))),
            "mines"=>folders.Select(f=>Path.Combine(f,"Microsoft Games","Minesweeper","Minesweeper.exe")),
            _=>Array.Empty<string>()
        };
        return paths.FirstOrDefault(File.Exists);
    }
}
