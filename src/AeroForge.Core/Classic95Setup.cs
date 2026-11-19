using System.Diagnostics;
using System.Runtime.Versioning;
namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class Classic95Setup
{
    private readonly ContentStore? store;
    private readonly string content;
    private readonly Catalog? catalog;
    private readonly TransactionManager shellTransactions,desktopTransactions;
    private readonly TransactionManager panelTransactions;
    private readonly ShellHost host;
    private readonly string root;
    private readonly Func<bool,Task> switchSession;
    public static string Data=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
    public Classic95Setup(string content,Func<bool,Task>? switchSession=null)
        : this(content, switchSession, recoveryOnly: false) { }

    public static Classic95Setup ForRecovery(string? content = null) => new(content ?? "", null, recoveryOnly: true);

    private Classic95Setup(string content, Func<bool,Task>? switchSession, bool recoveryOnly)
    {
        this.switchSession=switchSession??(restore=>new ClassicSession(ClassicSession.ActiveSession).ElevatedAsync(restore));
        this.content = content;
        if (content.Length != 0)
        {
            store = new(content);
            try { catalog = store.Load(); }
            catch (Exception error) when (recoveryOnly && error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException) { }
        }
        root = Path.Combine(Data, "Classic95");
        host = new(Path.Combine(Data, "Host"), catalog ?? new());
        shellTransactions=new(new WindowsBackend(Path.Combine(Data,"Host")),Path.Combine(Data,"State"));
        desktopTransactions=new(new DesktopBackend(root,preserveStyleMetrics:true),Path.Combine(Data,"Classic95State"));
        panelTransactions=new(new RetroBarSettingsBackend(RetroBarHost.SettingsPath),Path.Combine(Data,"ClassicPanelState"));
    }
    public static bool IsActive=>Directory.Exists(Path.Combine(Data,"Classic95State")) && new TransactionManager(new DesktopBackend(Path.Combine(Data,"Classic95")),Path.Combine(Data,"Classic95State")).History(false).Any(r=>r.Status!="RolledBack");
    public static string? ActiveTarget=>Directory.Exists(Path.Combine(Data,"Classic95State"))?new TransactionManager(new DesktopBackend(Path.Combine(Data,"Classic95")),Path.Combine(Data,"Classic95State")).History(false).Where(r=>r.Status!="RolledBack").Select(r=>ClassicThemes.FromReceipt(r.ProfileId)).FirstOrDefault():null;
    private static string OperationPath => SafePath.Under(Data, "classic-transition.json");

    public static bool HasRecoveryWork =>
        File.Exists(OperationPath)
        || IsActive
        || new TransactionManager(new WindowsBackend(Path.Combine(Data, "Host")), Path.Combine(Data, "State"))
            .History(false).Any(receipt => receipt.Status != "RolledBack" && receipt.ProfileId is "windows95" or "windowsxp")
        || new TransactionManager(new RetroBarSettingsBackend(RetroBarHost.SettingsPath), Path.Combine(Data, "ClassicPanelState"))
            .History(false).Any(receipt => receipt.Status != "RolledBack");

    private static ClassicTransition? ReadTransition() =>
        File.Exists(OperationPath) ? JsonFile.Read<ClassicTransition>(OperationPath) : null;

    private static void RecordTransition(ClassicTransition transition) => JsonFile.Write(OperationPath, transition);

    private sealed record ClassicTransition(string Target, string Phase, bool RestartMenu, bool RestartEngine);

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
        if (ReadTransition() is { Phase: not "Applied" })
            throw new InvalidOperationException("Предыдущая операция прервана. Сначала запустите восстановление оформления.");
        if (store is null || catalog is null)
            throw new InvalidOperationException("Каталог WinMim нужен для применения оформления.");
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
        if(HasRecoveryWork)await RestoreCoreAsync(progress);
        var plan=Classic95Profile.Build(content,root,theme.Xp?XpAppearance.ReadPhysical():ClassicAppearance.Read(),target:target);
        var transition = new ClassicTransition(target, "Applying", IsMenuRunning(), host.IsRunning());
        RecordTransition(transition);
        Receipt? desktopReceipt = null;
        try
        {
            progress?.Report("Остановка предыдущего оформления");
            await StopMenuAsync();
            await host.WithDesktopStoppedAsync(async()=>
            {
            await host.StopAsync();
            progress?.Report(theme.Xp?"Возврат доступа к визуальным стилям Luna":"Включение штатной классической отрисовки");await switchSession(theme.Xp);
            if(theme.Xp)EnsureThemedSession();
            token.ThrowIfCancellationRequested();progress?.Report("Подготовка классического Проводника и панели задач");
            shellTransactions.Apply(modulePlan,progress,token);
            // No Explorer may consume intermediate theme/cache state during
            // apply or an immediate transaction rollback. Start its host only
            // after all desktop settings have reached their committed state.
            desktopReceipt=desktopTransactions.Apply(plan,progress,token,allowProfileUpdate:true);
            if(target!="windows-95")panelTransactions.Apply(RetroBarSettingsBackend.Plan(theme,Path.Combine(root,"QuickLaunch")),progress,token);
            progress?.Report("Перезапуск оболочки "+theme.Name);host.Start();
            });
            if(theme.Xp)await host.WaitForExplorerAsync(TimeSpan.FromSeconds(45),token);
            StartMenu();if(target!="windows-95")await RetroBarHost.StartAsync(root);
            DesktopIconOverrides.Refresh();
            if (theme.Xp) XpDesktopResources.RefreshCursors();
            RecordTransition(transition with { Phase = "Applied" });
            return desktopReceipt!;
        }
        catch (Exception original)
        {
            try { await RestoreCoreAsync(progress); }
            catch (Exception recovery)
            {
                throw new AggregateException("Применение прервано. Возврат можно продолжить из раздела «Восстановление».", original, recovery);
            }
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
        if (!HasRecoveryWork) return;
        // A transition can die before the first receipt, or after the last receipt
        // was rolled back but before the shell returned. Keep the marker in both cases.
        var transition = ReadTransition() ?? new ClassicTransition(
            ActiveTarget ?? (shellTransactions.History(false)
                .FirstOrDefault(receipt => receipt.Status != "RolledBack")?.ProfileId == "windowsxp" ? "windows-xp" : "windows-95"),
            "Restoring", RestartMenu: true, RestartEngine: true);
        RecordTransition(transition with { Phase = "Restoring" });

        var errors = new List<Exception>();
        async Task AttemptAsync(string stage, Func<Task> action)
        {
            try { await action(); }
            catch (Exception error) { errors.Add(new IOException(stage + ": " + error.Message, error)); }
        }
        Task UndoAsync(TransactionManager manager, Func<Receipt,bool>? owns = null, IReadOnlySet<string>? preserve = null)
        {
            manager.RollbackOwned(progress, owns, preserve);
            return Task.CompletedTask;
        }

        // Stop owned UI independently. Even if it cannot stop, try to restore the
        // other domains. Do not touch module files while their engine may be live.
        await AttemptAsync("Панель задач", () => RetroBarHost.StopAsync(root));
        await AttemptAsync("Меню «Пуск»", StopMenuAsync);
        await AttemptAsync("Оболочка", () => host.WithDesktopStoppedAsync(async () =>
        {
            var preserve = RetroBarSettingsBackend.Fields
                .Select(key => new Change { Kind = ChangeKind.File, Target = key }.Identity).ToHashSet();
            await AttemptAsync("Настройки панели", () => UndoAsync(panelTransactions, preserve: preserve));

            bool engineStopped = false;
            await AttemptAsync("Остановка движка", async () =>
            {
                await host.StopAsync();
                engineStopped = true;
            });
            await AttemptAsync("Настройки рабочего стола", () => UndoAsync(desktopTransactions));
            if (engineStopped)
                await AttemptAsync("Модули", () => UndoAsync(shellTransactions, receipt => receipt.ProfileId is "windows95" or "windowsxp"));

            await AttemptAsync("Доступ к визуальным стилям", () => switchSession(true));
            ClassicAppearance.NotifyThemeChanged();
            if (transition.Target == "windows-xp") XpDesktopResources.RefreshCursors();

            if (errors.Count == 0 && transition.RestartEngine && File.Exists(SafePath.Under(Path.Combine(Data, "Host"), "active-profile.json")))
            {
                await AttemptAsync("Запуск предыдущего движка", () =>
                {
                    if (catalog is null)
                        throw new FileNotFoundException("Оболочка Windows восстановлена. Для запуска прежних модулей нужен каталог WinMim.");
                    host.Start();
                    return Task.CompletedTask;
                });
            }
        }));
        if (transition.RestartMenu)
            await AttemptAsync("Запуск меню «Пуск»", () => { StartMenu(); return Task.CompletedTask; });
        DesktopIconOverrides.Refresh();
        if (errors.Count != 0)
            throw new AggregateException("Возврат не завершён. Повторный запуск продолжит восстановление.", errors);
        File.Delete(OperationPath);
    }
    public async Task<bool> ResumeAsync()
    {
        Guard();
        using var operation = await ClassicOperationLock.AcquireAsync(Data);
        var active = desktopTransactions.History(false).FirstOrDefault(receipt => receipt.Status != "RolledBack");
        if (ReadTransition() is { Phase: not "Applied" } || (active?.Status != "Applied" && HasRecoveryWork))
        {
            await RestoreCoreAsync(null);
            return false;
        }
        if (!IsActive) return false;
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
        return true;
    }
    private static void EnsureThemedSession()
    {
        if(new ClassicSession(ClassicSession.ActiveSession).Inspect().Value!.Contains("(D;;LC;;;IU)",StringComparison.Ordinal))
            throw new IOException("Доступ к ThemeSection всё ещё ограничен. Восстановите журнал Classic; чужой ACL не изменён.");
    }
    private static string ShellPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe");
    private static bool IsMenuRunning()
    {
        int session = Process.GetCurrentProcess().SessionId;
        foreach (var process in Process.GetProcessesByName("StartMenu"))
        {
            using (process)
                try
                {
                    if (process.SessionId == session && string.Equals(WindowsProcessInfo.ImagePath(process.Id), ShellPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
        }
        return false;
    }

    private static async Task StopMenuAsync()
    {
        if (!File.Exists(ShellPath)) return;
        using var process=Process.Start(new ProcessStartInfo(ShellPath,"-exit"){UseShellExecute=false,CreateNoWindow=true});
        if(process!=null)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
    }
    private static void StartMenu()
    {
        if (File.Exists(ShellPath)) Process.Start(new ProcessStartInfo(ShellPath) { UseShellExecute = true })?.Dispose();
    }
}
