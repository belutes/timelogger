using TimeLogger.App.Features.DataAnalysis.ViewModels;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.Features.Home.ViewModels;

namespace TimeLogger.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private int _selectedTabIndex;

    public MainWindowViewModel()
    {
        var storage = new TimeLogStorageService();
        var config = CalendarConfigLoader.Load();
        ICalendarService calendarService;

        if (string.Equals(config.Provider, "Fake", System.StringComparison.OrdinalIgnoreCase))
        {
            calendarService = new FakeCalendarService();
        }
        else
        {
            calendarService = new GraphCalendarService(config);
        }

        Home = new HomeViewModel(storage, calendarService, new CsvTimeLogExportService());
        DataAnalysis = new DataAnalysisViewModel(storage);
    }

    public HomeViewModel Home { get; }
    public DataAnalysisViewModel DataAnalysis { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value) && value == 1)
            {
                _ = DataAnalysis.RefreshAsync();
            }
        }
    }
}
