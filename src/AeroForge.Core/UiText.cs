using System.Globalization;
using System.Text.Json;

namespace AeroForge.Core;

public static class UiText
{
    private static readonly Lazy<Dictionary<string, string>> Russian = new(() => LoadResource("ru"));
    private static readonly Lazy<Dictionary<string, string>> English = new(() => LoadResource("en"));
    private static readonly string Preference = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AeroForge", "ui-language.json");

    public static string Language { get; private set; } = "ru";

    public static void LoadLanguage(string? location = null)
    {
        string path = location ?? Preference;
        try { Language = File.Exists(path) && JsonFile.Read<string>(path) == "en" ? "en" : "ru"; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { Language = "ru"; }
    }

    public static void SaveLanguage(string language, string? location = null)
    {
        if (language is not ("ru" or "en")) throw new ArgumentOutOfRangeException(nameof(language));
        JsonFile.Write(location ?? Preference, language);
    }

    public static string Get(string key)
    {
        if (Language == "en" && English.Value.TryGetValue(key, out var english)) return english;
        return Russian.Value.GetValueOrDefault(key, key);
    }

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo(Language), Get(key), arguments);

    private static Dictionary<string, string> LoadResource(string language)
    {
        using var resource = typeof(UiText).Assembly.GetManifestResourceStream($"AeroForge.Core.Localization.{language}.json")
            ?? throw new InvalidOperationException("WinMim language resource missing: " + language);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(resource)
            ?? throw new InvalidDataException("WinMim language resource is empty: " + language);
    }
}
