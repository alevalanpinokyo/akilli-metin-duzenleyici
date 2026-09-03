using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AkilliMetinDuzenleyici.Models;
using AkilliMetinDuzenleyici.Services;

namespace AkilliMetinDuzenleyici.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IGroqApiService _groqApiService;
        private readonly ITextChunkerService _textChunkerService;
        private readonly IQuotaManagerService _quotaManagerService;
        private readonly ITokenCounterService _tokenCounterService;
        private readonly ISettingsService _settingsService;

        private AppSettings _appSettings;
        private CancellationTokenSource? _ccts;

        private string _inputText = string.Empty;
        private string _outputText = string.Empty;
        private int _inputWordCount = 0;
        private int _inputTokenCount = 0;
        private int _outputWordCount = 0;
        private int _outputTokenCount = 0;

        private bool _isProcessing = false;
        private double _progressValue = 0;
        private string _statusMessage = "Hazır";
        private string _progressDetailText = string.Empty;

        private int _dailyRequestCount = 0;
        private int _dailyMaxRequests = 1000;
        private int _dailyWordCount = 0;
        private int _dailyTokenCount = 0;

        private ObservableCollection<PromptItem> _savedPrompts = new();
        private PromptItem? _selectedPrompt;

        // Design-time constructor for Visual Studio XAML designer
        public MainViewModel()
            : this(new GroqApiService(), new TextChunkerService(new TokenCounterService()), new QuotaManagerService(), new TokenCounterService(), new SettingsService())
        {
            _inputText = "Bu bir tasarım anı örnek metnidir. Türkçe imla hatalarını düzeltmek için hazırdır.";
            _outputText = "Bu bir tasarım anı örnek metnidir. Türkçe imla hatalarını düzeltmek için hazırdır.";
            _statusMessage = "Tasarım Modu (Hazır)";
            _dailyRequestCount = 42;
            _dailyMaxRequests = 1000;
            _dailyWordCount = 1250;
            _inputWordCount = 10;
            _inputTokenCount = 13;
            _outputWordCount = 10;
            _outputTokenCount = 13;
        }

        public MainViewModel(
            IGroqApiService groqApiService,
            ITextChunkerService textChunkerService,
            IQuotaManagerService quotaManagerService,
            ITokenCounterService tokenCounterService,
            ISettingsService settingsService)
        {
            _groqApiService = groqApiService;
            _textChunkerService = textChunkerService;
            _quotaManagerService = quotaManagerService;
            _tokenCounterService = tokenCounterService;
            _settingsService = settingsService;
            _appSettings = new AppSettings();

            ProcessTextCommand = new RelayCommand(async _ => await ProcessTextAsync(), _ => CanProcessText());
            CancelProcessingCommand = new RelayCommand(_ => CancelProcessing(), _ => IsProcessing);
            CopyOutputCommand = new RelayCommand(_ => CopyOutput(), _ => !string.IsNullOrEmpty(OutputText));
            ClearAllCommand = new RelayCommand(_ => ClearAll(), _ => !IsProcessing);
            LoadSampleTextCommand = new RelayCommand(_ => LoadSampleText(), _ => !IsProcessing);
            OpenSettingsCommand = new RelayCommand(async param => await OpenSettingsAsync(param as Window));

            _ = InitializeAppAsync();
        }

        public AppSettings AppSettings
        {
            get => _appSettings;
            set => SetProperty(ref _appSettings, value);
        }

        public ObservableCollection<PromptItem> SavedPrompts
        {
            get => _savedPrompts;
            private set => SetProperty(ref _savedPrompts, value);
        }

        public PromptItem? SelectedPrompt
        {
            get => _selectedPrompt;
            set
            {
                if (SetProperty(ref _selectedPrompt, value) && value != null)
                {
                    AppSettings.SelectedPromptName = value.Name;
                    AppSettings.SystemPrompt = value.Content;
                    StatusMessage = $"İstemi Değiştirildi: '{value.Name}'";
                }
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                if (SetProperty(ref _inputText, value))
                {
                    InputWordCount = _tokenCounterService.CountWords(value);
                    InputTokenCount = _tokenCounterService.EstimateTokens(value);
                }
            }
        }

        public string OutputText
        {
            get => _outputText;
            set
            {
                if (SetProperty(ref _outputText, value))
                {
                    OutputWordCount = _tokenCounterService.CountWords(value);
                    OutputTokenCount = _tokenCounterService.EstimateTokens(value);
                }
            }
        }

        public int InputWordCount
        {
            get => _inputWordCount;
            private set => SetProperty(ref _inputWordCount, value);
        }

        public int InputTokenCount
        {
            get => _inputTokenCount;
            private set => SetProperty(ref _inputTokenCount, value);
        }

        public int OutputWordCount
        {
            get => _outputWordCount;
            private set => SetProperty(ref _outputWordCount, value);
        }

        public int OutputTokenCount
        {
            get => _outputTokenCount;
            private set => SetProperty(ref _outputTokenCount, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            private set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(IsNotProcessing));
                }
            }
        }

        public bool IsNotProcessing => !_isProcessing;

        public double ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string ProgressDetailText
        {
            get => _progressDetailText;
            private set => SetProperty(ref _progressDetailText, value);
        }

        public int DailyRequestCount
        {
            get => _dailyRequestCount;
            private set
            {
                if (SetProperty(ref _dailyRequestCount, value))
                {
                    OnPropertyChanged(nameof(DailyQuotaPercentage));
                    OnPropertyChanged(nameof(DailyQuotaText));
                }
            }
        }

        public int DailyMaxRequests
        {
            get => _dailyMaxRequests;
            private set
            {
                if (SetProperty(ref _dailyMaxRequests, value))
                {
                    OnPropertyChanged(nameof(DailyQuotaPercentage));
                    OnPropertyChanged(nameof(DailyQuotaText));
                }
            }
        }

        public int DailyWordCount
        {
            get => _dailyWordCount;
            private set => SetProperty(ref _dailyWordCount, value);
        }

        public int DailyTokenCount
        {
            get => _dailyTokenCount;
            private set => SetProperty(ref _dailyTokenCount, value);
        }

        public double DailyQuotaPercentage => DailyMaxRequests > 0 ? ((double)DailyRequestCount / DailyMaxRequests) * 100 : 0;

        public string DailyQuotaText => $"{DailyRequestCount} / {DailyMaxRequests} istek bugün kullanıldı";

        public ICommand ProcessTextCommand { get; }
        public ICommand CancelProcessingCommand { get; }
        public ICommand CopyOutputCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand LoadSampleTextCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        private async Task InitializeAppAsync()
        {
            try
            {
                // 1. Load persistent settings from settings.json
                AppSettings = await _settingsService.LoadSettingsAsync();
                
                SavedPrompts = new ObservableCollection<PromptItem>(AppSettings.SavedPrompts ?? new List<PromptItem>());
                SelectedPrompt = SavedPrompts.FirstOrDefault(p => p.Name == AppSettings.SelectedPromptName) 
                                 ?? SavedPrompts.FirstOrDefault();

                // 2. Load usage quota
                var usage = await _quotaManagerService.GetUsageAsync();
                DailyRequestCount = usage.GunlukIstekSayisi;
                DailyMaxRequests = usage.GunlukMaxIstek;
                DailyWordCount = usage.ToplamIslenanKelime;
                DailyTokenCount = usage.ToplamHarcananToken;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ayarlar yüklenirken hata: {ex.Message}";
            }
        }

        private bool CanProcessText()
        {
            return !IsProcessing && !string.IsNullOrWhiteSpace(InputText);
        }

        private async Task ProcessTextAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                MessageBox.Show("Lütfen düzenlemek için bir metin girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ensure settings sanitized
            _settingsService.SanitizeSettings(AppSettings);

            if (string.IsNullOrWhiteSpace(AppSettings.ApiKey))
            {
                MessageBox.Show("Groq API anahtarı boş olamaz! Lütfen Ayarlar bölümünden API anahtarınızı girin.", "API Anahtarı Eksik", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool canMake = await _quotaManagerService.CanMakeRequestAsync();
            if (!canMake)
            {
                MessageBox.Show("Günlük 1000 istek kotanıza ulaştınız! Yeni istekler yarın 00:00'da sıfırlanacaktır.", "Kota Sınırı", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Günlük istek kotası doldu (1000/1000).";
                return;
            }

            IsProcessing = true;
            ProgressValue = 0;
            StatusMessage = "Metin analiz ediliyor...";
            ProgressDetailText = string.Empty;
            OutputText = string.Empty;

            _ccts = new CancellationTokenSource();
            CancellationToken token = _ccts.Token;

            try
            {
                // Step 1: Chunking text if > maxWordsPerChunk
                List<TextChunk> chunks = _textChunkerService.ChunkText(InputText, AppSettings.MaxWordsPerChunk);
                int totalChunks = chunks.Count;

                StatusMessage = totalChunks > 1 
                    ? $"Metin {totalChunks} parçaya bölündü, işleme başlanıyor..." 
                    : "Metin işleniyor...";

                int totalPromptTokens = 0;
                int totalCompletionTokens = 0;

                for (int i = 0; i < totalChunks; i++)
                {
                    token.ThrowIfCancellationRequested();

                    TextChunk currentChunk = chunks[i];
                    int chunkNumber = i + 1;

                    StatusMessage = totalChunks > 1
                        ? $"Parça {chunkNumber}/{totalChunks} işleniyor (Model: {AppSettings.Model})..."
                        : $"Groq Cloud API ile düzenleme yapılıyor (Model: {AppSettings.Model})...";

                    ProgressValue = ((double)(chunkNumber - 1) / totalChunks) * 100;

                    GroqApiResult result = await _groqApiService.CorrectTextAsync(
                        currentChunk.Text,
                        AppSettings,
                        status => 
                        {
                            ProgressDetailText = status;
                            StatusMessage = $"Çalışan Model: {AppSettings.Model}";
                        },
                        token);

                    if (!result.IsSuccess)
                    {
                        MessageBox.Show($"Parça {chunkNumber} işlenirken hata oluştu:\n{result.ErrorMessage}", "API Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                        StatusMessage = $"Hata: {result.ErrorMessage}";
                        return;
                    }

                    currentChunk.ProcessedText = result.CorrectedText;
                    currentChunk.IsSuccess = true;

                    totalPromptTokens += result.PromptTokens;
                    totalCompletionTokens += result.CompletionTokens;

                    ProgressValue = ((double)chunkNumber / totalChunks) * 100;

                    // If multi-chunk, apply Task.Delay rate limiting safety protection
                    if (totalChunks > 1 && i < totalChunks - 1)
                    {
                        StatusMessage = $"Hız sınırı koruması: Parçalar arası bekleniyor ({AppSettings.DelayBetweenChunksMs} ms)...";
                        await Task.Delay(AppSettings.DelayBetweenChunksMs, token);
                    }
                }

                // Step 2: Recombine chunks into complete output
                OutputText = _textChunkerService.RecombineChunks(chunks);

                // Step 3: Record local usage tracking
                int totalWords = InputWordCount;
                int totalTokensUsed = (totalPromptTokens + totalCompletionTokens) > 0 
                    ? (totalPromptTokens + totalCompletionTokens) 
                    : InputTokenCount;

                await _quotaManagerService.RecordUsageAsync(totalWords, totalTokensUsed);

                // Reload quota display
                var usage = await _quotaManagerService.GetUsageAsync();
                DailyRequestCount = usage.GunlukIstekSayisi;
                DailyMaxRequests = usage.GunlukMaxIstek;
                DailyWordCount = usage.ToplamIslenanKelime;
                DailyTokenCount = usage.ToplamHarcananToken;

                StatusMessage = "Düzenleme başarıyla tamamlandı!";
                ProgressDetailText = $"İşlenen Parça: {totalChunks} | Kullanılan Token: ~{totalTokensUsed}";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "İşlem kullanıcı tarafından iptal edildi.";
                ProgressDetailText = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Beklenmeyen Hata: {ex.Message}";
                MessageBox.Show($"Bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                _ccts?.Dispose();
                _ccts = null;
            }
        }

        private void CancelProcessing()
        {
            _ccts?.Cancel();
            StatusMessage = "İptal ediliyor...";
        }

        private void CopyOutput()
        {
            if (!string.IsNullOrEmpty(OutputText))
            {
                Clipboard.SetText(OutputText);
                StatusMessage = "Düzeltilmiş metin panoya kopyalandı!";
            }
        }

        private void ClearAll()
        {
            InputText = string.Empty;
            OutputText = string.Empty;
            ProgressValue = 0;
            StatusMessage = "Temizlendi. Yeni metin girebilirsiniz.";
            ProgressDetailText = string.Empty;
        }

        private void LoadSampleText()
        {
            InputText = "gecen gun fabrikadaki panonun basina gectim saat sabah sekiz civariydi heralde plc ye baglanmaya calisiyorum fakat ethernet portundan bi türlü iletisim gelmiyor kabloyumu degistirsem switchdemi sikinti var anlamadim gitti yani sonra farkettimki karsi tarafin ip adresi 192.168.0.10 olmasi gerekirken birisi gitmis 192.168.1.10 yazmis haliyle aglar farkli olunca haberlesme kopmus neyseki scada tarafindaki etiketleride kontrol ettim oradada birkac degisken eksik tanimlanmis mesela motorun termik arizasi geldimi ekranda kirmizi yanmasi lazim ama hic bir tepki vermiyordu bende oturdum db bloklarini tek tek actim yeniden derleyip yukledim bu arada operatorde basimda bekliyo abi uretim durdu nezaman biter diye sorup duruyor tabiki insan stres oluyo boyle anlarda ama sakin kalmak sart sonra c# tarafina gectim ordada tcp soket dinleyicisi time out a dusmus thread kilitlenmis megerse try catch blogunun icine duzgun bir loglama koymadigimiz icin hatayida gorememisiz velhasil kelam sistemi bastan asagiya toparladik saat aksam uzeri uce geliyordu test butonuna bastiktan sonra konveyorler calismaya baslayinca herkez rahat bir nefes aldi ama birdaha böyle plansiz meksiz ise girismemek lazim bunu cok iyi anladim seninde basina boyle seyler geliyormu hic gercekten cok yorucu bir gundu";
            StatusMessage = "Örnek fabrika/PLC metni yüklendi.";
        }

        private async Task OpenSettingsAsync(Window? ownerWindow)
        {
            var settingsVm = new SettingsViewModel(AppSettings, _settingsService);
            var settingsWindow = new Views.SettingsWindow
            {
                DataContext = settingsVm,
                Owner = ownerWindow
            };

            bool? result = settingsWindow.ShowDialog();
            if (result == true || settingsVm.DialogResult)
            {
                AppSettings = settingsVm.Settings;
                SavedPrompts = new ObservableCollection<PromptItem>(AppSettings.SavedPrompts);
                SelectedPrompt = SavedPrompts.FirstOrDefault(p => p.Name == AppSettings.SelectedPromptName) 
                                 ?? SavedPrompts.FirstOrDefault();

                await _settingsService.SaveSettingsAsync(AppSettings);
                StatusMessage = "Ayarlar kaydedildi ve hafızaya alındı.";
            }
        }
    }
}
