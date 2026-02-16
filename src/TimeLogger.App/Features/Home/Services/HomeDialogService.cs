using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Views.Dialogs;

namespace TimeLogger.App.Features.Home.Services;

public sealed class HomeDialogService(Window owner) : IHomeDialogService
{
    public async Task ShowAlertAsync(string title, string message)
    {
        var dialog = new MessageDialogWindow(title, message, "OK", null);
        await dialog.ShowDialog<bool>(owner);
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        var dialog = new MessageDialogWindow(title, message, confirmText, cancelText);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<string?> ShowCustomTaskPromptAsync()
    {
        var dialog = new CustomTaskDialogWindow();
        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<string?> ShowTextInputAsync(string title, string prompt, string watermark, string initialValue = "")
    {
        var dialog = new TextInputDialogWindow(title, prompt, watermark, initialValue);
        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<DateRangeInput?> ShowDateRangePromptAsync(DateTime defaultStartDate, DateTime defaultEndDate)
    {
        var dialog = new DateRangeDialogWindow(defaultStartDate, defaultEndDate);
        return await dialog.ShowDialog<DateRangeInput?>(owner);
    }

    public async Task<EditEntryInput?> ShowEditEntryAsync(WorkEntry entry, IReadOnlyList<string> timeOptions)
    {
        var dialog = new EditEntryDialogWindow(entry, timeOptions);
        return await dialog.ShowDialog<EditEntryInput?>(owner);
    }
}
