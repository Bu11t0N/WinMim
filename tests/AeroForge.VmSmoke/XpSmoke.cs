using AeroForge.Core;
using System.Diagnostics;
using System.Text.Json;

// Operator-driven acceptance runner. Protected elevation prompts are handled
// by the VM operator; this runner never automates the secure desktop.
static class XpSmoke
{
    public static async Task<int> Run(string[] args)
    {
        string Arg(string flag,string fallback)=>Array.IndexOf(args,flag) is var i&&i>=0&&i+1<args.Length?args[i+1]:fallback;
        string content=Path.GetFullPath(Arg("--content",Path.Combine(AppContext.BaseDirectory,"content")));
        string output=Path.GetFullPath(Arg("--output",Path.Combine(AppContext.BaseDirectory,"xp-acceptance.json")));
        var events=new List<object>();var timer=Stopwatch.StartNew();
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
            JsonFile.Write(output,new{passed=true,events});return 0;
        }
        catch(Exception error){JsonFile.Write(output,new{passed=false,error=error.ToString(),events});return 1;}
    }
}
