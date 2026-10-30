using AeroForge.Core;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Globalization;

// Operator-driven acceptance runner. Protected elevation prompts are handled
// by the VM operator; this runner never automates the secure desktop.
static class XpSmoke
{
    public static async Task<int> Run(string[] args)
    {
        string Arg(string flag,string fallback)=>Array.IndexOf(args,flag) is var i&&i>=0&&i+1<args.Length?args[i+1]:fallback;
        string content=Path.GetFullPath(Arg("--content",Path.Combine(AppContext.BaseDirectory,"content")));
        string output=Path.GetFullPath(Arg("--output",Path.Combine(AppContext.BaseDirectory,"xp-acceptance.json")));
        var events=new List<object>();var timer=Stopwatch.StartNew();var started=DateTimeOffset.Now;
        void Record(string phase)=>events.Add(new{phase,seconds=timer.Elapsed.TotalSeconds,target=Classic95Setup.ActiveTarget});
        try
        {
            DesktopController.EnsureLab();
            if(Classic95Setup.IsActive)throw new InvalidOperationException("Start from a restored baseline.");
            var backend=new DesktopBackend(Path.Combine(Classic95Setup.Data,"Classic95"),preserveStyleMetrics:true);
            var styleChange=new Change{Kind=ChangeKind.VisualStyle,Target="Windows.VisualStyle"};
            var original=XpAppearance.ReadPhysical();var originalStyle=backend.Read(styleChange);
            var setup=new Classic95Setup(content);
            var first=await setup.ApplyThemeAsync("windows-xp");Record("apply");
            var repeated=await setup.ApplyThemeAsync("windows-xp");Record("repeat");
            if(first.Id!=repeated.Id)throw new IOException("Unchanged repeat created a new receipt.");
            await setup.ResumeAsync();Record("resume");
            await setup.ApplyThemeAsync("windows-95");Record("xp-to-95");
            await setup.ApplyThemeAsync("windows-xp");Record("95-to-xp");
            await setup.RestoreAsync();Record("restore");
            bool restored=!Classic95Setup.IsActive&&Process.GetProcessesByName("RetroBar").Length==0
                &&ClassicAppearance.StableValue(ClassicAppearance.Serialize(original))==ClassicAppearance.StableValue(ClassicAppearance.Serialize(XpAppearance.ReadPhysical()))
                &&backend.Read(styleChange).Fingerprint==originalStyle.Fingerprint;
            if(!restored)throw new IOException("Original native appearance, style or taskbar state differs.");
            int crashes=await ExplorerErrorsSince(started);
            if(crashes!=0)throw new IOException("Explorer Application Error events during acceptance: "+crashes);
            JsonFile.Write(output,new{passed=true,explorerErrors=crashes,events});return 0;
        }
        catch(Exception error){JsonFile.Write(output,new{passed=false,error=error.ToString(),events});return 1;}
    }
    public static async Task<int> ExplorerErrorsSince(DateTimeOffset started)
    {
        string script="$ErrorActionPreference='Stop';$since=[datetime]::Parse('"+started.ToString("o",CultureInfo.InvariantCulture)+"',[cultureinfo]::InvariantCulture);try{$events=@(Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000;StartTime=$since} -ErrorAction Stop)}catch{if($_.FullyQualifiedErrorId -like 'NoMatchingEventsFound*'){$events=@()}else{throw}};@($events | Where-Object {$_.Properties[0].Value -ieq 'explorer.exe'}).Count";
        var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string argument in new[]{"-NoProfile","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(script))})info.ArgumentList.Add(argument);
        using var process=Process.Start(info)??throw new IOException("Application event reader did not start.");
        string value=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();
        if(process.ExitCode!=0||!int.TryParse(value.Trim(),out int count))throw new IOException("Application event check failed: "+await process.StandardError.ReadToEndAsync());
        return count;
    }
}
