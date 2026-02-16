using Avalonia.Controls;
using Avalonia.Input;
using TimeLogger.App.Features.Home.Services;
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

    private async void OnEntriesListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HomeViewModel viewModel && viewModel.EditSelectedEntryCommand.CanExecute(null))
        {
            await viewModel.EditSelectedEntryCommand.ExecuteAsync(null);
        }
    }
}
