using TimeLogger.App.Features.DataAnalysis.ViewModels;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.Features.Home.ViewModels;

namespace TimeLogger.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public HomeViewModel Home { get; } = new(new TimeLogStorageService());
    public DataAnalysisViewModel DataAnalysis { get; } = new();
}
