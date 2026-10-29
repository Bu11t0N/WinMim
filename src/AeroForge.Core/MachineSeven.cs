using Microsoft.Win32;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Diagnostics;

namespace AeroForge.Core;

// Separate elevated adapter: the ordinary profile backend still cannot write HKLM.
[SupportedOSPlatform("windows")]
public sealed class MachineSeven
{
    public const string Explorer=@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer";
    public const string Volume=@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\MTCUVC";
    public const string ShellExplorer=@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";
    private readonly TransactionManager transactions=new(new Backend(),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WinMim","MachineState"));
    public static Plan Plan()=>new(){ProfileId="machine-seven",ProfileName="Системные настройки Seven",Exclusive=true,Changes=
    [new(){Kind=ChangeKind.Registry,Target=Explorer,ValueName="DisableNotificationCenter",ValueKind="DWord",Value="1",Description="Отключить центр уведомлений Windows 10"},
     new(){Kind=ChangeKind.Registry,Target=Volume,ValueName="EnableMtcUvc",ValueKind="DWord",Value="0",Description="Классический регулятор громкости"},
     new(){Kind=ChangeKind.Registry,Target=ShellExplorer,ValueName="HubMode",ValueKind="DWord",Value="1",Description="Скрыть Быстрый доступ при восстановленном Избранном"}]};
    private static void EnsureLab(bool elevated)
    {
        if(Environment.MachineName!="AEROFORGE-LAB")throw new InvalidOperationException("Системный адаптер пока проверяется только в AEROFORGE-LAB.");
        var info=WindowsBackend.Inspect();
        if(!info.KnownBase || elevated&&!info.IsAdministrator)throw new InvalidOperationException("Нужен повышенный токен тестовой Windows 10 x64.");
    }
    public Receipt Apply(){EnsureLab(true);return transactions.Apply(Plan(),allowProfileUpdate:true);}
    public Receipt Restore()
    {
        EnsureLab(true);
        Receipt? result=null;
        while(transactions.History(false).FirstOrDefault(r=>r.Status!="RolledBack") is {} active)
        {
            result=transactions.Rollback(active.FilePath);
            if(result.Status!="RolledBack")return result;
        }
        return result??throw new InvalidOperationException("Нет системных настроек WinMim для возврата.");
    }
    public static Dictionary<string,ValueState> Inspect()
    {
        EnsureLab(false);var backend=new Backend();
        return Plan().Changes.ToDictionary(c=>c.ValueName,c=>backend.Read(c));
    }
    public static bool IsConfigured()
    {
        var current=Inspect();
        return Plan().Changes.All(c=>current[c.ValueName].Fingerprint==ValueState.Desired(c).Fingerprint);
    }
    public async Task<bool> EnsureConfiguredAsync()
    {
        EnsureLab(false);
        if(transactions.History(false).Any(r=>r.Status is not ("Applied" or "RolledBack")))
            throw new InvalidOperationException("Есть незавершённая системная операция WinMim. Сначала верните её настройки.");
        if(IsConfigured())return false;
        await ElevatedAsync("machine-seven-apply");
        if(!IsConfigured())throw new IOException("Системные настройки Seven не прошли проверку.");
        return true;
    }
    public async Task RestoreElevatedAsync()=>await ElevatedAsync("machine-seven-restore");
    private async Task ElevatedAsync(string operation)
    {
        EnsureLab(false);
        string cli=new[]{Path.Combine(AppContext.BaseDirectory,"AeroForge.Cli.exe"),Path.Combine(AppContext.BaseDirectory,"cli","AeroForge.Cli.exe")}.FirstOrDefault(File.Exists)
            ??throw new FileNotFoundException("CLI WinMim не найден рядом с приложением.");
        var start=new ProcessStartInfo(cli){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        start.ArgumentList.Add(operation);start.ArgumentList.Add("--accept-experimental");
        using var child=Process.Start(start)??throw new IOException("Не удалось запустить системный адаптер.");
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        if(child.ExitCode!=0)throw new IOException(transactions.History(false).FirstOrDefault()?.Error??"Системный адаптер завершился с кодом "+child.ExitCode);
    }
    private sealed class Backend:IChangeBackend
    {
        public void Validate(Change c)
        {
            bool allowed=c.Kind==ChangeKind.Registry && (c.Target.Equals(Explorer,StringComparison.OrdinalIgnoreCase)&&c.ValueName=="DisableNotificationCenter"
                || c.Target.Equals(Volume,StringComparison.OrdinalIgnoreCase)&&c.ValueName=="EnableMtcUvc"
                || c.Target.Equals(ShellExplorer,StringComparison.OrdinalIgnoreCase)&&c.ValueName=="HubMode");
            if(!allowed)throw new InvalidDataException("Системная настройка вне списка WinMim.");
        }
        public ValueState Read(Change c)
        {
            Validate(c);
            using var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
            using var key=hive.OpenSubKey(c.Target[5..]);
            if(key==null || !key.GetValueNames().Contains(c.ValueName,StringComparer.OrdinalIgnoreCase))return new();
            var kind=key.GetValueKind(c.ValueName);var value=key.GetValue(c.ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
            string text=kind switch{RegistryValueKind.Binary=>Convert.ToBase64String((byte[])value!),RegistryValueKind.MultiString=>JsonSerializer.Serialize((string[])value!),_=>Convert.ToString(value,CultureInfo.InvariantCulture)??""};
            return new(){Exists=true,Kind=kind.ToString(),Value=text};
        }
        public void Write(Change c,ValueState state)
        {
            EnsureLab(true);Validate(c);
            using var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
            if(!state.Exists){using var existing=hive.OpenSubKey(c.Target[5..],true);existing?.DeleteValue(c.ValueName,false);return;}
            var kind=Enum.Parse<RegistryValueKind>(state.Kind);
            object value=kind switch{RegistryValueKind.DWord=>int.Parse(state.Value!,CultureInfo.InvariantCulture),RegistryValueKind.QWord=>long.Parse(state.Value!,CultureInfo.InvariantCulture),RegistryValueKind.Binary=>Convert.FromBase64String(state.Value!),RegistryValueKind.MultiString=>JsonSerializer.Deserialize<string[]>(state.Value!)!,_=>state.Value??""};
            using var key=hive.CreateSubKey(c.Target[5..],true);key.SetValue(c.ValueName,value,kind);
        }
    }
}
