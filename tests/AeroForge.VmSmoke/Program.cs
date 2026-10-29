using AeroForge.Core;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;

// This runner deliberately refuses to inject modules outside the dedicated lab.
if (!OperatingSystem.IsWindows() || Environment.MachineName != "AEROFORGE-LAB" || !args.Contains("--i-am-in-test-vm"))
{ Console.Error.WriteLine("Run only inside the dedicated AEROFORGE-LAB VM with --i-am-in-test-vm."); return 2; }
if(args.Contains("--inspect-explorer-modules"))return ExplorerModuleInspector.RunWorker(args);
if(args.Contains("--probe-explorer"))
{
    string ReadArg(string name) => args[Array.IndexOf(args,name)+1];
    string reportPath=Path.GetFullPath(ReadArg("--output"));
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    var inspection=await ExplorerModuleInspector.ReadAsync(ReadArg("--host-root"),reportPath+".worker.json");
    File.WriteAllText(reportPath,JsonSerializer.Serialize(inspection,JsonFile.Options));
    return inspection.TimedOut || inspection.Error!=null || inspection.Processes.Any(p=>p.Error!=null) ? 1 : 0;
}
if(args.Contains("--sounds"))return SoundsSmoke.Run(args);
if(args.Contains("--desktop"))return DesktopSmoke.Run(args);
if(args.Contains("--favorites-namespace"))return NamespaceSmoke.Run(args);
string Arg(string name, string fallback) => Array.IndexOf(args, name) is var n && n >= 0 && n + 1 < args.Length ? args[n + 1] : fallback;
string output = Arg("--output", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AeroForgeLab", "vm-smoke.json"));
var events = new List<object>();
int failures = 0;
try
{
    using var identity = WindowsIdentity.GetCurrent();
    bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    events.Add(new { phase = "environment", machine = Environment.MachineName, elevated, os = WindowsBackend.Inspect(), user = Environment.UserName });
    if (elevated) throw new InvalidOperationException("Run the interactive test task with least privilege.");
    var store = new ContentStore(Arg("--content", @"C:\AeroForgeLab\AeroForge\content"));
    var catalog = store.Load();
    if(args.Contains("--probe-taskbar"))
        foreach(var module in catalog.Modules.Where(m=>m.Id is "classic-taskbar-properties" or "settings-to-control-panel"))
            module.Includes=module.Includes.Append("WinMim.ShellProbe.exe").ToArray();
    var errors = store.VerifyAll(catalog);
    events.Add(new { phase = "verify", errors });
    if (errors.Length != 0) throw new InvalidDataException("Content verification failed");
    foreach (string profile in Arg("--profiles", "seven,vista").Split(','))
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AeroForgeLab", Guid.NewGuid().ToString("N"));
        string hostRoot = Path.Combine(directory, "Host");
        var host = new ShellHost(hostRoot, catalog);
        var transactions = new TransactionManager(new WindowsBackend(hostRoot), Path.Combine(directory, "State"));
        var selected = args.Contains("--modules") ? Arg("--modules", "").Split(',', StringSplitOptions.RemoveEmptyEntries) : null;
        var plan = new ProfilePlanner(store, catalog).Build(profile, selected,privateDisplayProvider:args.Contains("--display-provider")?Arg("--display-provider",""):null);
        events.Add(new { phase = "plan", profile, modules = plan.Modules, blockers = plan.Blockers });
        if (plan.Blockers.Count != 0) throw new InvalidOperationException("Plan blocked");
        var receipt = transactions.Apply(plan);
        try
        {
            string symbols = Arg("--symbols", @"C:\AeroForgeLab\Symbols");
            if (Directory.Exists(symbols))
                foreach (string file in Directory.EnumerateFiles(symbols, "*.pdb", SearchOption.AllDirectories))
                {
                    string target = SafePath.Under(hostRoot, "Data/Engine/Symbols/" + Path.GetRelativePath(symbols, file).Replace('\\', '/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, true);
                }
            host.Start();
            await Task.Delay(TimeSpan.FromSeconds(10));
            using var explorer = Process.Start(new ProcessStartInfo("explorer.exe", @"C:\Windows") { UseShellExecute = true });
            await Task.Delay(TimeSpan.FromSeconds(20));
            var loaded = await ExplorerModuleInspector.ReadAsync(hostRoot, Path.Combine(directory, "explorer-modules.json"));
            var loadedNames = new HashSet<string>(loaded.LoadedNames, StringComparer.OrdinalIgnoreCase);
            bool running = host.IsRunning();
            var missing = catalog.Modules.Where(m => plan.Modules.Contains(m.Id) && m.Includes.Any(i => i is "*" or "explorer.exe") && !m.Excludes.Contains("explorer.exe"))
                .Where(m => !m.Binaries.Any(b => loadedNames.Contains(Path.GetFileName(b.Target)))).Select(m => m.Id).ToArray();
            events.Add(new { phase = "live", profile, running, loaded, missing });
            if (loaded.TimedOut || loaded.Error != null || loaded.Processes.Any(p => p.Error != null)) failures++;
            if (missing.Length != 0) failures++;
            if (!running) { failures++; events.Add(new { phase = "failure", profile, reason = "Host exited" }); }
            if(args.Contains("--probe-taskbar"))
            {
                var module=catalog.Modules.Single(m=>m.Id=="classic-taskbar-properties");
                var start=new ProcessStartInfo(@"C:\AeroForgeLab\WinMim.ShellProbe.exe"){UseShellExecute=false,CreateNoWindow=true};
                foreach(var value in new[]{Path.GetFileName(module.Binaries.Single(b=>b.Target.Contains("/64/")).Target),"ms-settings:taskbar","--hold"})start.ArgumentList.Add(value);
                using var probe=Process.Start(start)??throw new IOException("URI probe did not start");
                await Task.Delay(5000);
                File.WriteAllText(output+".uri-ready.txt","Probe launched; awaiting screenshot");
                await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(65));
                events.Add(new {phase="taskbar-uri",probe.ExitCode});
                if(probe.ExitCode!=0)failures++;
            }
        }
        finally
        {
            await host.StopAsync();
            var reverted = transactions.Rollback(receipt.FilePath);
            string[] remaining = Directory.Exists(hostRoot) ? Directory.GetFiles(hostRoot, "*", SearchOption.AllDirectories) : [];
            string[] remainingManaged = plan.Changes.Where(c => c.Kind == ChangeKind.File).Select(c => SafePath.Under(hostRoot, c.Target)).Where(File.Exists).ToArray();
            events.Add(new { phase = "rollback", profile, reverted.Status, remainingManaged, runtimeArtifacts = remaining });
            if (reverted.Status != "RolledBack" || remainingManaged.Length != 0) failures++;
        }
    }
}
catch (Exception error) { failures++; events.Add(new { phase = "error", error = error.ToString() }); }
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(new { failures, events }, JsonFile.Options));
Console.WriteLine(output);
return failures == 0 ? 0 : 1;
