using System.Diagnostics;
using System.Runtime.Versioning;
namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class Classic95Setup
{
    private readonly ContentStore store;
    private readonly string content;
    private readonly Catalog catalog;
    private readonly TransactionManager shellTransactions,desktopTransactions;
    private readonly TransactionManager panelTransactions;
    private readonly ShellHost host;
    private readonly string root;
    private readonly Func<bool,Task> switchSession;
    public static string Data=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
    public Classic95Setup(string content,Func<bool,Task>? switchSession=null)
    {
        this.switchSession=switchSession??(restore=>new ClassicSession(ClassicSession.ActiveSession).ElevatedAsync(restore));
        this.content=content;store=new(content);catalog=store.Load();root=Path.Combine(Data,"Classic95");
        host=new(Path.Combine(Data,"Host"),catalog);
        shellTransactions=new(new WindowsBackend(Path.Combine(Data,"Host")),Path.Combine(Data,"State"));
        desktopTransactions=new(new DesktopBackend(root,preserveStyleMetrics:true),Path.Combine(Data,"Classic95State"));
        panelTransactions=new(new RetroBarSettingsBackend(RetroBarHost.SettingsPath),Path.Combine(Data,"ClassicPanelState"));
    }
    public static bool IsActive=>Directory.Exists(Path.Combine(Data,"Classic95State")) && new TransactionManager(new DesktopBackend(Path.Combine(Data,"Classic95")),Path.Combine(Data,"Classic95State")).History(false).Any(r=>r.Status!="RolledBack");
    public static string? ActiveTarget=>Directory.Exists(Path.Combine(Data,"Classic95State"))?new TransactionManager(new DesktopBackend(Path.Combine(Data,"Classic95")),Path.Combine(Data,"Classic95State")).History(false).Where(r=>r.Status!="RolledBack").Select(r=>ClassicThemes.FromReceipt(r.ProfileId)).FirstOrDefault():null;
    public static void Guard()
    {
        DesktopController.EnsureLab();
        if(WindowsBackend.Inspect().Build!=19044)throw new InvalidOperationException("Классические профили пока испытываются только на LTSC 2021 build 19044.");
    }
    public async Task<Receipt> ApplyAsync(IProgress<string>? progress=null,CancellationToken token=default)
        =>await ApplyThemeAsync("windows-95",progress,token);
    public async Task<Receipt> ApplyThemeAsync(string target,IProgress<string>? progress=null,CancellationToken token=default)
    {
        Guard();var theme=ClassicThemes.Get(target);using var operation=await ClassicOperationLock.AcquireAsync(Data,token);
        RetroBarHost.EnsureNoForeignInstance(root);
        var errors=store.VerifyAll(catalog);if(errors.Length!=0)throw new InvalidDataException(string.Join("; ",errors));
        string shell=ShellPath;if(!File.Exists(shell))throw new FileNotFoundException("Нужен установленный Open-Shell.");
        // Validate assets before restoring a working profile. Then recapture the
        // restored baseline, so every new theme returns to the same original.
        var preview=Classic95Profile.Build(content,root,theme.Xp?XpAppearance.ReadPhysical():ClassicAppearance.Read(),target:target);
        if(theme.Xp)XpRuntime.Check(content);
        string? iconPack=theme.Xp?XpThemePack.Read(content,XpThemePack.PrivateAssets).IconPackDirectory:null;
        var modulePlan=new ProfilePlanner(store,catalog).Build(theme.ShellProfile,autostart:true,hostExecutable:host.Executable,resumeLauncher:ProfilePlanner.FindController(),privateIconPack:iconPack);
        if(modulePlan.Blockers.Count!=0)throw new InvalidOperationException(string.Join("; ",modulePlan.Blockers));
        var current=desktopTransactions.History(false).FirstOrDefault(r=>r.Status!="RolledBack");
        if(current is {Status:"Applied"} && ClassicThemes.FromReceipt(current.ProfileId)==target
            && IsUnchanged(desktopTransactions,new DesktopBackend(root,preserveStyleMetrics:true))
            && IsUnchanged(shellTransactions,new WindowsBackend(Path.Combine(Data,"Host")),theme.ShellProfile)
            && MatchesPlan(new DesktopBackend(root,preserveStyleMetrics:true),preview)
            && MatchesPlan(new WindowsBackend(Path.Combine(Data,"Host")),modulePlan)
            && (target=="windows-95"||IsUnchanged(panelTransactions,new RetroBarSettingsBackend(RetroBarHost.SettingsPath))))
        {
            if(theme.Xp){await switchSession(true);EnsureThemedSession();}
            host.Start();if(target!="windows-95")await RetroBarHost.StartAsync(root);
            var existing=JsonFile.Read<Receipt>(current.FilePath);existing.FilePath=current.FilePath;return existing;
        }
        if(IsActive)await RestoreCoreAsync(progress);
        var plan=Classic95Profile.Build(content,root,theme.Xp?XpAppearance.ReadPhysical():ClassicAppearance.Read(),target:target);
        Receipt? shellReceipt=null,desktopReceipt=null,panelReceipt=null;
        progress?.Report("Остановка предыдущего оформления");await host.StopAsync();await StopMenuAsync();
        try
        {
            progress?.Report(theme.Xp?"Возврат доступа к визуальным стилям Luna":"Включение штатной классической отрисовки");await switchSession(theme.Xp);
            if(theme.Xp)EnsureThemedSession();
            token.ThrowIfCancellationRequested();progress?.Report("Подготовка классического Проводника и панели задач");
            shellReceipt=shellTransactions.Apply(modulePlan,progress,token);
            // Luna is a basic style. Disable DWM non-client rendering before
            // changing the style, while retaining themed controls and DWM.
            if(theme.Xp){host.Start();await host.WaitForExplorerAsync(TimeSpan.FromSeconds(45),token);}
            desktopReceipt=desktopTransactions.Apply(plan,progress,token,allowProfileUpdate:true);
            if(target!="windows-95")panelReceipt=panelTransactions.Apply(RetroBarSettingsBackend.Plan(theme,Path.Combine(root,"QuickLaunch")),progress,token);
            progress?.Report("Перезапуск оболочки "+theme.Name);host.Start();await DesktopController.RestartExplorerAsync();
            if(theme.Xp)await host.WaitForExplorerAsync(TimeSpan.FromSeconds(45),token);
            StartMenu();if(target!="windows-95")await RetroBarHost.StartAsync(root);
            DesktopIconOverrides.Refresh();if(theme.Xp)XpDesktopResources.RefreshCursors();return desktopReceipt;
        }
        catch(Exception original)
        {
            var recoveryErrors=new List<Exception>();
            if(original is ApplyException failure && failure.Receipt.Status!="RolledBack")recoveryErrors.Add(failure);
            await host.StopAsync();
            await RetroBarHost.StopAsync(root);
            void Undo(TransactionManager manager,Receipt? receipt)
            {
                if(receipt==null)return;
                try{if(manager.Rollback(receipt.FilePath,progress).Status!="RolledBack")throw new IOException("Возврат содержит конфликты: "+receipt.ProfileId);}
                catch(Exception error){recoveryErrors.Add(error);}
            }
            Undo(panelTransactions,panelReceipt);Undo(desktopTransactions,desktopReceipt);Undo(shellTransactions,shellReceipt);
            try{await switchSession(true);}catch(Exception error){recoveryErrors.Add(error);}
            if(recoveryErrors.Count==0){host.Start();await DesktopController.RestartExplorerAsync();StartMenu();}
            if(recoveryErrors.Count!=0)throw new AggregateException("Применение прервано; проверьте журналы восстановления.",new[]{original}.Concat(recoveryErrors));
            throw;
        }
    }
    private static bool IsUnchanged(TransactionManager manager,IChangeBackend backend,string? profile=null)
    {
        var receipt=manager.History(false).FirstOrDefault(r=>r.Status!="RolledBack");
        if(receipt is not {Status:"Applied"}||profile!=null&&receipt.ProfileId!=profile)return false;
        var full=JsonFile.Read<Receipt>(receipt.FilePath);
        return full.Entries.All(entry=>backend.Read(entry.Change).Fingerprint==entry.ExpectedAfter);
    }
    private static bool MatchesPlan(IChangeBackend backend,Plan plan)=>plan.Changes.All(change=>
    {
        var current=backend.Read(change);var desired=ValueState.Desired(change);
        if(current.Fingerprint==desired.Fingerprint)return true;
        // Generated module INIs carry a notification timestamp. Ignore only
        // that field when checking a repeat; versions/settings/files still
        // must match the new plan, so upgrades cannot reuse stale receipts.
        if(change.Kind!=ChangeKind.File||!change.Target.StartsWith("Data/Engine/Mods/",StringComparison.Ordinal)||!change.Target.EndsWith(".ini",StringComparison.Ordinal)||current.Bytes==null||change.Content==null)return false;
        string[] Configuration(byte[] bytes)=>System.Text.Encoding.Unicode.GetString(bytes).Split('\n').Where(line=>!line.StartsWith("SettingsChangeTime=",StringComparison.Ordinal)).ToArray();
        return Configuration(current.Bytes).SequenceEqual(Configuration(change.Content));
    });
    public async Task RestoreAsync(IProgress<string>? progress=null)
    {Guard();using var operation=await ClassicOperationLock.AcquireAsync(Data);await RestoreCoreAsync(progress);}
    private async Task RestoreCoreAsync(IProgress<string>? progress)
    {
        bool xp=ActiveTarget=="windows-xp";
        Guard();await RetroBarHost.StopAsync(root);await host.StopAsync();await StopMenuAsync();
        foreach(var receipt in desktopTransactions.History(false).Where(r=>r.Status!="RolledBack"))
        {var result=desktopTransactions.Rollback(receipt.FilePath,progress);if(result.Status!="RolledBack")throw new IOException("Чужие изменения сохранены; часть оформления требует проверки журнала.");}
        foreach(var receipt in shellTransactions.History(false).TakeWhile(r=>r.ProfileId is "windows95" or "windowsxp"))
            if(receipt.Status!="RolledBack"){var result=shellTransactions.Rollback(receipt.FilePath,progress);if(result.Status!="RolledBack")throw new IOException("Модули изменены после применения; их изменения сохранены.");}
        await switchSession(true);
        ClassicAppearance.NotifyThemeChanged();
        if(xp)XpDesktopResources.RefreshCursors();
        host.Start();await DesktopController.RestartExplorerAsync();StartMenu();DesktopIconOverrides.Refresh();
        foreach(var receipt in panelTransactions.History(false).Where(r=>r.Status!="RolledBack"))
        {
            // RetroBar's user preferences remain user-owned; restore only fields
            // which still equal our applied values, preserving any edited field.
            var preserve=RetroBarSettingsBackend.Fields.Select(key=>new Change{Kind=ChangeKind.File,Target=key}.Identity).ToHashSet();
            panelTransactions.Rollback(receipt.FilePath,progress,preserve);
        }
    }
    public async Task ResumeAsync()
    {
        Guard();using var operation=await ClassicOperationLock.AcquireAsync(Data);if(!IsActive)return;
        var receipt=desktopTransactions.History().First(r=>r.Status=="Applied");
        var saved=receipt.Entries.Single(e=>e.Change.Kind==ChangeKind.File&&e.Change.Target=="classic-appearance.json");
        var backend=new WindowsBackend(root);if(backend.Read(saved.Change).Fingerprint!=saved.ExpectedAfter)throw new IOException("Сохранённая палитра Classic изменена.");
        var appearance=ClassicAppearance.Parse(File.ReadAllText(SafePath.Under(root,"classic-appearance.json")));
        if(ActiveTarget=="windows-xp")
        {
            XpRuntime.Check(content);_=XpThemePack.Read(content,XpThemePack.PrivateAssets);await switchSession(true);EnsureThemedSession();
            if(!IsUnchanged(shellTransactions,new WindowsBackend(Path.Combine(Data,"Host")),"windowsxp"))throw new IOException("Модули XP изменены после применения; изменения сохранены.");
            var backendDesktop=new DesktopBackend(root,preserveStyleMetrics:true);
            foreach(var entry in receipt.Entries.Where(e=>e.Change.Kind is ChangeKind.File or ChangeKind.VisualStyle))
                if(backendDesktop.Read(entry.Change).Fingerprint!=entry.ExpectedAfter)throw new IOException("Ресурс или стиль XP изменён после применения: "+entry.Change.Target);
            // The style is retained by Windows. Do not overwrite later user
            // appearance edits or rewrite unchanged metrics on every logon.
        }
        else
        {
            string dacl=new ClassicSession(ClassicSession.ActiveSession).Inspect().Value!;
            if(!dacl.Contains("(D;;LC;;;IU)",StringComparison.Ordinal))await switchSession(false);
            ClassicAppearance.Write(appearance);
        }
        if(ActiveTarget is string target && target!="windows-95")await RetroBarHost.StartAsync(root);
    }
    private static void EnsureThemedSession()
    {
        if(new ClassicSession(ClassicSession.ActiveSession).Inspect().Value!.Contains("(D;;LC;;;IU)",StringComparison.Ordinal))
            throw new IOException("Доступ к ThemeSection всё ещё ограничен. Восстановите журнал Classic; чужой ACL не изменён.");
    }
    private static string ShellPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe");
    private static async Task StopMenuAsync()
    {
        using var process=Process.Start(new ProcessStartInfo(ShellPath,"-exit"){UseShellExecute=false,CreateNoWindow=true});
        if(process!=null)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
    }
    private static void StartMenu()=>Process.Start(new ProcessStartInfo(ShellPath){UseShellExecute=true})?.Dispose();
}
