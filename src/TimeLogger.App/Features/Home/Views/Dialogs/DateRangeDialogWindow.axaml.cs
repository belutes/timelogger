using System;
using Avalonia.Controls;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class DateRangeDialogWindow : Window
{
    public DateRangeDialogWindow()
    {
        InitializeComponent();
    }

    public DateRangeDialogWindow(DateTime defaultStartDate, DateTime defaultEndDate)
        : this()
    {
        StartDatePicker.SelectedDate = defaultStartDate.Date;
        EndDatePicker.SelectedDate = defaultEndDate.Date;
    }

    private void OnOkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ValidationTextBlock.IsVisible = false;

        if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
        {
            ValidationTextBlock.Text = "Start and End dates are required.";
            ValidationTextBlock.IsVisible = true;
            return;
        }

        var startDate = StartDatePicker.SelectedDate.Value.Date;
        var endDate = EndDatePicker.SelectedDate.Value.Date;

        if (endDate < startDate)
        {
            ValidationTextBlock.Text = "End date must be the same or later than start date.";
            ValidationTextBlock.IsVisible = true;
            return;
        }

        Close(new DateRangeInput
        {
            StartDate = startDate,
            EndDate = endDate
        });
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
