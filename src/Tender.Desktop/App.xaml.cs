using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tender.Core.Clock;
using Tender.Core.DateConversion;
using Tender.Core.Keywords;
using Tender.Core.Search;
using Tender.Desktop.Services;
using Tender.Desktop.ViewModels;
using Tender.Desktop.Views;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var shell = _host.Services.GetRequiredService<ShellWindow>();
        shell.Show();

        // 確保每日排程任務以使用者層級存在（idempotent，schtasks /F）。
        // 失敗（例：找不到 crawler exe、或公司 group policy 禁用 schtasks）不影響主程式。
        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await _host.Services
                    .GetRequiredService<IAppSettingsRepository>().LoadAsync();
                _host.Services.GetRequiredService<IScheduledTaskInstaller>()
                    .EnsureTask(settings.ScheduledTime);
            }
            catch { /* silent */ }
        });
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // 先 dispose tray icon 避免殘留
        try
        {
            (_host?.Services.GetService<INotificationService>() as IDisposable)?.Dispose();
        }
        catch { }

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core
        services.AddSingleton<ITaiwanDateConverter, TaiwanDateConverter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IKeywordMatcher, KeywordMatcher>();
        services.AddSingleton<ISearchService, SearchService>();

        // Storage
        services.AddSingleton<IDataPaths, LocalAppDataPaths>();
        services.AddSingleton<IAtomicJsonWriter, AtomicJsonWriter>();
        services.AddSingleton<ITenderRepository, TenderRepository>();
        services.AddSingleton<IDailySummaryRepository, DailySummaryRepository>();
        services.AddSingleton<ICrawlRunLogRepository, CrawlRunLogRepository>();
        services.AddSingleton<IErrorLogWriter, ErrorLogWriter>();
        services.AddSingleton<IKeywordsRepository, KeywordsRepository>();
        services.AddSingleton<IUserMarksRepository, UserMarksRepository>();
        services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();
        services.AddSingleton<ISavedSearchesRepository, SavedSearchesRepository>();

        // Desktop services
        services.AddSingleton<IMonthlyCalendarService, MonthlyCalendarService>();
        services.AddSingleton<IBrowserLauncher, ProcessStartBrowserLauncher>();
        services.AddSingleton<ICrawlerLauncher, CrawlerLauncher>();
        services.AddSingleton<IMissedRunDetector, MissedRunDetector>();
        services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
        services.AddSingleton<IXlsmExporter, TemplateXlsmExporter>();
        services.AddSingleton<IIsmsXlsmExporter, IsmsTemplateXlsmExporter>();
        services.AddSingleton<IEsgXlsmExporter, EsgTemplateXlsmExporter>();
        services.AddSingleton<ISaveFileDialogService, WpfSaveFileDialogService>();
        services.AddSingleton<IErrorSummaryDialog, WpfErrorSummaryDialog>();
        services.AddSingleton<IMigrateDataRootDialog, WpfMigrateDataRootDialog>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        services.AddSingleton<IDataCleanupService, DataCleanupService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IScheduledTaskInstaller, ScheduledTaskInstaller>();

        // ViewModels
        services.AddSingleton<MonthlyCalendarViewModel>();
        services.AddTransient<DailyQueryViewModel>();
        services.AddTransient<KeywordsManageViewModel>();
        services.AddTransient<AppSettingsViewModel>();
        services.AddSingleton<ShellViewModel>(sp =>
            new ShellViewModel(
                sp.GetRequiredService<MonthlyCalendarViewModel>(),
                () => sp.GetRequiredService<DailyQueryViewModel>(),
                sp.GetRequiredService<ICrawlerLauncher>(),
                sp.GetRequiredService<IMissedRunDetector>(),
                sp.GetRequiredService<IErrorSummaryDialog>(),
                sp.GetRequiredService<IDataPaths>(),
                sp.GetRequiredService<IDailySummaryRepository>(),
                () => sp.GetRequiredService<KeywordsManageWindow>(),
                () => sp.GetRequiredService<AppSettingsWindow>(),
                sp.GetRequiredService<IUpdateChecker>(),
                sp.GetRequiredService<IBrowserLauncher>(),
                sp.GetRequiredService<IDataCleanupService>(),
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<IAppSettingsRepository>()));

        // Windows
        services.AddSingleton<ShellWindow>();
        services.AddTransient<KeywordsManageWindow>();
        services.AddTransient<AppSettingsWindow>();
    }
}
