using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SparkForge.Services;

namespace SparkForge.Views;

public partial class ModelDownloadDialog : Window
{
    private readonly OllamaService _ollama;
    private readonly List<AvailableModel> _allModels = new();
    private CancellationTokenSource? _downloadCts;

    public ObservableCollection<AvailableModel> FilteredModels { get; } = new();

    public ModelDownloadDialog()
    {
        InitializeComponent();
        _ollama = new OllamaService();
        ModelList.ItemsSource = FilteredModels;
        Loaded += async (_, _) => await RefreshModelsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        try
        {
            // Fetch available models from Ollama library
            var tags = await FetchLibraryModelsAsync();
            _allModels.Clear();
            _allModels.AddRange(tags);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to fetch models: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<List<AvailableModel>> FetchLibraryModelsAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var response = await http.GetStringAsync("https://ollama.com/api/models");
        using var doc = JsonDocument.Parse(response);
        var models = new List<AvailableModel>();

        foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            var name = model.GetProperty("name").GetString() ?? "";
            var desc = model.GetProperty("description").GetString() ?? "";
            var pulls = model.GetProperty("pull_count").GetInt64();

            // Get parameter size from tags
            var paramSize = "";
            var sizeBytes = 0L;
            var tags = model.GetProperty("tags");
            if (tags.GetArrayLength() > 0)
            {
                var latest = tags[0];
                paramSize = latest.GetProperty("parameter_size").GetString() ?? "";
                sizeBytes = latest.GetProperty("size").GetInt64();
            }

            models.Add(new AvailableModel
            {
                Name = name,
                Description = desc,
                ParameterSize = paramSize,
                SizeBytes = sizeBytes,
                PullCount = pulls
            });
        }

        return models.OrderByDescending(m => m.PullCount).ToList();
    }

    private void ApplyFilter()
    {
        var search = SearchBox.Text?.Trim() ?? "";
        FilteredModels.Clear();
        foreach (var model in _allModels)
        {
            if (string.IsNullOrEmpty(search) ||
                model.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (model.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                FilteredModels.Add(model);
            }
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await RefreshModelsAsync();

    private void OnModelSelected(object sender, SelectionChangedEventArgs e) { }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelName)
        {
            btn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            ProgressLabel.Text = $"Downloading {modelName}...";

            _downloadCts = new CancellationTokenSource();

            try
            {
                await foreach (var line in _ollama.PullModelAsync(modelName, _downloadCts.Token))
                {
                    if (line == null) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("completed", out var completed) &&
                            root.TryGetProperty("total", out var total))
                        {
                            var c = completed.GetInt64();
                            var t = total.GetInt64();
                            if (t > 0)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressBar.Value = (double)c / t * 100;
                                    ProgressLabel.Text = $"Downloading {modelName}... {c / 1_000_000:N0}MB / {t / 1_000_000:N0}MB";
                                });
                            }
                        }
                        else if (root.TryGetProperty("status", out var status))
                        {
                            Dispatcher.Invoke(() => ProgressLabel.Text = status.GetString());
                        }
                    }
                    catch { }
                }

                Dispatcher.Invoke(() =>
                {
                    ProgressLabel.Text = $"{modelName} downloaded successfully!";
                    ProgressBar.Value = 100;
                    DialogResult = true;
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => ProgressLabel.Text = "Download cancelled");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressLabel.Text = $"Error: {ex.Message}";
                    btn.IsEnabled = true;
                });
            }
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
        DialogResult = false;
        Close();
    }
}

public class AvailableModel : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ParameterSize { get; set; } = "";
    public long SizeBytes { get; set; }
    public long PullCount { get; set; }

    public string SizeFormatted
    {
        get
        {
            if (SizeBytes >= 1_000_000_000)
                return $"{SizeBytes / 1_000_000_000.0:F1} GB";
            if (SizeBytes >= 1_000_000)
                return $"{SizeBytes / 1_000_000.0:F0} MB";
            return $"{SizeBytes} B";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
