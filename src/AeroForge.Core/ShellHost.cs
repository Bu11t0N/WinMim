using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Text;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class ShellHost(string hostRoot, Catalog catalog)
{
    private readonly string root = Path.GetFullPath(hostRoot);
    public string Executable => SafePath.Under(root, "windhawk.exe");
    public sealed record ModuleState(int ProcessId,string ModuleId,string Status,int ManagerId=0,long SessionCreated=0);
    public ModuleState[] ExplorerStates()
    {
        string directory=SafePath.Under(root,"Data/Engine/ModsWritable/mod-status");
        if(!Directory.Exists(directory))return [];
        var result=new List<ModuleState>();
        foreach(string file in Directory.EnumerateFiles(directory))
        {
            var parts=Path.GetFileName(file).Split('_',4);
            if(parts.Length!=4 || !int.TryParse(parts[2],out int processId) || !int.TryParse(parts[0],out int managerId) || !long.TryParse(parts[1],out long sessionCreated))continue;
            try
            {
                using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                using var reader=new StreamReader(stream,Encoding.Unicode);
                var value=reader.ReadToEnd().Split('|',2);
                if(value.Length==2 && value[0].Equals("explorer.exe",StringComparison.OrdinalIgnoreCase))
                    result.Add(new(processId,parts[3],value[1],managerId,sessionCreated));
            }
            catch(IOException) { } // Metadata disappears when its owning process exits.
        }
        return result.ToArray();
    }
    public async Task WaitForExplorerAsync(TimeSpan timeout,CancellationToken cancellationToken=default)
    {
        var profile=JsonFile.Read<ActiveProfile>(SafePath.Under(root,"active-profile.json"));
        var expected=catalog.Modules.Where(m=>profile.Modules.Contains(m.Id)
            && m.Includes.Any(i=>i=="*"||i.Equals("explorer.exe",StringComparison.OrdinalIgnoreCase))
            && !m.Excludes.Any(i=>i.Equals("explorer.exe",StringComparison.OrdinalIgnoreCase))).ToArray();
        var deadline=DateTime.UtcNow+timeout;
        string[] pending=[];
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetWindowThreadProcessId(FindWindowW("Shell_TrayWnd",null),out uint processId);
            var loaded=ExplorerStates().Where(s=>s.ProcessId==processId&&ManagedSession(s.ManagerId))
                .GroupBy(s=>s.ModuleId).ToDictionary(g=>g.Key,g=>g.OrderByDescending(s=>s.SessionCreated).First().Status);
            pending=expected.Where(m=>!loaded.TryGetValue(m.ManagedId,out string? status)||status!="Loaded").Select(m=>m.Id).ToArray();
            if(processId!=0 && pending.Length==0){await Task.Delay(250,cancellationToken);return;}
            await Task.Delay(250,cancellationToken);
        }while(DateTime.UtcNow<deadline);
        throw new TimeoutException("Проводник ещё не готов; не завершена загрузка модулей: "+string.Join(", ",pending));
    }
    private bool ManagedSession(int id)
    {
        try
        {
            string? path=WindowsProcessInfo.ImagePath(id);
            return string.Equals(path,Executable,StringComparison.OrdinalIgnoreCase)
                || string.Equals(path,SafePath.Under(root,"windhawk-x64-helper.exe"),StringComparison.OrdinalIgnoreCase);
        }
        catch(System.ComponentModel.Win32Exception){return false;}
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr FindWindowW(string className,string? windowName);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    public bool IsRunning()
    {
        foreach (var p in Process.GetProcessesByName("windhawk"))
        {
            using (p)
                try { if (string.Equals(WindowsProcessInfo.ImagePath(p.Id), Executable, StringComparison.OrdinalIgnoreCase)) return true; }
                catch (System.ComponentModel.Win32Exception) { }
        }
        return false;
    }
    public void EnsureStopped()
    { if (IsRunning()) throw new InvalidOperationException("Сначала остановите профиль. Загруженные модули должны освободить файлы."); }
    public void Start()
    {
        var info = WindowsBackend.Inspect();
        if (!info.KnownBase) throw new InvalidOperationException("Эта сборка рассчитана на Windows 10 x64 19041–19045. Для других версий сначала нужны испытания.");
        if (info.IsAdministrator) throw new InvalidOperationException("Запустите AeroForge без прав администратора: этот движок настроен для пользовательской сессии.");
        if (IsRunning()) return;
        new WindowsBackend(root).CleanupRetiredModules();
        if (Process.GetProcessesByName("windhawk").Length > 0)
            throw new InvalidOperationException("Уже запущен другой Windhawk. Остановите его вручную перед запуском профиля AeroForge.");
        var profile = JsonFile.Read<ActiveProfile>(SafePath.Under(root, "active-profile.json"));
        foreach (var file in catalog.RuntimeFiles.Concat(catalog.Modules.Where(m => profile.Modules.Contains(m.Id)).SelectMany(m => m.Binaries)))
        {
            string path = SafePath.Under(root, file.Target);
            if (!File.Exists(path) || !Hashing.File(path).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Файл движка изменён или отсутствует: " + file.Target);
        }
        if(profile.Modules.Contains("win7-display-control-panel-restorer"))
        {
            // Upstream can remove this disposable cache when unloading.
            string cache=SafePath.Under(root,ExternalDisplayProvider.Target);
            if(File.Exists(cache))ExternalDisplayProvider.Read(cache);
            else
            {
                if(string.IsNullOrEmpty(profile.DisplayProvider))throw new FileNotFoundException("Сначала примените профиль с внешним Display provider.");
                var resource=new Change{Kind=ChangeKind.File,Target=ExternalDisplayProvider.Target,Content=ExternalDisplayProvider.Read(profile.DisplayProvider)};
                new WindowsBackend(root).Write(resource,ValueState.Desired(resource));
            }
        }
        // The persistent GUI host must not inherit a CLI caller's redirected pipes:
        // otherwise PowerShell waits for EOF until Windhawk itself exits.
        var start = new ProcessStartInfo(Executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-tray-only");
        var child = Process.Start(start);
        if(child!=null)
        {
            child.BeginOutputReadLine(); child.BeginErrorReadLine();
            child.Exited+=(_,_)=>child.Dispose();
            child.EnableRaisingEvents=true;
        }
    }
    public async Task StopAsync()
    {
        if (!IsRunning()) { new WindowsBackend(root).CleanupRetiredModules();return; }
        var start = new ProcessStartInfo(Executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-exit");
        using var p = Process.Start(start);
        if (p != null) await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        for (int i = 0; i < 60 && IsRunning(); i++) await Task.Delay(250);
        if (IsRunning()) throw new IOException("Движок ещё работает. Завершите его через значок Windhawk и повторите операцию.");
        new WindowsBackend(root).CleanupRetiredModules();
    }
    private sealed class ActiveProfile
    { public string Profile { get; set; } = ""; public string[] Modules { get; set; } = []; public string? DisplayProvider { get; set; } }
}
