using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using SparkForge.Services;

namespace SparkForge.Views;

public partial class BenchmarkDialog : Window
{
    private readonly OllamaService _ollama = new();

    public ObservableCollection<BenchmarkResult> Results { get; } = new();

    public BenchmarkDialog()
    {
        InitializeComponent();
        ResultsList.ItemsSource = Results;
    }

    private async void OnRunBenchmark(object sender, RoutedEventArgs e)
    {
        Results.Clear();
        SummaryPanel.Visibility = Visibility.Collapsed;

        var models = await _ollama.ListModelsAsync();
        if (models.Count == 0)
        {
            SummaryPanel.Visibility = Visibility.Visible;
            SummaryText.Text = "No models installed. Download a model first.";
            return;
        }

        var prompt = TestPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
            prompt = "Explain the concept of recursion in one paragraph.";

        var bestTps = 0.0;
        var bestModel = "";

        foreach (var model in models)
        {
            var result = new BenchmarkResult { ModelName = model.Name, Status = "Running..." };
            Results.Add(result);

            try
            {
                var sw = Stopwatch.StartNew();
                var tokenCount = 0;
                var fullResponse = "";

                var messages = new List<Models.ChatMessage>
                {
                    new() { Role = "user", Content = prompt }
                };

                await foreach (var chunk in _ollama.ChatStreamAsync(model.Name, messages))
                {
                    if (chunk != null)
                    {
                        fullResponse += chunk;
                        tokenCount++;
                    }
                }

                sw.Stop();
                var tps = tokenCount / sw.Elapsed.TotalSeconds;

                result.TotalTokens = tokenCount;
                result.TotalTime = $"{sw.Elapsed.TotalSeconds:F1}s";
                result.TokensPerSec = $"{tps:F1}";
                result.Status = GetSpeedLabel(tps);

                if (tps > bestTps)
                {
                    bestTps = tps;
                    bestModel = model.Name;
                }
            }
            catch (Exception ex)
            {
                result.TokensPerSec = "-";
                result.TotalTime = "-";
                result.TotalTokens = 0;
                result.Status = $"Error: {ex.Message}";
            }
        }

        // Show summary
        SummaryPanel.Visibility = Visibility.Visible;
        SummaryText.Text = $"Fastest model: {bestModel} ({bestTps:F1} tokens/sec)";

        var recommendation = bestTps switch
        {
            < 5 => "Performance is low. Try smaller models (1-3B parameters) or check if GPU acceleration is enabled.",
            < 15 => "Decent performance for a CPU. Enable GPU acceleration in Ollama for faster speeds.",
            < 30 => "Good performance. You can comfortably use models up to 14B parameters.",
            _ => "Excellent performance! Your hardware is well-suited for local AI. Large models will run smoothly."
        };

        RecommendationText.Text = recommendation;
    }

    private static string GetSpeedLabel(double tps) => tps switch
    {
        < 3 => "Slow",
        < 8 => "OK",
        < 20 => "Good",
        < 40 => "Fast",
        _ => "Very Fast"
    };
}

public class BenchmarkResult
{
    public string ModelName { get; set; } = "";
    public string TokensPerSec { get; set; } = "-";
    public string TotalTime { get; set; } = "-";
    public int TotalTokens { get; set; }
    public string Status { get; set; } = "Pending";
}
