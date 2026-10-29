using System.Diagnostics;
using System.Runtime.Versioning;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class DesktopController
{
    public string Root { get; }
    public string State { get; }
    private readonly TransactionManager transactions;
    public DesktopController(string? root=null,string? state=null)
    {
        Root=Path.GetFullPath(root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge","DesktopV2"));
        State=Path.GetFullPath(state??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge","DesktopState"));
        transactions=new(new DesktopBackend(Root),State);
    }
    public static void EnsureLab()
    {
        if (Environment.MachineName!="AEROFORGE-LAB") throw new InvalidOperationException("Оформление пока проверяется только в AEROFORGE-LAB.");
        var info=WindowsBackend.Inspect();
        if (!info.KnownBase || info.IsAdministrator) throw new InvalidOperationException("Требуется обычный пользователь тестовой Windows 10 x64.");
    }
    public IReadOnlyList<Receipt> History(bool includeEntries=true)=>transactions.History(includeEntries);
    public TaskbarPreferences TaskbarSettings()
    {
        EnsureLab();
        var backend=new DesktopBackend(Root);
        int Number(string name,int fallback)
        {
            var state=backend.Read(new(){Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.Advanced,ValueName=name});
            return state.Exists&&int.TryParse(state.Value,out int value)?value:fallback;
        }
        var taskbar=backend.Read(new(){Kind=ChangeKind.TaskbarState,Target="Windows.TaskbarState"});
        return new(Number("TaskbarSizeMove",0)==0,(uint.Parse(taskbar.Value!)&1)!=0,
            Number("TaskbarSmallIcons",0)!=0,Number("TaskbarGlomLevel",0),Number("DisablePreviewDesktop",0)==0);
    }
    public Receipt SetTaskbar(TaskbarPreferences preferences)
    {
        EnsureLab();preferences.Validate();
        var latest=History(false).FirstOrDefault(r=>r.Status!="RolledBack");
        if(latest?.ProfileId!="desktop-seven")throw new InvalidOperationException("Сначала примените оформление WinMim.");
        // Same profile, new journal layer: removal still reaches the original settings.
        var plan=new Plan{ProfileId="desktop-seven",ProfileName="Свойства панели задач",Exclusive=true,Changes=preferences.Changes().ToList()};
        plan.AdoptUserPreferences=TaskbarPreferences.UserSettingIdentities.ToHashSet();
        var receipt=transactions.Apply(plan,allowProfileUpdate:true);
        DesktopBackend.NotifySettingsChanged();return receipt;
    }
    public Receipt Apply(string style,string wallpaper,string? button,IProgress<string>? progress=null,CancellationToken cancellationToken=default,string? privateIconPack=null)
    {
        EnsureLab();
        string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe");
        if(!File.Exists(shell))throw new FileNotFoundException("Сначала установите Open-Shell.",shell);
        var plan=DesktopProfile.BuildSeven(Root,style,wallpaper,button,taskbarPreferences:TaskbarSettings(),privateIconPack:privateIconPack);
        var receipt=transactions.Apply(plan,progress,cancellationToken,allowProfileUpdate:true);
        RefreshShell(shell,progress);
        return receipt;
    }
    public Receipt Restore(IProgress<string>? progress=null)
    {
        EnsureLab();
        var latest=History(false).FirstOrDefault(r=>r.Status!="RolledBack") ?? throw new InvalidOperationException("Нет активного оформления для возврата.");
        var receipt=transactions.Rollback(latest.FilePath,progress,TaskbarPreferences.UserSettingIdentities);
        while(receipt.Status=="RolledBack" && History(false).FirstOrDefault(r=>r.Status!="RolledBack") is {} previous)
            receipt=transactions.Rollback(previous.FilePath,progress,TaskbarPreferences.UserSettingIdentities);
        RefreshShell(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe"),progress);
        return receipt;
    }
    internal Receipt RollbackUpdate(string receiptPath,IProgress<string>? progress)
    {
        EnsureLab();
        var receipt=transactions.Rollback(receiptPath,progress,TaskbarPreferences.UserSettingIdentities);
        RefreshShell(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe"),progress);
        return receipt;
    }
    public static async Task RestartExplorerAsync()
    {
        EnsureLab();
        int session=Process.GetCurrentProcess().SessionId;
        string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
        foreach(var process in Process.GetProcessesByName("explorer"))
        {
            using(process)
                if(process.SessionId==session && string.Equals(WindowsProcessInfo.ImagePath(process.Id),executable,StringComparison.OrdinalIgnoreCase))
                    process.Kill();
        }
        await Task.Delay(3000);
        bool started=Process.GetProcessesByName("explorer").Any(p=>{using(p)return p.SessionId==session;});
        if(!started)Process.Start(new ProcessStartInfo(executable){UseShellExecute=true})?.Dispose();
    }
    private static void RefreshShell(string shell,IProgress<string>? progress)
    {
        // A committed transaction remains committed if a component can't refresh.
        try
        {
            DesktopBackend.NotifySettingsChanged();
            DesktopIconOverrides.Refresh();
            if(File.Exists(shell))Process.Start(new ProcessStartInfo(shell,"-reloadsettings"){UseShellExecute=false,CreateNoWindow=true})?.Dispose();
        }
        catch(Exception error){progress?.Report("Настройки записаны; обновление оболочки: "+error.Message);}
    }
}
