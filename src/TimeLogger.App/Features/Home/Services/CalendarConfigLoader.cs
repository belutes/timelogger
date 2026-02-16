using System;
using System.IO;
using System.Text.Json;

namespace TimeLogger.App.Features.Home.Services;

public static class CalendarConfigLoader
{
    public static CalendarConfig Load()
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(localPath))
        {
            localPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "TimeLogger.App", "appsettings.json");
        }

        if (!File.Exists(localPath))
        {
            return new CalendarConfig { Provider = "Fake" };
        }

        try
        {
            using var stream = File.OpenRead(localPath);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("Calendar", out var calendarNode))
            {
                return new CalendarConfig { Provider = "Fake" };
            }

            var provider = ReadString(calendarNode, "Provider") ?? "Graph";
            var clientId = ReadString(calendarNode, "ClientId") ?? string.Empty;
            var tenantId = ReadString(calendarNode, "TenantId") ?? "common";
            var redirectUri = ReadString(calendarNode, "RedirectUri") ?? "http://localhost";
            var timeZoneId = ReadString(calendarNode, "TimeZoneId") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                timeZoneId = TimeZoneInfo.Local.Id;
            }

            return new CalendarConfig
            {
                Provider = provider,
                ClientId = clientId,
                TenantId = tenantId,
                RedirectUri = redirectUri,
                TimeZoneId = timeZoneId
            };
        }
        catch
        {
            return new CalendarConfig { Provider = "Fake" };
        }
    }

    private static string? ReadString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
