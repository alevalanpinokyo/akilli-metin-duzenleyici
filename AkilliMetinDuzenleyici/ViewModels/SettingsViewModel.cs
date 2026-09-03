using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AkilliMetinDuzenleyici.Models;
using AkilliMetinDuzenleyici.Services;

namespace AkilliMetinDuzenleyici.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private AppSettings _settings;
        private readonly ISettingsService _settingsService;
        private bool _isApiKeyVisible = false;

        private PromptItem? _selectedPrompt;
        private string _newPromptName = string.Empty;
        private string _newPromptContent = string.Empty;

        // Design-time constructor for Visual Studio XAML designer
        public SettingsViewModel() : this(new AppSettings(), new SettingsService())
        {
        }

        public SettingsViewModel(AppSettings currentSettings, ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settings = new AppSettings
            {
                ApiKey = currentSettings.ApiKey?.Trim() ?? string.Empty,
                Endpoint = currentSettings.Endpoint?.Trim() ?? string.Empty,
                Model = currentSettings.Model?.Trim() ?? string.Empty,
                Temperature = currentSettings.Temperature,
                SystemPrompt = currentSettings.SystemPrompt?.Trim() ?? string.Empty,
                SelectedPromptName = currentSettings.SelectedPromptName?.Trim() ?? string.Empty,
                SavedPrompts = currentSettings.SavedPrompts != null 
                    ? currentSettings.SavedPrompts.Select(p => new PromptItem { Name = p.Name.Trim(), Content = p.Content.Trim() }).ToList() 
                    : new System.Collections.Generic.List<PromptItem>(),
                MaxWordsPerChunk = currentSettings.MaxWordsPerChunk,
                DelayBetweenChunksMs = currentSettings.DelayBetweenChunksMs
            };

            SavedPrompts = new ObservableCollection<PromptItem>(_settings.SavedPrompts);

            // Select active prompt
            _selectedPrompt = SavedPrompts.FirstOrDefault(p => p.Name == _settings.SelectedPromptName) 
                              ?? SavedPrompts.FirstOrDefault();

            if (_selectedPrompt != null)
            {
                _settings.SystemPrompt = _selectedPrompt.Content;
            }

            SaveCommand = new RelayCommand(async param => await SaveSettingsAsync(param as Window));
            ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
            ToggleApiKeyVisibilityCommand = new RelayCommand(_ => IsApiKeyVisible = !IsApiKeyVisible);
            AddNewPromptCommand = new RelayCommand(_ => AddNewPrompt(), _ => CanAddNewPrompt());
            DeletePromptCommand = new RelayCommand(_ => DeleteSelectedPrompt(), _ => SelectedPrompt != null && SavedPrompts.Count > 1);
        }

        public AppSettings Settings => _settings;

        public ObservableCollection<PromptItem> SavedPrompts { get; }

        public PromptItem? SelectedPrompt
        {
            get => _selectedPrompt;
            set
            {
                if (SetProperty(ref _selectedPrompt, value) && value != null)
                {
                    SystemPrompt = value.Content;
                    _settings.SelectedPromptName = value.Name;
                }
            }
        }

        public string ApiKey
        {
            get => _settings.ApiKey;
            set
            {
                _settings.ApiKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MaskedApiKey));
            }
        }

        public string MaskedApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(ApiKey)) return string.Empty;
                if (IsApiKeyVisible) return ApiKey;
                if (ApiKey.Length <= 8) return new string('*', ApiKey.Length);
                return ApiKey.Substring(0, 4) + new string('*', ApiKey.Length - 8) + ApiKey.Substring(ApiKey.Length - 4);
            }
        }

        public bool IsApiKeyVisible
        {
            get => _isApiKeyVisible;
            set
            {
                if (SetProperty(ref _isApiKeyVisible, value))
                {
                    OnPropertyChanged(nameof(MaskedApiKey));
                    OnPropertyChanged(nameof(VisibilityToggleText));
                }
            }
        }

        public string VisibilityToggleText => IsApiKeyVisible ? "🔒 Gizle" : "👁 Göster";

        public string Endpoint
        {
            get => _settings.Endpoint;
            set
            {
                _settings.Endpoint = value;
                OnPropertyChanged();
            }
        }

        public string Model
        {
            get => _settings.Model;
            set
            {
                _settings.Model = value;
                OnPropertyChanged();
            }
        }

        public double Temperature
        {
            get => _settings.Temperature;
            set
            {
                _settings.Temperature = value;
                OnPropertyChanged();
            }
        }

        public string SystemPrompt
        {
            get => _settings.SystemPrompt;
            set
            {
                _settings.SystemPrompt = value;
                OnPropertyChanged();
                if (SelectedPrompt != null && SelectedPrompt.Content != value)
                {
                    SelectedPrompt.Content = value;
                }
            }
        }

        public string NewPromptName
        {
            get => _newPromptName;
            set => SetProperty(ref _newPromptName, value);
        }

        public string NewPromptContent
        {
            get => _newPromptContent;
            set => SetProperty(ref _newPromptContent, value);
        }

        public int MaxWordsPerChunk
        {
            get => _settings.MaxWordsPerChunk;
            set
            {
                _settings.MaxWordsPerChunk = value;
                OnPropertyChanged();
            }
        }

        public int DelayBetweenChunksMs
        {
            get => _settings.DelayBetweenChunksMs;
            set
            {
                _settings.DelayBetweenChunksMs = value;
                OnPropertyChanged();
            }
        }

        public bool DialogResult { get; private set; }

        public ICommand SaveCommand { get; }
        public ICommand ResetDefaultsCommand { get; }
        public ICommand ToggleApiKeyVisibilityCommand { get; }
        public ICommand AddNewPromptCommand { get; }
        public ICommand DeletePromptCommand { get; }

        private bool CanAddNewPrompt()
        {
            return !string.IsNullOrWhiteSpace(NewPromptName) && !string.IsNullOrWhiteSpace(NewPromptContent);
        }

        private void AddNewPrompt()
        {
            string name = NewPromptName.Trim();
            string content = NewPromptContent.Trim();

            var existing = SavedPrompts.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Content = content;
                SelectedPrompt = existing;
            }
            else
            {
                var newPrompt = new PromptItem { Name = name, Content = content };
                SavedPrompts.Add(newPrompt);
                SelectedPrompt = newPrompt;
            }

            NewPromptName = string.Empty;
            NewPromptContent = string.Empty;
            MessageBox.Show($"'{name}' istemi (prompt) eklendi ve seçildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteSelectedPrompt()
        {
            if (SelectedPrompt == null) return;
            if (SavedPrompts.Count <= 1)
            {
                MessageBox.Show("En az bir sistem istemi (prompt) bulunmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var promptToRemove = SelectedPrompt;
            SavedPrompts.Remove(promptToRemove);
            SelectedPrompt = SavedPrompts.FirstOrDefault();
        }

        private async System.Threading.Tasks.Task SaveSettingsAsync(Window? window)
        {
            _settings.SavedPrompts = SavedPrompts.ToList();
            if (SelectedPrompt != null)
            {
                _settings.SelectedPromptName = SelectedPrompt.Name;
                _settings.SystemPrompt = SelectedPrompt.Content;
            }

            _settingsService.SanitizeSettings(_settings);
            await _settingsService.SaveSettingsAsync(_settings);

            DialogResult = true;
            window?.Close();
        }

        private void ResetDefaults()
        {
            ApiKey = string.Empty;
            Endpoint = "https://api.groq.com/openai/v1/chat/completions";
            Model = "groq/compound";
            Temperature = 0.1;
            MaxWordsPerChunk = 2000;
            DelayBetweenChunksMs = 1500;

            var defaultSettings = new AppSettings();
            SavedPrompts.Clear();
            foreach (var item in defaultSettings.SavedPrompts)
            {
                SavedPrompts.Add(item);
            }
            SelectedPrompt = SavedPrompts.FirstOrDefault();
        }
    }
}
