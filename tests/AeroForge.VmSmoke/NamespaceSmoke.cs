using AeroForge.Core;
using Microsoft.Win32;
using System.Text.Json;

internal static class NamespaceSmoke
{
    internal static int Run(string[] args)
    {
        string output=args[Array.IndexOf(args,"--output")+1];var events=new List<object>();int failures=0;
        try
        {
            DesktopController.EnsureLab();
            string scratch=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForgeLab","namespace-"+Guid.NewGuid().ToString("N"));
            var backend=new DesktopBackend(scratch);var manager=new TransactionManager(backend,Path.Combine(scratch,"State"));
            var keyChange=new Change{Kind=ChangeKind.RegistryKey,Target=DesktopRegistryPolicy.FavoritesNamespace,ValueKind="RegistryKey",Value="present"};
            var label=new Change{Kind=ChangeKind.Registry,Target=keyChange.Target,ValueName="",ValueKind="String",Value="Favorites"};
            var before=backend.Read(keyChange);var beforeLabel=backend.Read(label);
            var receipt=manager.Apply(new Plan{ProfileId="namespace-test",Changes=[keyChange,label]});
            try
            {
                if(!backend.Read(keyChange).Exists)throw new IOException("Namespace was not created");
                if(!before.Exists)
                {
                    using(var key=Registry.CurrentUser.OpenSubKey(keyChange.Target[5..],true))key!.SetValue("WinMim-Test-Foreign","retained");
                    var conflict=manager.Rollback(receipt.FilePath);
                    using(var key=Registry.CurrentUser.OpenSubKey(keyChange.Target[5..]))
                        if(conflict.Status!="RollbackIncomplete" || (string?)key?.GetValue("WinMim-Test-Foreign")!="retained")throw new IOException("Foreign namespace content was not preserved");
                    events.Add(new{phase="foreign-change",preserved=true});
                }
            }
            finally
            {
                using(var key=Registry.CurrentUser.OpenSubKey(keyChange.Target[5..],true))
                    if((string?)key?.GetValue("WinMim-Test-Foreign")=="retained")key.DeleteValue("WinMim-Test-Foreign");
                var restored=manager.Rollback(receipt.FilePath);
                bool exact=backend.Read(keyChange).Fingerprint==before.Fingerprint && backend.Read(label).Fingerprint==beforeLabel.Fingerprint;
                events.Add(new{phase="restore",restored.Status,exact,originalKeyExisted=before.Exists});
                if(restored.Status!="RolledBack"||!exact)failures++;
            }
        }
        catch(Exception error){failures++;events.Add(new{phase="error",error=error.ToString()});}
        File.WriteAllText(output,JsonSerializer.Serialize(new{failures,events},JsonFile.Options));return failures==0?0:1;
    }
}
