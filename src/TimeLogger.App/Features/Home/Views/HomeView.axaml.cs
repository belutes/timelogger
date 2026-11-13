using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Views.Dialogs;
using TimeLogger.App.Features.Home.ViewModels;

namespace TimeLogger.App.Features.Home.Views;

public partial class HomeView : UserControl
{
    private bool _servicesAttached;

    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDateContainerPressed(object? sender, PointerPressedEventArgs e)
    {
        EntryDatePicker.IsDropDownOpen = true;
        e.Handled = true;
    }

    private void OnTimeDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!combo.IsDropDownOpen || combo.SelectedIndex < 0) return;
            var popup = combo.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
            var root = popup?.Child;
            if (root is null) return;
            var scroll = root.GetSelfAndVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            var presenter = root.GetSelfAndVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
            var selected = combo.ContainerFromIndex(combo.SelectedIndex);
            if (scroll is null || presenter is null || selected is null) return;
            var position = selected.TranslatePoint(new Point(0, 0), presenter);
            if (position is not null)
                scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y + position.Value.Y));
        }, DispatcherPriority.Background);
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        TryAttachServices();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        TryAttachServices();
    }

    private void TryAttachServices()
    {
        if (_servicesAttached)
        {
            return;
        }

        if (DataContext is HomeViewModel viewModel &&
            TopLevel.GetTopLevel(this) is Window owner)
        {
            viewModel.AttachDialogService(new HomeDialogService(owner));
            _servicesAttached = true;
        }
    }

    private async void OnMeetingDetailsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CalEvent meeting } && TopLevel.GetTopLevel(this) is Window owner)
            await new MeetingDetailsWindow(meeting).ShowDialog(owner);
    }

    private async void OnMeetingNotesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CalEvent meeting } && TopLevel.GetTopLevel(this) is Window owner)
            await new MeetingNotesWindow(meeting).ShowDialog(owner);
    }

    private async void OnEntryMeetingNotesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WorkEntry { Meeting: { } meeting } } && TopLevel.GetTopLevel(this) is Window owner)
        {
            e.Handled = true;
            await new MeetingNotesWindow(meeting).ShowDialog(owner);
        }
    }

    private async void OnMeetingToTimeLogClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CalEvent meeting } && DataContext is HomeViewModel viewModel)
            await viewModel.FillFromMeetingAsync(meeting);
    }

    private async void OnEntriesListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && viewModel.EditSelectedEntryCommand.CanExecute(null))
        {
            await viewModel.EditSelectedEntryCommand.ExecuteAsync(null);
        }
    }
}
