using System;
using System.IO;
using System.Text.Json;

namespace TetherGuard;

public sealed record Settings(string UsbId = "", string WifiId = "", string Profile = "");
public static class Storage
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TetherGuard");
    public static string Logs => Path.Combine(Folder, "Logs");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder, "settings.json"))) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(settings));
        File.Move(path + ".tmp", path, true);
    }
    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Logs);
            var file = Path.Combine(Logs, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            // Bounded daily files and seven-day retention. No SSIDs, passwords or browsing history.
            if (!File.Exists(file) || new FileInfo(file).Length < 1_048_576)
                File.AppendAllText(file, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            foreach (var old in Directory.EnumerateFiles(Logs, "*.log"))
                if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-7)) File.Delete(old);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
