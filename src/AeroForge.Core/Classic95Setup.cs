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
        desktopTransactions=new(new DesktopBackend(root),Path.Combine(Data,"Classic95State"));
    }
    public static bool IsActive=>Directory.Exists(Path.Combine(Data,"Classic95State")) && new TransactionManager(new DesktopBackend(Path.Combine(Data,"Classic95")),Path.Combine(Data,"Classic95State")).History(false).Any(r=>r.Status!="RolledBack");
    public static void Guard()
    {
        DesktopController.EnsureLab();
        if(WindowsBackend.Inspect().Build!=19044)throw new InvalidOperationException("Windows 95 пока испытывается только на LTSC 2021 build 19044.");
    }
    public async Task<Receipt> ApplyAsync(IProgress<string>? progress=null,CancellationToken token=default)
    {
        Guard();bool wasActive=IsActive;
        var errors=store.VerifyAll(catalog);if(errors.Length!=0)throw new InvalidDataException(string.Join("; ",errors));
        string shell=ShellPath;if(!File.Exists(shell))throw new FileNotFoundException("Нужен установленный Open-Shell.");
        var plan=Classic95Profile.Build(content,root,ClassicAppearance.Read());
        var modulePlan=new ProfilePlanner(store,catalog).Build("windows95",autostart:true,hostExecutable:host.Executable,resumeLauncher:ProfilePlanner.FindController());
        if(modulePlan.Blockers.Count!=0)throw new InvalidOperationException(string.Join("; ",modulePlan.Blockers));
        Receipt? shellReceipt=null,desktopReceipt=null;var session=new ClassicSession(ClassicSession.ActiveSession);
        progress?.Report("Остановка предыдущего оформления");await host.StopAsync();await StopMenuAsync();
        try
        {
            progress?.Report("Включение штатной классической отрисовки");await switchSession(false);
            token.ThrowIfCancellationRequested();progress?.Report("Подготовка классического Проводника и панели задач");
            shellReceipt=shellTransactions.Apply(modulePlan,progress,token);
            desktopReceipt=desktopTransactions.Apply(plan,progress,token,allowProfileUpdate:true);
            progress?.Report("Перезапуск оболочки Windows 95");host.Start();await DesktopController.RestartExplorerAsync();
            StartMenu();DesktopIconOverrides.Refresh();return desktopReceipt;
        }
        catch
        {
            await host.StopAsync();
            if(desktopReceipt!=null)desktopTransactions.Rollback(desktopReceipt.FilePath,progress);
            if(shellReceipt!=null)shellTransactions.Rollback(shellReceipt.FilePath,progress);
            if(!wasActive)await switchSession(true);
            host.Start();StartMenu();throw;
        }
    }
    public async Task RestoreAsync(IProgress<string>? progress=null)
    {
        Guard();await host.StopAsync();await StopMenuAsync();
        foreach(var receipt in desktopTransactions.History(false).Where(r=>r.Status!="RolledBack"))
        {var result=desktopTransactions.Rollback(receipt.FilePath,progress);if(result.Status!="RolledBack")throw new IOException("Чужие изменения сохранены; часть оформления требует проверки журнала.");}
        foreach(var receipt in shellTransactions.History(false).TakeWhile(r=>r.ProfileId=="windows95"))
            if(receipt.Status!="RolledBack"){var result=shellTransactions.Rollback(receipt.FilePath,progress);if(result.Status!="RolledBack")throw new IOException("Модули изменены после применения; их изменения сохранены.");}
        await switchSession(true);
        host.Start();await DesktopController.RestartExplorerAsync();StartMenu();DesktopIconOverrides.Refresh();
    }
    public async Task ResumeAsync()
    {
        Guard();if(!IsActive)return;
        var receipt=desktopTransactions.History().First(r=>r.Status=="Applied");
        var saved=receipt.Entries.Single(e=>e.Change.Kind==ChangeKind.File&&e.Change.Target=="classic-appearance.json");
        var backend=new WindowsBackend(root);if(backend.Read(saved.Change).Fingerprint!=saved.ExpectedAfter)throw new IOException("Сохранённая палитра Classic изменена.");
        var appearance=ClassicAppearance.Parse(File.ReadAllText(SafePath.Under(root,"classic-appearance.json")));
        string dacl=new ClassicSession(ClassicSession.ActiveSession).Inspect().Value!;
        if(!dacl.Contains("(D;;LC;;;IU)",StringComparison.Ordinal))await switchSession(false);
        ClassicAppearance.Write(appearance);
    }
    private static string ShellPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe");
    private static async Task StopMenuAsync()
    {
        using var process=Process.Start(new ProcessStartInfo(ShellPath,"-exit"){UseShellExecute=false,CreateNoWindow=true});
        if(process!=null)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
    }
    private static void StartMenu()=>Process.Start(new ProcessStartInfo(ShellPath){UseShellExecute=true})?.Dispose();
}
