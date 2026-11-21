using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class ThemePreparation(string content)
{
    private const string OpenShellMsi = "OpenShellSetup64_4_4_198.msi";
    private const string OpenShellMsiHash = "e3f7f41fe718d01a80f2af7f8d6f6c50aded56e1ea80f85d83eab03ac0cdd493";
    private static readonly string State = Path.Combine(Classic95Setup.Data, "Preparation");
    private static readonly string Cache = Path.Combine(State, "Downloads");
    private static readonly string SystemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
    private const string OpenShellSettings = @"Software\OpenShell\StartMenu\Settings";
    internal static string OpenShellExecutable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Open-Shell", "StartMenu.exe");

    public static string[] Components(AppearanceTarget theme) => theme.Mode switch
    {
        AppearanceMode.Seven => ["Open-Shell 4.4.198", "SecureUxTheme 4.0.0", "OpenGlass и ресурсы Seven — требуется подтверждённый способ подготовки"],
        AppearanceMode.Classic when theme.Id == "windows-xp" => ["Open-Shell 4.4.198", "SecureUxTheme 4.0.0", "Личный пакет Luna Blue — подключается пользователем при наличии прав"],
        AppearanceMode.Classic when theme.Id == "windows-95" => ["Open-Shell 4.4.198"],
        AppearanceMode.Classic => ["Open-Shell 4.4.198", "RetroBar — входит в WinMim"],
        _ => []
    };

    public static string DisabledReason()
    {
        try
        {
            DesktopController.EnsureLab();
            if (RuntimeInformation.OSArchitecture != Architecture.X64 || !Environment.Is64BitProcess ||
                ClassicSession.ActiveSession != Process.GetCurrentProcess().SessionId)
                return "Нужен обычный локальный сеанс Windows x64.";
            return "";
        }
        catch (InvalidOperationException error) { return error.Message; }
    }

    public async Task PrepareAsync(string target, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var theme = AppearanceCatalog.Select(target).Target;
        if (!theme.Available) throw new InvalidOperationException("Подготовка для этой темы пока недоступна.");
        string disabled = DisabledReason();
        if (disabled.Length != 0) throw new InvalidOperationException(disabled);
        using var operation = await ClassicOperationLock.AcquireAsync(Classic95Setup.Data, cancellationToken);
        if (Classic95Setup.HasRecoveryWork || new DesktopController().HasRecoveryWork)
            throw new InvalidOperationException("Перед установкой зависимостей верните прежнее оформление.");

        await PrepareDependencyAsync("open-shell", OpenShellReady, progress, cancellationToken);
        if (theme.Mode == AppearanceMode.Seven || theme.Id == "windows-xp")
            await PrepareDependencyAsync("secureuxtheme", () => SecureUxReady(content), progress, cancellationToken);
    }

    internal static bool OpenShellReady()
    {
        RequireRestartCompleted("open-shell");
        if (!File.Exists(OpenShellExecutable)) return false;
        var version = FileVersionInfo.GetVersionInfo(OpenShellExecutable);
        if ((version.FileMajorPart, version.FileMinorPart, version.FileBuildPart) != (4, 4, 198))
            throw new InvalidOperationException("Установлена другая версия Open-Shell. WinMim оставит её и настройки без изменений. Для этой сборки проверяется 4.4.198.");
        return true;
    }

    internal static bool SecureUxReady(string content)
    {
        RequireRestartCompleted("secureuxtheme");
        try { XpRuntime.VerifyInstalled(content); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private async Task PrepareDependencyAsync(string id, Func<bool> isReady, IProgress<string>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        progress?.Report($"Проверяется {id}…");
        if (isReady())
        {
            progress?.Report($"{id}: совместимая версия уже установлена.");
            return;
        }
        bool shell = id == "open-shell";
        if (!shell && new[] { "SecureUxTheme.dll", "SecureUxTheme4.dll" }.Any(name => File.Exists(Path.Combine(SystemDirectory, name))))
            throw new InvalidOperationException("Обнаружен другой SecureUxTheme. Автоматическая миграция не подтверждена. Установленный компонент сохранён.");
        if (shell && OpenShellRegistered())
            throw new InvalidOperationException("Обнаружена другая или неполная установка Open-Shell. Автоматическое обновление не выполняется.");

        var package = ReadApprovedDependency(id);
        string downloaded;
        try { downloaded = await new PackageCache(Cache).DownloadAsync(package, progress, token); }
        catch (HttpRequestException error) { throw new IOException($"{package.Name}: нет доступа к официальному источнику. Повторите при доступной сети. Проверенный кеш работает без сети.", error); }
        catch (TaskCanceledException error) when (!token.IsCancellationRequested) { throw new IOException($"{package.Name}: истекло время загрузки. Можно повторить подготовку.", error); }
        token.ThrowIfCancellationRequested();

        Directory.CreateDirectory(State);
        Guid boot = BootIdentifier();
        string staging = Path.Combine(State, "install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            string msi = downloaded;
            string msiHash = package.Sha256;
            if (shell)
            {
                // The bootstrapper runs without elevation. Only the pinned extracted MSI reaches Windows Installer.
                using var source = PackageCache.OpenVerified(downloaded, package.Sha256);
                var extract = new ProcessStartInfo(downloaded) { WorkingDirectory = staging, UseShellExecute = false, CreateNoWindow = true };
                extract.ArgumentList.Add("extract64");
                using var extractor = Process.Start(extract) ?? throw new IOException("Не удалось распаковать MSI Open-Shell.");
                await extractor.WaitForExitAsync();
                if (extractor.ExitCode != 0) throw new IOException("Open-Shell не смог распаковать x64 MSI.");
                msi = SafePath.Under(staging, OpenShellMsi);
                msiHash = OpenShellMsiHash;
            }

            using var verified = PackageCache.OpenVerified(msi, msiHash);
            token.ThrowIfCancellationRequested();
            if (shell) CheckOpenShellStartup();
            progress?.Report($"{package.Name}: Windows может запросить права администратора. После запуска установщика дождитесь его завершения.");
            var start = new ProcessStartInfo(Path.Combine(SystemDirectory, "msiexec.exe"))
            { Verb = "runas", UseShellExecute = true, WorkingDirectory = SystemDirectory };
            foreach (string argument in new[] { "/i", msi, "/quiet", "/norestart" }) start.ArgumentList.Add(argument);
            if (shell)
            {
                start.ArgumentList.Add("ADDLOCAL=StartMenu");
                start.ArgumentList.Add("NOSTART=1");
            }
            Process installer;
            try { installer = Process.Start(start) ?? throw new IOException("Windows Installer не запустился."); }
            catch (Win32Exception error) when (error.NativeErrorCode == 1223)
            { throw new OperationCanceledException("Подтверждение UAC отменено. Проверенный пакет сохранён для повтора.", error); }
            using (installer)
            {
                await installer.WaitForExitAsync();
                if (installer.ExitCode is 3010 or 1641)
                {
                    JsonFile.Write(RestartMarker(id), boot);
                    if (shell) SetOpenShellStartupDefault();
                    throw new InvalidOperationException($"{package.Name}: для завершения требуется перезагрузка. Перезагрузите Windows в удобное время и проверьте требования.");
                }
                if (installer.ExitCode == 1602) throw new OperationCanceledException("Установка отменена. Проверенный пакет сохранён для повтора.");
                if (installer.ExitCode != 0) throw new IOException($"{package.Name}: Windows Installer завершился с кодом {installer.ExitCode}. Можно повторить подготовку.");
            }
            if (!isReady()) throw new IOException($"{package.Name}: после установки ожидаемый компонент не обнаружен.");
            if (shell) SetOpenShellStartupDefault();
            progress?.Report($"{package.Name}: установленная версия проверена.");
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            foreach (var name in new[] { OpenShellMsi })
            {
                string item = SafePath.Under(staging, name);
                try { File.Delete(item); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            try { Directory.Delete(staging); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private Dependency ReadApprovedDependency(string id)
    {
        // The installed lock and the runtime catalog must agree. Only approved adapters can install a package.
        string root = Path.GetDirectoryName(content) ?? throw new InvalidDataException("Отсутствует корень WinMim.");
        var pinned = JsonFile.Read<DependencyLock>(SafePath.Under(root, "third-party.lock.json")).OptionalDependencies
            .SingleOrDefault(item => item.Id == id) ?? throw new InvalidDataException("Поставщик не утверждён в lock-файле.");
        var catalog = new ContentStore(content).Load().Dependencies.Single(item => item.Id == id);
        string repo = id == "open-shell" ? "Open-Shell/Open-Shell-Menu" : "namazso/SecureUxTheme";
        string version = id == "open-shell" ? "4.4.198" : "4.0.0";
        string name = id == "open-shell" ? "OpenShellSetup_4_4_198.exe" : "SecureUxTheme_x64.msi";
        string hash = id == "open-shell" ? "a4d2d4459de55b5e962ba2a14f7bb794170511649138173dfa72949837b48c3f"
            : "af6ab67e0a283a0138b827b6731d5bb1f4ffc59e6d905f1c41567eb3305a9537";
        if (pinned.SourceUrl != $"https://github.com/{repo}" || pinned.Version != version || pinned.FileName != name ||
            pinned.DownloadUrl != $"{pinned.SourceUrl}/releases/download/v{version}/{name}" || catalog.Version != version ||
            catalog.DownloadUrl != pinned.DownloadUrl || catalog.Sha256 != pinned.Sha256 || pinned.Sha256 != hash || catalog.FileName != name)
            throw new InvalidDataException("Метаданные пакета не совпадают с утверждённым каналом.");
        return pinned;
    }

    private static bool OpenShellRegistered()
    {
        foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
        {
            using var hive = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view);
            using var uninstall = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (string name in uninstall.GetSubKeyNames())
            {
                using var product = uninstall.OpenSubKey(name);
                if (product?.GetValue("DisplayName") is string display && display.StartsWith("Open-Shell", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    // StartMenu registers an autorun. NOSTART suppresses immediate launch, and -autorun
    // honors the profile's AutoStart value on subsequent logons.
    private static void CheckOpenShellStartup()
    {
        using var settings = Registry.CurrentUser.OpenSubKey(OpenShellSettings);
        var existing = settings?.GetValue("AutoStart");
        if (existing is not null and not 0)
            throw new InvalidOperationException("Найдено пользовательское значение автозапуска Open-Shell. WinMim сохраняет его; автоматическая установка остановлена.");
    }

    private static void SetOpenShellStartupDefault()
    {
        using var settings = Registry.CurrentUser.CreateSubKey(OpenShellSettings);
        if (settings.GetValue("AutoStart") == null) settings.SetValue("AutoStart", 0, RegistryValueKind.DWord);
    }

    private static string RestartMarker(string id) => SafePath.Under(State, id + ".restart.json");
    private static void RequireRestartCompleted(string id)
    {
        string path = RestartMarker(id);
        if (File.Exists(path) && JsonFile.Read<Guid>(path) == BootIdentifier())
            throw new InvalidOperationException($"{id}: перезагрузите Windows, затем повторите проверку требований.");
    }

    // The boot identity persists across process restarts and changes after a full reboot.
    private static Guid BootIdentifier()
    {
        byte[] info = new byte[32];
        if (NtQuerySystemInformation(90, info, info.Length, out _) != 0)
            throw new IOException("Невозможно определить, была ли Windows перезагружена.");
        return new Guid(info.AsSpan(0, 16));
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int informationClass, [Out] byte[] data, int size, out int written);

    private sealed class DependencyLock
    {
        public Dependency[] OptionalDependencies { get; set; } = [];
    }
}
