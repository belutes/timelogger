using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace TimeLogger.App.Features.Home.ViewModels;

public sealed partial class HomeViewModel
{
    private async Task<bool> SaveCurrentDateEntriesAsync()
    {
        if (_dialogs is null)
        {
            return false;
        }

        if (!await EnsureRecordsDirectoryAsync(
                $"Create '{_storage.RecordsDirectoryPath}' to store time log records?",
                "Save Cancelled",
                "Entry was not saved because the records folder was not created."))
        {
            return false;
        }

        var date = (SelectedDate ?? DateTime.Today).Date;
        await _storage.SaveEntriesForDateAsync(date, WorkEntries.ToList());
        return true;
    }

    private async Task<bool> EnsureRecordsDirectoryAsync(string confirmationMessage, string cancelTitle, string cancelMessage)
    {
        if (_dialogs is null)
        {
            return false;
        }

        if (_storage.RecordsDirectoryExists())
        {
            return true;
        }

        var shouldCreate = await _dialogs.ShowConfirmationAsync(
            "Create Records Folder",
            confirmationMessage,
            "Create",
            "Cancel");

        if (!shouldCreate)
        {
            await _dialogs.ShowAlertAsync(cancelTitle, cancelMessage);
            return false;
        }

        _storage.CreateRecordsDirectory();
        return true;
    }

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var formats = new[] { "h:mm tt", "hh:mm tt" };
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        time = parsed.TimeOfDay;
        return true;
    }

    private static IEnumerable<string> BuildTimeOptions()
    {
        var time = DateTime.Today;
        for (var i = 0; i < 96; i++)
        {
            yield return time.AddMinutes(i * 15).ToString("hh:mm tt");
        }
    }

    private void SetDefaultWorkdayTimes()
    {
        _isUpdatingTimes = true;
        SelectedStartTime = "08:00 AM";
        SelectedEndTime = "08:15 AM";
        _isUpdatingTimes = false;
    }

    private void AdvanceTimeAfterAdd(TimeSpan endTime)
    {
        var nextStart = DateTime.Today.Add(endTime).ToString("hh:mm tt");

        _isUpdatingTimes = true;
        SelectedStartTime = nextStart;
        _isUpdatingTimes = false;

        if (TryGetNextTimeOption(nextStart, out var nextEnd))
        {
            SelectedEndTime = nextEnd;
            return;
        }

        SelectedEndTime = nextStart;
    }

    private bool TryGetNextTimeOption(string current, out string next)
    {
        next = string.Empty;
        var index = TimeOptions.IndexOf(current);
        if (index < 0)
        {
            return false;
        }

        var nextIndex = index + 1;
        if (nextIndex >= TimeOptions.Count)
        {
            return false;
        }

        next = TimeOptions[nextIndex];
        return true;
    }
}
