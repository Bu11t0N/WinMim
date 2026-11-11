using AeroForge.Core;
using System.Diagnostics;

const string folder=@"C:\AeroForgeLab\Windows2000";
DesktopController.EnsureLab();
if(!args.Contains("--i-am-in-w2000-clone")||!File.Exists(folder+@"\w2000-clone.marker"))return 2;
var progress=new ProgressLog(folder+@"\progress.log");
async Task Switch(bool restore)
{
    using var p=Process.Start(new ProcessStartInfo("schtasks.exe","/Run /TN WinMim-W2000-Session-"+(restore?"Restore":"Apply")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})!;
    await p.WaitForExitAsync();if(p.ExitCode!=0)throw new IOException(await p.StandardError.ReadToEndAsync());
    var deadline=DateTime.UtcNow.AddSeconds(30);
    while(DateTime.UtcNow<deadline){if(new ClassicSession(ClassicSession.ActiveSession).Inspect().Value!.Contains("(D;;LC;;;IU)")!=restore)return;await Task.Delay(100);}
    throw new TimeoutException("Classic session broker");
}
var setup=new Classic95Setup(folder+@"\content",Switch);
try
{
    if(args.Contains("--restore")){
        await setup.RestoreAsync(progress);
        var baseline=JsonFile.Read<ClassicAppearanceState>(folder+@"\baseline.json");
        bool nativeRestored=ClassicAppearance.StableValue(ClassicAppearance.Serialize(baseline))==ClassicAppearance.StableValue(ClassicAppearance.Serialize(ClassicAppearance.Read()));
        int panels=Process.GetProcessesByName("RetroBar").Length;
        JsonFile.Write(folder+@"\restored.json",new{active=Classic95Setup.ActiveTarget,retrobar=panels,nativeRestored});
        return !Classic95Setup.IsActive&&panels==0&&nativeRestored?0:1;
    }
    if(Classic95Setup.IsActive)await setup.RestoreAsync(progress);
    var before=ClassicAppearance.Read();JsonFile.Write(folder+@"\baseline.json",before);
    var results=new List<object>();
    foreach(string target in args.Contains("--cycle")?new[]{"windows-2000","windows-2000","windows-95","windows-2000"}:new[]{"windows-2000"})
    {
        var watch=Stopwatch.StartNew();await setup.ApplyThemeAsync(target,progress);watch.Stop();
        if(Classic95Setup.ActiveTarget!=target)throw new IOException("Wrong active target");
        int panels=Process.GetProcessesByName("RetroBar").Length;
        if(panels!=(target=="windows-95"?0:1))throw new IOException("Wrong taskbar count");
        var expected=ClassicAppearance.Desired(before,target);
        if(ClassicAppearance.StableValue(ClassicAppearance.Serialize(expected))!=ClassicAppearance.StableValue(ClassicAppearance.Serialize(ClassicAppearance.Read())))throw new IOException("Native appearance mismatch");
        results.Add(new{target,applyMilliseconds=watch.ElapsedMilliseconds,panels});JsonFile.Write(folder+@"\cycle-progress.json",results);
    }
    if(args.Contains("--hold"))
    {
        string demo=folder+@"\FileCommands";Directory.CreateDirectory(demo+@"\Child");File.WriteAllText(demo+@"\sample.txt","Windows 2000 file-command test\r\n");
        Process.Start(new ProcessStartInfo(folder+@"\WinMim.exe"){UseShellExecute=true})?.Dispose();
        Process.Start(new ProcessStartInfo("explorer.exe",demo){UseShellExecute=true})?.Dispose();
        JsonFile.Write(folder+@"\held.json",new{active=Classic95Setup.ActiveTarget,results});return 0;
    }
    await setup.RestoreAsync(progress);
    bool restored=ClassicAppearance.StableValue(ClassicAppearance.Serialize(before))==ClassicAppearance.StableValue(ClassicAppearance.Serialize(ClassicAppearance.Read()));
    if(!restored||Classic95Setup.IsActive||Process.GetProcessesByName("RetroBar").Length!=0)throw new IOException("Baseline restore mismatch");
    JsonFile.Write(folder+@"\complete.json",new{restored,results});return 0;
}
catch(Exception error){JsonFile.Write(folder+@"\error.json",new{error=error.ToString()});return 1;}

sealed class ProgressLog(string path):IProgress<string>
{
    private readonly object sync=new();
    public void Report(string message)
    {
        lock(sync)try{using var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);using var writer=new StreamWriter(stream);writer.WriteLine(message);}
        catch(IOException){/* Progress capture must never terminate a transaction. */}
    }
}
