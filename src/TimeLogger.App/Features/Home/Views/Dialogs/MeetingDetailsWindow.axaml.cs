using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class MeetingDetailsWindow : Window
{
    private readonly CalEvent _meeting;

    public MeetingDetailsWindow()
    {
        InitializeComponent();
        _meeting = new CalEvent { Subject = "", Location = "", Start = DateTimeOffset.Now, End = DateTimeOffset.Now };
    }

    public MeetingDetailsWindow(CalEvent meeting) : this()
    {
        _meeting = meeting;
        SubjectText.Text = meeting.Subject;
        ScheduleText.Text = $"When: {meeting.Start:dddd, MMMM d, yyyy h:mm tt} – {meeting.End:dddd, MMMM d, yyyy h:mm tt}" + (meeting.IsAllDay ? " (all day)" : "");
        LocationText.Text = $"Location: {meeting.Location}";
        OrganizerText.Text = $"Organizer: {meeting.Organizer}";
        StatusText.Text = $"Availability: {meeting.Status}  |  Response: {meeting.Response}  |  Importance: {meeting.Importance}" + (meeting.IsCancelled ? "  |  Cancelled" : "");
        CategoriesText.Text = $"Categories: {meeting.Categories}";
        AttendeesText.Text = meeting.Attendees;
        DescriptionText.Text = string.IsNullOrWhiteSpace(meeting.Description) ? "Not provided" : meeting.Description;
        JoinButton.IsVisible = IsWebUrl(meeting.JoinUrl);
        OutlookButton.IsVisible = IsWebUrl(meeting.WebLink);
    }

    private static bool IsWebUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private async void OpenUrl(string? value)
    {
        if (!IsWebUrl(value)) return;
        try { Process.Start(new ProcessStartInfo(value!) { UseShellExecute = true }); }
        catch (Exception ex) { await new MessageDialogWindow("Unable to open link", ex.Message, "OK", null).ShowDialog<bool>(this); }
    }

    private void OnJoinClicked(object? sender, RoutedEventArgs e) => OpenUrl(_meeting.JoinUrl);
    private void OnOutlookClicked(object? sender, RoutedEventArgs e) => OpenUrl(_meeting.WebLink);
    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
