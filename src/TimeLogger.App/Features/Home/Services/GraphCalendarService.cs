using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public sealed class GraphCalendarService : ICalendarService
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";
    private static readonly string[] Scopes = ["Calendars.Read"];

    private readonly IPublicClientApplication? _msal;
    private readonly HttpClient _http;
    private readonly string _timeZoneId;
    private readonly string? _configurationError;

    public GraphCalendarService(CalendarConfig config, HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        _timeZoneId = string.IsNullOrWhiteSpace(config.TimeZoneId) ? TimeZoneInfo.Local.Id : config.TimeZoneId;

        if (string.IsNullOrWhiteSpace(config.ClientId))
        {
            _configurationError = "Calendar.ClientId is required for Graph calendar integration. Set it in appsettings.json.";
            return;
        }

        _msal = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, string.IsNullOrWhiteSpace(config.TenantId) ? "common" : config.TenantId)
            .WithRedirectUri(string.IsNullOrWhiteSpace(config.RedirectUri) ? "http://localhost" : config.RedirectUri)
            .Build();
    }

    public async Task<IReadOnlyList<CalEvent>> GetEventsAsync(DateTimeOffset start, DateTimeOffset endExclusive)
    {
        // End is exclusive by design so "Today" queries do not leak into next day.
        if (endExclusive <= start)
        {
            throw new ArgumentException("endExclusive must be greater than start.");
        }

        var token = await AcquireAccessTokenAsync();

        var startIso = Uri.EscapeDataString(start.ToString("o", CultureInfo.InvariantCulture));
        var endIso = Uri.EscapeDataString(endExclusive.ToString("o", CultureInfo.InvariantCulture));
        var select = Uri.EscapeDataString("id,subject,start,end,location,organizer,attendees,body,onlineMeeting,webLink,showAs,responseStatus,importance,categories,isAllDay,isCancelled");
        var orderBy = Uri.EscapeDataString("start/dateTime");

        var nextLink =
            $"{GraphBase}/me/calendarView?startDateTime={startIso}&endDateTime={endIso}&$orderby={orderBy}&$top=50&$select={select}";

        var events = new List<CalEvent>();

        while (!string.IsNullOrWhiteSpace(nextLink))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("Prefer", $"outlook.timezone=\"{_timeZoneId}\", outlook.body-content-type=\"text\", IdType=\"ImmutableId\"");

            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(responseStream);

            if (document.RootElement.TryGetProperty("value", out var valueNode) &&
                valueNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var eventNode in valueNode.EnumerateArray())
                {
                    var mapped = TryMapEvent(eventNode);
                    if (mapped is not null)
                    {
                        events.Add(mapped);
                    }
                }
            }

            nextLink = null;
            if (document.RootElement.TryGetProperty("@odata.nextLink", out var nextNode) && nextNode.ValueKind == JsonValueKind.String)
            {
                nextLink = nextNode.GetString();
            }
        }

        return events
            .OrderBy(item => item.Start)
            .ToList();
    }

    private async Task<string> AcquireAccessTokenAsync()
    {
        if (_msal is null)
        {
            throw new InvalidOperationException(_configurationError ?? "Graph calendar is not configured.");
        }

        var accounts = await _msal.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        try
        {
            if (firstAccount is not null)
            {
                var silentResult = await _msal.AcquireTokenSilent(Scopes, firstAccount).ExecuteAsync();
                return silentResult.AccessToken;
            }
        }
        catch (MsalUiRequiredException)
        {
            // Interactive fallback below.
        }

        var interactiveResult = await _msal
            .AcquireTokenInteractive(Scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync();

        return interactiveResult.AccessToken;
    }

    private static CalEvent? TryMapEvent(JsonElement eventNode)
    {
        var subject = eventNode.TryGetProperty("subject", out var subjectNode) && subjectNode.ValueKind == JsonValueKind.String
            ? subjectNode.GetString() ?? "(No title)"
            : "(No title)";

        var location = "No location";
        if (eventNode.TryGetProperty("location", out var locationNode) && locationNode.ValueKind == JsonValueKind.Object)
        {
            if (locationNode.TryGetProperty("displayName", out var displayNameNode) && displayNameNode.ValueKind == JsonValueKind.String)
            {
                var value = displayNameNode.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    location = value;
                }
            }
        }

        if (!TryParseGraphDateTime(eventNode, "start", out var start) ||
            !TryParseGraphDateTime(eventNode, "end", out var end))
        {
            return null;
        }

        return new CalEvent
        {
            Id = StringProperty(eventNode, "id"),
            Subject = subject,
            Location = location,
            Start = start,
            End = end,
            Organizer = Person(eventNode, "organizer"),
            Attendees = AttendeeList(eventNode),
            Description = NestedString(eventNode, "body", "content") ?? "Not provided",
            Status = StringProperty(eventNode, "showAs") ?? "Not provided",
            Response = NestedString(eventNode, "responseStatus", "response") ?? "Not provided",
            Importance = StringProperty(eventNode, "importance") ?? "Not provided",
            Categories = CategoriesList(eventNode),
            IsAllDay = BooleanProperty(eventNode, "isAllDay"),
            IsCancelled = BooleanProperty(eventNode, "isCancelled"),
            JoinUrl = NestedString(eventNode, "onlineMeeting", "joinUrl"),
            WebLink = StringProperty(eventNode, "webLink")
        };
    }

    private static string? StringProperty(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string? NestedString(JsonElement node, string parent, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(parent, out var value)
            ? StringProperty(value, name) : null;

    private static bool BooleanProperty(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Person(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var person) || person.ValueKind != JsonValueKind.Object ||
            !person.TryGetProperty("emailAddress", out var email) || email.ValueKind != JsonValueKind.Object)
            return "Not provided";
        var name = StringProperty(email, "name");
        var address = StringProperty(email, "address");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(address)) return $"{name} <{address}>";
        return name ?? address ?? "Not provided";
    }

    private static string AttendeeList(JsonElement node)
    {
        if (!node.TryGetProperty("attendees", out var attendees) || attendees.ValueKind != JsonValueKind.Array)
            return "Not provided";
        var items = attendees.EnumerateArray().Select(a =>
        {
            var name = "Unknown";
            // Attendees store emailAddress directly, unlike the organizer wrapper.
            if (a.TryGetProperty("emailAddress", out var email) && email.ValueKind == JsonValueKind.Object)
            {
                var display = StringProperty(email, "name");
                var address = StringProperty(email, "address");
                name = !string.IsNullOrWhiteSpace(display) && !string.IsNullOrWhiteSpace(address)
                    ? $"{display} <{address}>" : display ?? address ?? "Unknown";
            }
            var type = StringProperty(a, "type");
            var response = NestedString(a, "status", "response");
            var details = new[] { type, response }.Where(x => !string.IsNullOrWhiteSpace(x));
            var suffix = string.Join(", ", details);
            return string.IsNullOrWhiteSpace(suffix) ? name : $"{name} ({suffix})";
        }).ToArray();
        return items.Length == 0 ? "None listed" : string.Join(Environment.NewLine, items);
    }

    private static string CategoriesList(JsonElement node)
    {
        if (!node.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
            return "None";
        var items = categories.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return items.Length == 0 ? "None" : string.Join(", ", items);
    }

    private static bool TryParseGraphDateTime(JsonElement eventNode, string nodeName, out DateTimeOffset parsed)
    {
        parsed = default;

        if (!eventNode.TryGetProperty(nodeName, out var dateNode) || dateNode.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!dateNode.TryGetProperty("dateTime", out var dateTimeNode) || dateTimeNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var dateTimeRaw = dateTimeNode.GetString();
        if (string.IsNullOrWhiteSpace(dateTimeRaw))
        {
            return false;
        }

        // Graph can return either an offset timestamp or a local timestamp with a separate timezone.
        if (DateTimeOffset.TryParse(dateTimeRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
        {
            return true;
        }

        if (!DateTime.TryParse(dateTimeRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDateTime))
        {
            return false;
        }

        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);

        if (dateNode.TryGetProperty("timeZone", out var timeZoneNode) && timeZoneNode.ValueKind == JsonValueKind.String)
        {
            var timeZoneId = timeZoneNode.GetString();
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    offset = zone.GetUtcOffset(localDateTime);
                }
                catch
                {
                    // Keep local offset if the timezone id is not resolvable on this OS.
                }
            }
        }

        parsed = new DateTimeOffset(localDateTime, offset);
        return true;
    }
}
