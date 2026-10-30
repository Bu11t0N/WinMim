using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AeroForge.Core;

// Each field is a logical file value in the journal. Upstream may reformat the
// JSON or add default fields; those unrelated changes must not block restoration.
public sealed class RetroBarSettingsBackend(string path):IChangeBackend
{
    public static readonly string[] Fields=["Theme","ShowQuickLaunch","QuickLaunchPath","ShowInputLanguage","CollapseNotifyIcons","CheckForUpdates","ShowDesktopButton","TaskbarScale","ShowMultiMon","AutoHide"];
    public void Validate(Change c){if(c.Kind!=ChangeKind.File||!Fields.Contains(c.Target))throw new InvalidDataException("Unknown RetroBar setting");if(c.Content!=null)using(JsonDocument.Parse(c.Content)){} }
    private JsonObject Load()=>File.Exists(path)?JsonNode.Parse(File.ReadAllText(path)) as JsonObject??throw new InvalidDataException("Invalid RetroBar settings"):new();
    public ValueState Read(Change c){Validate(c);var json=Load();return json.ContainsKey(c.Target)?new(){Exists=true,Kind="File",Bytes=JsonSerializer.SerializeToUtf8Bytes(json[c.Target])}:new();}
    public void Write(Change c,ValueState state){Validate(c);var json=Load();if(state.Exists){if(state.Kind!="File")throw new InvalidDataException("Invalid JSON snapshot");json[c.Target]=JsonNode.Parse(state.Bytes!);}else json.Remove(c.Target);if(json.Count==0){if(File.Exists(path))File.Delete(path);}else JsonFile.Write(path,json);}
    public static Plan Plan(ClassicTheme theme,string quickLaunch)
    {
        var p=new Plan{ProfileId="retrobar-settings",ProfileName="Параметры классической панели",Exclusive=true};
        var values=new Dictionary<string,object>{{"Theme",theme.Taskbar},{"ShowQuickLaunch",theme.QuickLaunch},{"QuickLaunchPath",quickLaunch},{"ShowInputLanguage",true},{"CollapseNotifyIcons",theme.Xp},{"CheckForUpdates",false},{"ShowDesktopButton",false},{"TaskbarScale",1},{"ShowMultiMon",false},{"AutoHide",false}};
        foreach(var (key,value) in values)p.Changes.Add(new(){Kind=ChangeKind.File,Target=key,Content=JsonSerializer.SerializeToUtf8Bytes(value),Description="RetroBar: "+key});return p;
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class RetroBarHost
{
    public const string ExecutableHash="bf3fda3e31a1839482bd3ec355cd9fffebf27d3ce378a8c0f890670e24af7d1a";
    public const string LicenseHash="3413530d8a5a30ad46de082ea70ff0e2195565499393d5156635a4787f80d215";
    public static string SettingsPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RetroBar","settings.json");
    private static string Executable(string root)=>SafePath.Under(root,"Runtime/RetroBar/RetroBar.exe");
    public static void AddFiles(Plan plan,string content)
    {
        foreach(var (name,hash) in new[]{("RetroBar.exe",ExecutableHash),("License.txt",LicenseHash)})
        {
            string path=SafePath.Under(content,"classic-runtime/retrobar/"+name);
            if(!File.Exists(path)||Hashing.Bytes(File.ReadAllBytes(path))!=hash)throw new InvalidDataException("Подготовьте проверенный RetroBar: python tools/fetch_classic_runtime.py");
            plan.Changes.Add(new(){Kind=ChangeKind.File,Target="Runtime/RetroBar/"+name,Content=File.ReadAllBytes(path),Description="Классическая панель RetroBar: "+name});
        }
    }
    public static void EnsureNoForeignInstance(string root)
    {
        foreach(var p in Process.GetProcessesByName("RetroBar"))using(p)
            if(!string.Equals(WindowsProcessInfo.ImagePath(p.Id),Executable(root),StringComparison.OrdinalIgnoreCase))throw new IOException("Уже работает отдельный RetroBar. Завершите его перед применением.");
    }
    public static async Task StopAsync(string root)
    {
        EnsureNoForeignInstance(root);
        foreach(var p in Process.GetProcessesByName("RetroBar"))using(p)
        {
            p.CloseMainWindow();try{await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));}
            catch(TimeoutException){p.Kill();await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));}
        }
    }
    public static async Task StartAsync(string root)
    {
        EnsureNoForeignInstance(root);if(Process.GetProcessesByName("RetroBar").Length!=0)return;
        string path=Executable(root);if(!File.Exists(path)||Hashing.Bytes(File.ReadAllBytes(path))!=ExecutableHash)throw new IOException("RetroBar отсутствует или изменён");
        using var p=Process.Start(new ProcessStartInfo(path){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path)!,CreateNoWindow=true})??throw new IOException("Не удалось запустить RetroBar");
        // A process handle alone is not readiness. Wait for its taskbar window.
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(DateTime.UtcNow<deadline){p.Refresh();if(p.HasExited)throw new IOException("RetroBar завершился при запуске");if(p.MainWindowHandle!=IntPtr.Zero)return;await Task.Delay(200);}
        throw new TimeoutException("Панель RetroBar не появилась");
    }
}
