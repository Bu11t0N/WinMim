using AeroForge.Core;
using System.Runtime.Versioning;
using System.Text.Json;

[SupportedOSPlatform("windows")]
internal static class DesktopSmoke
{
    public static int Run(string[] args)
    {
        string Get(string key,string fallback)=>Array.IndexOf(args,key) is var i && i>=0 && i+1<args.Length?args[i+1]:fallback;
        string output=Get("--output",@"C:\AeroForgeLab\desktop-smoke.json");
        var events=new List<object>();int failures=0;
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForgeLab","Desktop",Guid.NewGuid().ToString("N"));
        var controller=new DesktopController(Path.Combine(directory,"Assets"),Path.Combine(directory,"State"));
        var backend=new DesktopBackend(controller.Root);
        try
        {
            DesktopController.EnsureLab();
            string style=@"C:\Windows\Resources\Themes\Aero10\Seven\aero10_seven.msstyles";
            string wallpaper=@"C:\Windows\Resources\Themes\Aero10\Wallpapers\img0.png";
            string button=@"C:\AeroForgeLab\Aero10Source\Extras\Start buttons\7 mid (big top-bottom taskbar)\6805.png";
            var plan=DesktopProfile.BuildSeven(controller.Root,style,wallpaper,button);
            var before=plan.Changes.ToDictionary(c=>c.Identity,c=>backend.Read(c).Fingerprint);
            events.Add(new {phase="before",os=WindowsBackend.Inspect(),changes=plan.Changes.Count,before});
            Receipt? receipt=null;
            try
            {
                receipt=controller.Apply(style,wallpaper,button);
                var initialMismatches=plan.Changes.Where(c=>backend.Read(c).Fingerprint!=ValueState.Desired(c).Fingerprint).Select(c=>c.Identity).ToArray();
                events.Add(new{phase="verify-first-apply",initialMismatches});
                var again=controller.Apply(style,wallpaper,button);
                bool idempotent=receipt.Id==again.Id && controller.History().Count==1;
                var mismatches=plan.Changes.Where(c=>backend.Read(c).Fingerprint!=ValueState.Desired(c).Fingerprint).Select(c=>c.Identity).ToArray();
                events.Add(new{phase="applied",receipt.Id,receipt.Status,idempotent,mismatches});
                if(!idempotent||mismatches.Length!=0)failures++;
                object? shortcutReader=null;
                try
                {
                    shortcutReader=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic reader=shortcutReader!;
                    var resolved=new List<object>();
                    foreach(var entry in plan.Changes.Where(c=>c.Kind==ChangeKind.File&&c.Target.EndsWith(".lnk")))
                    {
                        object link=reader.CreateShortcut(SafePath.Under(controller.Root,entry.Target));
                        try
                        {
                            dynamic shortcut=link;string target=shortcut.TargetPath;
                            bool valid=File.Exists(target)&&string.Equals(Path.GetDirectoryName(target),Environment.GetFolderPath(Environment.SpecialFolder.System),StringComparison.OrdinalIgnoreCase);
                            resolved.Add(new{shortcut=entry.Target,target,valid});if(!valid)failures++;
                        }
                        finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}
                    }
                    events.Add(new{phase="shortcuts",resolved});
                }
                finally{if(shortcutReader!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcutReader);}
            }
            catch(ApplyException error){receipt=error.Receipt;throw;}
            finally
            {
                if(receipt!=null && receipt.Status!="RolledBack")
                {
                    var restored=controller.Restore();
                    events.Add(new{phase="restore",restored.Status});
                    if(restored.Status!="RolledBack")failures++;
                }
                var mismatches=plan.Changes.Where(c=>backend.Read(c).Fingerprint!=before[c.Identity]).Select(c=>c.Identity).ToArray();
                events.Add(new{phase="verify-restored",mismatches,managedFiles=Directory.Exists(controller.Root)?Directory.GetFiles(controller.Root,"*",SearchOption.AllDirectories):[]});
                if(mismatches.Length!=0)failures++;
            }
        }
        catch(Exception error){failures++;events.Add(new{phase="error",error=error.ToString()});}
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output,JsonSerializer.Serialize(new{failures,events},JsonFile.Options));
        return failures==0?0:1;
    }
}
