using System.Diagnostics;
using System.Runtime.Versioning;

namespace AeroForge.Core;

public sealed record SetupCheck(string Name, bool Ready, string Detail);
public sealed record SevenSetupResult(string ShellReceipt, string DesktopReceipt);

[SupportedOSPlatform("windows")]
public sealed class SevenSetup
{
    private readonly ContentStore store;
    private readonly Catalog catalog;
    private readonly DesktopController desktop;
    private readonly TransactionManager transactions;
    private readonly ShellHost host;
    private readonly string hostRoot;
    public string Style { get; }
    public string Wallpaper { get; }
    public string? Button { get; }
    public string? IconPack { get; }
    private readonly string? displayProvider;
    public string? DisplayProvider=>displayProvider;

    public SevenSetup(string content, string? hostRoot=null, string? state=null, string? assets=null, string? button=null)
    {
        store=new(content); catalog=store.Load();
        string appData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
        this.hostRoot=hostRoot??Path.Combine(appData,"Host");
        transactions=new(new WindowsBackend(this.hostRoot),state??Path.Combine(appData,"State"));
        host=new(this.hostRoot,catalog); desktop=new();
        bool defaultAssets=assets==null;
        assets??=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Resources","Themes","Aero10");
        string privateStyle=@"C:\AeroForgeLab\PrivateAssets\SevenTheme\W7Aero-Redstone\style.msstyles";
        Style=defaultAssets&&File.Exists(privateStyle)?privateStyle:Path.Combine(assets,"Seven","aero10_seven.msstyles");
        Wallpaper=Path.Combine(assets,"Wallpapers","img0.png");
        string candidate=button??@"C:\AeroForgeLab\Aero10Source\Extras\Start buttons\7 mid (big top-bottom taskbar)\6805.png";
        Button=File.Exists(candidate)?candidate:null;
        string icons=Directory.Exists(@"C:\AeroForgeLab\PrivateAssets\SevenIconsFull")?@"C:\AeroForgeLab\PrivateAssets\SevenIconsFull":@"C:\AeroForgeLab\PrivateAssets\SevenIcons";
        IconPack=File.Exists(Path.Combine(icons,"manifest.json"))?icons:null;
        string provider=@"C:\AeroForgeLab\PrivateAssets\DisplayProvider\display.dll";
        displayProvider=File.Exists(provider)?provider:null;
    }

    private static bool Owns(Receipt receipt) => receipt.ProfileId == "seven";
    public bool HasRecoveryWork => desktop.HasRecoveryWork
        || transactions.History(false).Any(receipt => Owns(receipt) && receipt.Status != "RolledBack");

    public async Task RestoreAsync(IProgress<string>? progress = null)
    {
        DesktopController.EnsureLab();
        using var operation = await ClassicOperationLock.AcquireAsync(Classic95Setup.Data);
        if (!HasRecoveryWork) return;
        if (Classic95Setup.HasRecoveryWork)
            throw new InvalidOperationException(UiText.Get("SevenSetup.RestoreXPOrClassicFirst"));

        // Preserve the per-user journals if the system settings still need restoration.
        var machine = new MachineSeven();
        if (machine.HasRecoveryWork) await machine.RestoreElevatedAsync();

        await host.WithDesktopStoppedAsync(async () =>
        {
            await host.StopAsync();
            transactions.RollbackOwned(progress, Owns);
        });
        if (desktop.HasRecoveryWork && desktop.Restore(progress).Status != "RolledBack")
            throw new IOException(UiText.Get("SevenSetup.Windows7RecoveryNeedsAJournalReview"));
    }

    public SetupCheck[] Check()
    {
        var result=new List<SetupCheck>();
        if(Classic95Setup.HasRecoveryWork)result.Add(new(UiText.Get("SevenSetup.ClassicAppearance"),false,UiText.Get("SevenSetup.RestoreThePreviousAppearanceFirst")));
        try { DesktopController.EnsureLab(); result.Add(new("Windows",true,UiText.Get("SevenSetup.Windows10X64LabStandardUser"))); }
        catch(InvalidOperationException error){result.Add(new("Windows",false,error.Message));}
        result.Add(new(UiText.Get("SevenSetup.WinMimHelper"), DesktopController.FindHelper() != null, UiText.Get("SevenSetup.IfMissingRepairTheMSIInstallation")));
        var errors=store.VerifyAll(catalog);
        result.Add(new(UiText.Get("SevenSetup.SevenModules"),errors.Length==0,errors.Length==0?UiText.Get("SevenSetup.ChecksumsMatch"):string.Join("; ",errors)));
        void CheckDependency(string name, Func<bool> ready)
        {
            try { result.Add(new(name, ready(), UiText.Get("SevenSetup.UsePrepareTheme"))); }
            catch (Exception error) when (error is IOException or InvalidOperationException)
            { result.Add(new(name, false, error.Message)); }
        }
        CheckDependency("Open-Shell", ThemePreparation.OpenShellReady);
        CheckDependency("SecureUxTheme", () => ThemePreparation.SecureUxReady(store.Root));
        bool glass=false;
        foreach(var process in Process.GetProcessesByName("OpenGlassHost")){using(process)glass=true;}
        result.Add(new("OpenGlass",glass,glass?UiText.Get("SevenSetup.TheComponentIsRunning"):UiText.Get("SevenSetup.TheComponentIsStoppedSilentInstallationOf")));
        result.Add(new(UiText.Get("SevenSetup.Windows7Theme"), File.Exists(Style) && File.Exists(Wallpaper) && Button != null,
            UiText.Get("SevenSetup.ACompatibleStyleWallpaperAndStartButton")));
        Plan plan;
        try{plan=new ProfilePlanner(store,catalog).Build("seven",privateIconPack:IconPack,privateDisplayProvider:displayProvider);}
        catch(Exception error){result.Add(new(UiText.Get("SevenSetup.IconPack"),false,error.Message));return result.ToArray();}
        result.Add(new(UiText.Get("SevenSetup.Windows7Icons"),true,IconPack==null?UiText.Get("SevenSetup.NoPersonalPackIsConfiguredDefaultIcons"):UiText.Get("SevenSetup.TheExternalPackPassedSHA256Verification")));
        result.Add(new(UiText.Get("SevenSetup.SevenProfile"),plan.Blockers.Count==0,string.Join("; ",plan.Blockers)));
        bool foreign=false;
        foreach(var process in Process.GetProcessesByName("windhawk"))
            using(process)if(!string.Equals(WindowsProcessInfo.ImagePath(process.Id),host.Executable,StringComparison.OrdinalIgnoreCase))foreign=true;
        result.Add(new(UiText.Get("SevenSetup.Engine"),!foreign,foreign?UiText.Get("SevenSetup.StopTheOtherWindhawkInstanceFirst"):UiText.Get("SevenSetup.NoConflictingInstances")));
        try{result.Add(new(UiText.Get("SevenSetup.SystemSettings"),true,MachineSeven.IsConfigured()?UiText.Get("SevenSetup.ClassicVolumeAndActionCenterSettingsAre"):UiText.Get("SevenSetup.WindowsMayAskForAdministratorApprovalTo")));}
        catch(Exception error){result.Add(new(UiText.Get("SevenSetup.SystemSettings"),false,error.Message));}
        return result.ToArray();
    }

    public async Task<SevenSetupResult> ApplyAsync(bool restartExplorer,IProgress<string>? progress=null,CancellationToken cancellationToken=default)
    {
        DesktopController.EnsureLab();
        using var operation = await ClassicOperationLock.AcquireAsync(Classic95Setup.Data, cancellationToken);
        var blockers=Check().Where(c=>!c.Ready).ToArray();
        if(blockers.Length!=0)throw new InvalidOperationException(string.Join("\n",blockers.Select(c=>c.Name+": "+c.Detail)));
        bool wasRunning=host.IsRunning();
        Receipt? shellReceipt=null; Receipt? desktopReceipt=null;
        bool machineChanged=false;
        var previousDesktopIds=desktop.History(false).Select(r=>r.Id).ToHashSet();
        progress?.Report(UiText.Get("SevenSetup.16StoppingTheEngine"));
        await host.StopAsync();
        try
        {
            progress?.Report(UiText.Get("SevenSetup.26PreparingSevenModules"));
            shellReceipt=transactions.Apply(new ProfilePlanner(store,catalog).Build("seven",autostart:true,hostExecutable:host.Executable,privateIconPack:IconPack,privateDisplayProvider:displayProvider,resumeLauncher:ProfilePlanner.FindController()),progress,cancellationToken);
            progress?.Report(UiText.Get("SevenSetup.36ApplyingTheThemeStartMenu"));
            desktopReceipt=desktop.Apply(Style,Wallpaper,Button,progress,cancellationToken,privateIconPack:IconPack);
            cancellationToken.ThrowIfCancellationRequested();
            // Resource hooks must already be active when Explorer builds its image lists.
            progress?.Report(UiText.Get("SevenSetup.46StartingModules")); host.Start();
            await host.WaitForExplorerAsync(TimeSpan.FromSeconds(45),cancellationToken);
            progress?.Report(UiText.Get("SevenSetup.56SevenSystemSettings"));
            machineChanged=await new MachineSeven().EnsureConfiguredAsync();
            if(restartExplorer){progress?.Report(UiText.Get("SevenSetup.66RefreshingExplorer"));await DesktopController.RestartExplorerAsync();await host.WaitForExplorerAsync(TimeSpan.FromSeconds(45),cancellationToken);}
            DesktopIconOverrides.Refresh();
            cancellationToken.ThrowIfCancellationRequested();
            return new(shellReceipt.FilePath,desktopReceipt.FilePath);
        }
        catch(Exception original)
        {
            var recoveryErrors=new List<Exception>();
            try{await host.StopAsync();}catch(Exception error){throw new AggregateException(UiText.Get("SevenSetup.TheEngineDidNotStopFilesWere"),original,error);}
            if(machineChanged)
                try{await new MachineSeven().RestoreElevatedAsync();}catch(Exception error){recoveryErrors.Add(error);}
            if(desktopReceipt!=null&&!previousDesktopIds.Contains(desktopReceipt.Id))
                try{var restored=desktop.RollbackUpdate(desktopReceipt.FilePath,progress);if(restored.Status!="RolledBack")throw new IOException(UiText.Get("SevenSetup.AppearanceRecoveryHasConflicts"));}catch(Exception error){recoveryErrors.Add(error);}
            if(shellReceipt!=null)
                try{var restored=transactions.Rollback(shellReceipt.FilePath,progress);if(restored.Status!="RolledBack")throw new IOException(UiText.Get("SevenSetup.ModuleRecoveryHasConflicts"));}catch(Exception error){recoveryErrors.Add(error);}
            if(wasRunning&&recoveryErrors.Count==0)try{host.Start();}catch(Exception error){recoveryErrors.Add(error);}
            if(recoveryErrors.Count!=0)throw new AggregateException(UiText.Get("SevenSetup.ApplyDidNotFinishReviewTheRecovery"),new[]{original}.Concat(recoveryErrors));
            throw;
        }
    }
}
