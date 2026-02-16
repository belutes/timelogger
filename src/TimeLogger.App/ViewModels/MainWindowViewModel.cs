using TimeLogger.App.Features.DataAnalysis.ViewModels;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.Features.Home.ViewModels;

namespace TimeLogger.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
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
}
