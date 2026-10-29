using AeroForge.Core;
using System.Diagnostics;
using System.Text.Json;

// The remote loader may be blocked. Keep module enumeration out of the test runner.
internal static class ExplorerModuleInspector
{
    internal sealed record ObservedModule(string Name, string Path);
    internal sealed record ObservedProcess(int Pid, ObservedModule[] Modules, string? Error);
    internal sealed record Snapshot(ObservedProcess[] Processes, bool TimedOut = false, string? Error = null)
    {
        public string[] LoadedNames => Processes.SelectMany(p => p.Modules).Select(m => m.Name).ToArray();
    }

    internal static int RunWorker(string[] args)
    {
        string Arg(string name) => args[Array.IndexOf(args, name) + 1];
        string root = Path.GetFullPath(Arg("--host-root")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var observed = new List<ObservedProcess>();
        foreach (var process in Process.GetProcessesByName("explorer"))
            using (process)
                try
                {
                    var modules = process.Modules.Cast<ProcessModule>()
                        .Where(m => m.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        .Select(m => new ObservedModule(m.ModuleName, m.FileName)).ToArray();
                    observed.Add(new(process.Id, modules, null));
                }
                catch (Exception error) { observed.Add(new(process.Id, [], error.Message)); }
        File.WriteAllText(Arg("--output"), JsonSerializer.Serialize(new Snapshot(observed.ToArray()), JsonFile.Options));
        return 0;
    }

    internal static async Task<Snapshot> ReadAsync(string hostRoot, string output)
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Test executable is unknown");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(ExplorerModuleInspector).Assembly.Location);
        foreach (string argument in new[] { "--i-am-in-test-vm", "--inspect-explorer-modules", "--host-root", hostRoot, "--output", output })
            start.ArgumentList.Add(argument);
        using var worker = Process.Start(start) ?? throw new IOException("Module inspector did not start");
        try { await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
        catch (TimeoutException)
        {
            // Only terminate our own newly created inspector, never the target Explorer.
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            return new([], TimedOut: true, Error: "Explorer module enumeration exceeded 15 seconds");
        }
        if (worker.ExitCode != 0 || !File.Exists(output))
            return new([], Error: "Module inspector failed: " + worker.ExitCode);
        return JsonFile.Read<Snapshot>(output);
    }
}
