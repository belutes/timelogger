using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TimeLogger.App.Features.Home.Services;

public sealed class AppSettings
{
    public List<string> TaskTitles { get; set; } = ["Tickets", "Meeting", "Power BI", "QA Testing", "Knowledge Base Development"];
    public string Theme { get; set; } = "Dark";
    public bool AllowAssistantAppData { get; set; } = true;
}

public sealed class AppSettingsStore
{
    private readonly string _path;
    public AppSettingsStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeLogger", "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("The settings file is empty.");
        if (settings.TaskTitles is null || settings.TaskTitles.Any(string.IsNullOrWhiteSpace) ||
            settings.TaskTitles.Any(x => string.Equals(x.Trim(), "Other", StringComparison.OrdinalIgnoreCase)) ||
            settings.TaskTitles.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.TaskTitles.Count ||
            settings.Theme is not ("Dark" or "Light" or "System"))
            throw new InvalidDataException("The settings file contains invalid values.");
        settings.TaskTitles = settings.TaskTitles.Select(x => x.Trim()).ToList();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Invalid settings path.");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, _path, true);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }
}
