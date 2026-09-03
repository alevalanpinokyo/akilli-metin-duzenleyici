using System.Windows;
using AkilliMetinDuzenleyici.Services;
using AkilliMetinDuzenleyici.ViewModels;
using AkilliMetinDuzenleyici.Views;

namespace AkilliMetinDuzenleyici
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Dependency Injection Services
            ITokenCounterService tokenCounterService = new TokenCounterService();
            IQuotaManagerService quotaManagerService = new QuotaManagerService();
            ITextChunkerService textChunkerService = new TextChunkerService(tokenCounterService);
            IGroqApiService groqApiService = new GroqApiService();
            ISettingsService settingsService = new SettingsService();

            var mainViewModel = new MainViewModel(
                groqApiService,
                textChunkerService,
                quotaManagerService,
                tokenCounterService,
                settingsService);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
