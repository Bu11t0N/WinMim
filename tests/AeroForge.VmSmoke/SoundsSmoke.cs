using AeroForge.Core;
using System.Text.Json;
internal static class SoundsSmoke
{
    internal static int Run(string[] args)
    {
        string output=args[Array.IndexOf(args,"--output")+1];var events=new List<object>();int failures=0;
        try
        {
            DesktopController.EnsureLab();
            if(args.Contains("--recover-sound-test"))
            {
                string target=Path.GetFullPath(args[Array.IndexOf(args,"--root")+1]);
                string allowed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForgeLab")+Path.DirectorySeparatorChar;
                if(!target.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(target).StartsWith("sounds-",StringComparison.Ordinal))throw new IOException("Not an owned sound test");
                if(args.Contains("--repair-deleted-marker"))
                {
                    // Only repair the absent first event left by the old test's own
                    // marker cleanup. Never overwrite any nonempty external preference.
                    var repair=new WindowsBackend(target,allowDesktopSettings:true);
                    var journal=new TransactionManager(repair,Path.Combine(target,"State")).History().First();
                    var entry=journal.Entries.Single(e=>e.Change.Target==SevenSounds.EventPaths.First());
                    if(entry.State=="Conflict" && !repair.Read(entry.Change).Exists)repair.Write(entry.Change,entry.Before);
                }
                var restored=new SevenSoundsController(target,Path.Combine(target,"State")).Restore();
                JsonFile.Write(output,new{failures=restored.Status=="RolledBack"?0:1,state=restored.Status});return restored.Status=="RolledBack"?0:1;
            }
            string scratch=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForgeLab","sounds-"+Guid.NewGuid().ToString("N"));
            var backend=new WindowsBackend(scratch,allowDesktopSettings:true);
            var controller=new SevenSoundsController(scratch,Path.Combine(scratch,"State"));
            var settings=SevenSounds.EventPaths.Select(path=>new Change{Kind=ChangeKind.Registry,Target=path,ValueName=""}).ToArray();
            var before=settings.ToDictionary(c=>c.Target,c=>backend.Read(c).Fingerprint);
            var first=controller.Apply();
            try
            {
                foreach(var entry in first.Entries.Where(e=>e.Change.Kind==ChangeKind.Registry))
                    if(backend.Read(entry.Change).Fingerprint!=entry.ExpectedAfter)throw new IOException("Sound event not applied");
                var second=controller.Apply();if(second.Id!=first.Id)throw new IOException("Repeated sounds application created a new original snapshot");
                events.Add(new{phase="apply",first.Status,events=settings.Length,files=first.Entries.Count(e=>e.Change.Kind==ChangeKind.File),idempotent=true});
                var changed=first.Entries.First(e=>e.Change.Kind==ChangeKind.Registry);
                var expected=backend.Read(changed.Change);
                backend.Write(changed.Change,new ValueState{Exists=true,Kind="String",Value="WinMim-smoke-foreign-change"});
                var conflict=controller.Restore();
                bool preserved=backend.Read(changed.Change).Value=="WinMim-smoke-foreign-change";
                events.Add(new{phase="foreign-change",conflict.Status,preserved});
                if(!preserved||conflict.Status!="RollbackIncomplete")throw new IOException("Foreign sound preference was lost");
                // Undo only our test marker; the journal can now return to the exact original state.
                backend.Write(changed.Change,expected);
            }
            finally
            {
                var result=controller.Restore();bool exact=settings.All(c=>backend.Read(c).Fingerprint==before[c.Target]);
                events.Add(new{phase="restore",result.Status,exact});
                if(!exact||result.Status!="RolledBack")failures++;
            }
        }
        catch(Exception error){failures++;events.Add(new{phase="error",error=error.ToString()});}
        File.WriteAllText(output,JsonSerializer.Serialize(new{failures,events},JsonFile.Options));return failures==0?0:1;
    }
}
