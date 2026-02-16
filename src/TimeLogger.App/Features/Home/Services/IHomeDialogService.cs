using System.Collections.Generic;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public interface IHomeDialogService
{
    Task ShowAlertAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel");
    Task<string?> ShowCustomTaskPromptAsync();
    Task<string?> ShowTextInputAsync(string title, string prompt, string watermark, string initialValue = "");
    Task<EditEntryInput?> ShowEditEntryAsync(
        WorkEntry entry,
        IReadOnlyList<string> timeOptions);
}
