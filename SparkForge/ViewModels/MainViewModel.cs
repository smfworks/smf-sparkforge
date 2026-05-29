using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SparkForge.Models;
using SparkForge.Services;
using SparkForge.Views;

namespace SparkForge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly OllamaService _ollama;
    private readonly ChatStorageService _storage;
    private readonly HardwareService _hardware;
    private CancellationTokenSource? _streamCts;
    private readonly object _messagesLock = new();

    public MainViewModel()
    {
        _ollama = new OllamaService();
        _hardware = new HardwareService();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SparkForge",
            "chats.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _storage = new ChatStorageService(dbPath);

        ThemeService.ThemeChanged += _ => OnThemeChanged();
        Logger.Info("MainViewModel initialized");

        _ = InitializeAsync();
    }

    // --- Observable Properties ---

    [ObservableProperty] private string _statusText = "Checking Ollama...";
    [ObservableProperty] private bool _isOllamaRunning;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private string _selectedModel = "";
    [ObservableProperty] private string _streamingResponse = "";
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private HardwareInfo _hardwareInfo = new();
    [ObservableProperty] private string _chatHtml = "";
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private string _themeIcon = "☀️";
    [ObservableProperty] private bool _canSend = false;
    [ObservableProperty] private bool _isThinking;

    public event Action<string>? ChatHtmlChanged;
    public event Action? ThemeApplied;

    partial void OnChatHtmlChanged(string value)
    {
        ChatHtmlChanged?.Invoke(value);
    }

    [ObservableProperty] private ObservableCollection<OllamaModel> _models = new();
    [ObservableProperty] private ObservableCollection<ChatMessage> _messages = new();
    [ObservableProperty] private ObservableCollection<Conversation> _conversations = new();
    [ObservableProperty] private Conversation? _selectedConversation;

    // --- Initialization ---

    private async Task InitializeAsync()
    {
        try
        {
            HardwareInfo = _hardware.Detect();
            Logger.Info($"Hardware: {HardwareInfo.Summary}");

            await CheckOllamaConnectionAsync();

            var convs = _storage.GetConversations();
            Conversations = new ObservableCollection<Conversation>(convs);

            if (Conversations.Count > 0)
            {
                await SelectConversationAsync(Conversations[0]);
            }
            else if (Models.Count > 0)
            {
                await NewConversationAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Fatal("InitializeAsync failed", ex);
            StatusText = "Startup error — check logs for details";
        }
    }

    private async Task CheckOllamaConnectionAsync()
    {
        var running = await _ollama.IsRunningAsync();
        IsOllamaRunning = running;
        CanSend = running;
        StatusText = running ? "Connected to Ollama" : "Ollama not running — start it to begin";

        if (running)
        {
            await RefreshModelsAsync();
        }
    }

    // --- Commands ---

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        StatusText = "Reconnecting to Ollama...";
        _ollama.Reset();
        await CheckOllamaConnectionAsync();
        Logger.Info($"Reconnect: Ollama running={IsOllamaRunning}, models={Models.Count}");
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        try
        {
            var models = await _ollama.ListModelsAsync();
            Models = new ObservableCollection<OllamaModel>(models);

            // If current model isn't in the list, pick first available
            if (Models.Count > 0)
            {
                if (string.IsNullOrEmpty(SelectedModel) || !Models.Any(m => m.Name == SelectedModel))
                {
                    SelectedModel = Models[0].Name;
                }
                CanSend = true;
            }
            else
            {
                CanSend = false;
            }

            Logger.Debug($"Refreshed models: {Models.Count} available");
        }
        catch (Exception ex)
        {
            Logger.Error("RefreshModels failed", ex);
            StatusText = "Cannot connect to Ollama — click Reconnect to try again";
            CanSend = false;
            IsOllamaRunning = false;
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.Toggle();
        IsDarkTheme = ThemeService.CurrentTheme == ThemeService.Theme.Dark;
        ThemeIcon = IsDarkTheme ? "☀️" : "🌙";
        RenderChatHtml();
    }

    private void OnThemeChanged()
    {
        IsDarkTheme = ThemeService.CurrentTheme == ThemeService.Theme.Dark;
        ThemeIcon = IsDarkTheme ? "☀️" : "🌙";
        RenderChatHtml();
        ThemeApplied?.Invoke();
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (string.IsNullOrEmpty(SelectedModel)) return;
        if (SelectedConversation == null) return;

        InputText = "";
        IsBusy = true;
        IsThinking = true;

        // Save user message
        var userMsg = new ChatMessage
        {
            ConversationId = SelectedConversation.Id,
            Role = "user",
            Content = text
        };
        _storage.SaveMessage(userMsg);

        lock (_messagesLock)
        {
            Messages.Add(userMsg);
        }
        RenderChatHtml();

        // Auto-title conversation from first message
        if (SelectedConversation.Title == "New Chat")
        {
            var title = text.Length > 40 ? text[..40] + "..." : text;
            _storage.UpdateConversationTitle(SelectedConversation.Id, title);
            SelectedConversation.Title = title;
            OnPropertyChanged(nameof(Conversations));
        }

        // Build message list for API
        var apiMessages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(SelectedConversation.SystemPrompt))
        {
            apiMessages.Add(new ChatMessage { Role = "system", Content = SelectedConversation.SystemPrompt });
        }

        // Snapshot messages under lock
        List<ChatMessage> snapshot;
        lock (_messagesLock)
        {
            snapshot = Messages.ToList();
        }
        apiMessages.AddRange(snapshot);

        // Stream response
        _streamCts = new CancellationTokenSource();
        StreamingResponse = "";
        IsStreaming = true;
        var assistantMsg = new ChatMessage
        {
            ConversationId = SelectedConversation.Id,
            Role = "assistant",
            Content = ""
        };

        lock (_messagesLock)
        {
            Messages.Add(assistantMsg);
        }
        RenderChatHtml();

        try
        {
            var firstToken = true;
            await foreach (var chunk in _ollama.ChatStreamAsync(SelectedModel, apiMessages, _streamCts.Token))
            {
                if (chunk == null)
                {
                    // null chunk = connection error signal
                    IsThinking = false;
                    assistantMsg.Content = "⚠️ Cannot connect to Ollama. Make sure it's running and click Reconnect.";
                    lock (_messagesLock)
                    {
                        var idx = Messages.Count - 1;
                        if (idx >= 0) Messages[idx] = assistantMsg;
                    }
                    RenderChatHtml();
                    IsOllamaRunning = false;
                    CanSend = false;
                    StatusText = "Ollama connection lost — click Reconnect";
                    break;
                }

                if (firstToken)
                {
                    firstToken = false;
                    IsThinking = false;
                }

                StreamingResponse += chunk;
                assistantMsg.Content = StreamingResponse;
                lock (_messagesLock)
                {
                    var idx = Messages.Count - 1;
                    if (idx >= 0) Messages[idx] = assistantMsg;
                }
                RenderChatHtml();
            }
        }
        catch (OperationCanceledException)
        {
            assistantMsg.Content = StreamingResponse + "\n\n_[stopped]_";
            lock (_messagesLock)
            {
                var idx = Messages.Count - 1;
                if (idx >= 0) Messages[idx] = assistantMsg;
            }
            RenderChatHtml();
        }
        catch (Exception ex)
        {
            Logger.Error("SendMessage stream failed", ex);
            assistantMsg.Content = $"⚠️ Error: {ex.Message}";
            lock (_messagesLock)
            {
                var idx = Messages.Count - 1;
                if (idx >= 0) Messages[idx] = assistantMsg;
            }
            RenderChatHtml();
        }

        // Save completed assistant message
        _storage.SaveMessage(assistantMsg);
        StreamingResponse = "";
        IsStreaming = false;
        IsBusy = false;
        IsThinking = false;
    }

    [RelayCommand]
    private void StopGeneration()
    {
        _streamCts?.Cancel();
        Logger.Debug("Generation stopped by user");
    }

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        var model = SelectedModel;
        if (string.IsNullOrEmpty(model) && Models.Count > 0)
            model = Models[0].Name;

        var conv = _storage.CreateConversation(model);
        Conversations.Insert(0, conv);
        await SelectConversationAsync(conv);
        Logger.Debug($"New conversation: {conv.Id}");
    }

    [RelayCommand]
    private Task SelectConversationAsync(Conversation? conv)
    {
        if (conv == null) return Task.CompletedTask;
        SelectedConversation = conv;
        SelectedModel = conv.Model;

        var messages = _storage.GetMessages(conv.Id);
        lock (_messagesLock)
        {
            Messages = new ObservableCollection<ChatMessage>(messages);
        }
        CanSend = IsOllamaRunning && Models.Count > 0;
        RenderChatHtml();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void DeleteConversation(Conversation? conv)
    {
        if (conv == null) return;

        var result = MessageBox.Show(
            $"Delete conversation \"{conv.Title}\"?\nThis cannot be undone.",
            "Delete Conversation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _storage.DeleteConversation(conv.Id);
        Conversations.Remove(conv);
        Logger.Debug($"Deleted conversation: {conv.Id}");

        if (SelectedConversation?.Id == conv.Id)
        {
            SelectedConversation = Conversations.FirstOrDefault();
            if (SelectedConversation != null)
                _ = SelectConversationAsync(SelectedConversation);
            else
            {
                lock (_messagesLock) { Messages.Clear(); }
                RenderChatHtml();
            }
        }
    }

    [RelayCommand]
    private void RenameConversation(Conversation? conv)
    {
        if (conv == null) return;

        var dialog = new Views.SystemPromptDialog
        {
            Owner = Application.Current.MainWindow,
            Title = "Rename Conversation",
            PromptText = conv.Title == "New Chat" ? "" : conv.Title
        };

        if (dialog.ShowDialog() == true)
        {
            var newTitle = dialog.PromptText?.Trim();
            if (!string.IsNullOrEmpty(newTitle))
            {
                _storage.UpdateConversationTitle(conv.Id, newTitle);
                conv.Title = newTitle;
                OnPropertyChanged(nameof(Conversations));
            }
        }
    }

    [RelayCommand]
    private void EditSystemPrompt()
    {
        if (SelectedConversation == null) return;

        var dialog = new SystemPromptDialog
        {
            Owner = Application.Current.MainWindow,
            PromptText = SelectedConversation.SystemPrompt ?? ""
        };

        if (dialog.ShowDialog() == true)
        {
            var newPrompt = dialog.PromptText?.Trim() ?? "";
            _storage.UpdateSystemPrompt(SelectedConversation.Id, newPrompt);
            SelectedConversation.SystemPrompt = newPrompt;
            StatusText = string.IsNullOrEmpty(newPrompt)
                ? "System prompt cleared"
                : "System prompt updated";
            Logger.Debug($"System prompt updated for {SelectedConversation.Id}");
        }
    }

    [RelayCommand]
    private async Task PullModelAsync()
    {
        var dialog = new ModelDownloadDialog { Owner = Application.Current.MainWindow };
        var result = dialog.ShowDialog();
        if (result == true)
        {
            await RefreshModelsAsync();
            StatusText = "Model downloaded successfully";
        }
    }

    [RelayCommand]
    private void Benchmark()
    {
        var dialog = new BenchmarkDialog { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenLogs()
    {
        Logger.OpenLogsFolder();
    }

    // --- Chat HTML Rendering ---

    private void RenderChatHtml()
    {
        List<ChatMessage> snapshot;
        lock (_messagesLock)
        {
            snapshot = Messages.ToList();
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><style>");
        sb.AppendLine(ThemeService.GetChatCss());

        // Thinking indicator styles
        sb.AppendLine(@"
            .thinking { display: flex; align-items: center; gap: 6px; padding: 8px 0; color: #a6adc8; font-style: italic; }
            .thinking .dot { width: 6px; height: 6px; border-radius: 50%; background: #89b4fa; animation: bounce 1.4s infinite; }
            .thinking .dot:nth-child(2) { animation-delay: 0.2s; }
            .thinking .dot:nth-child(3) { animation-delay: 0.4s; }
            @keyframes bounce { 0%, 80%, 100% { transform: scale(0); } 40% { transform: scale(1); } }
        ");

        sb.AppendLine("</style></head><body>");

        foreach (var msg in snapshot)
        {
            var role = msg.Role;
            var roleLabel = role == "user" ? "You" : "Assistant";
            var html = MarkdownService.ConvertToHtml(msg.Content);
            sb.AppendLine($"<div class='msg {role}'><div class='bubble'><div class='role-label'>{roleLabel}</div>{html}</div></div>");
        }

        // Thinking indicator at bottom if streaming hasn't produced tokens yet
        if (IsThinking)
        {
            sb.AppendLine("<div class='msg assistant'><div class='bubble'><div class='role-label'>Assistant</div><div class='thinking'><div class='dot'></div><div class='dot'></div><div class='dot'></div> Thinking...</div></div></div>");
        }

        sb.AppendLine("</body></html>");
        ChatHtml = sb.ToString();
    }
}
