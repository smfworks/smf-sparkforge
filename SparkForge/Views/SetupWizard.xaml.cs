using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SparkForge.Models;
using SparkForge.Services;

namespace SparkForge.Views;

public partial class SetupWizard : Window
{
    private readonly OllamaService _ollama = new();
    private readonly HardwareService _hardware = new();
    private readonly List<RecommendedModel> _recommendedModels = new();
    private CancellationTokenSource? _downloadCts;
    private int _currentStep = 0;
    private readonly Ellipse[] _dots;
    private readonly Grid[] _steps;

    public SetupWizard()
    {
        InitializeComponent();
        _dots = new[] { Dot1, Dot2, Dot3, Dot4 };
        _steps = new[] { Step1, Step2, Step3, Step4 };
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await CheckOllamaAsync();
        DetectHardware();
        PopulateRecommendations();
    }

    // --- Step 1: Ollama Detection ---

    private async Task CheckOllamaAsync()
    {
        var running = await _ollama.IsRunningAsync();
        var installed = IsOllamaInstalled();

        if (running)
        {
            OllamaStatusIcon.Text = "\u2705"; // checkmark
            OllamaStatusText.Text = "Ollama is running";
            OllamaStatusDetail.Text = "You're ready to use local AI models.";
            OllamaActionPanel.Visibility = Visibility.Collapsed;
        }
        else if (installed)
        {
            OllamaStatusIcon.Text = "\u26a0\ufe0f"; // warning
            OllamaStatusText.Text = "Ollama is installed but not running";
            OllamaStatusDetail.Text = "Start Ollama from the Start Menu or run 'ollama serve' in a terminal.";
            OllamaActionPanel.Visibility = Visibility.Visible;
            OllamaActionText.Text = "Ollama needs to be running to use local models.";
            OllamaActionButton.Content = "Launch Ollama";
            OllamaActionButton.Click -= OnInstallOllamaClick;
            OllamaActionButton.Click += OnLaunchOllamaClick;
        }
        else
        {
            OllamaStatusIcon.Text = "\u2b55"; // circle
            OllamaStatusText.Text = "Ollama is not installed";
            OllamaStatusDetail.Text = "Ollama is a free, open-source tool that runs AI models locally on your computer.";
            OllamaActionPanel.Visibility = Visibility.Visible;
            OllamaActionText.Text = "Click below to open the Ollama download page. Install it, then come back and click Next.";
            OllamaActionButton.Content = "Download Ollama";
        }
    }

    private static bool IsOllamaInstalled()
    {
        // Check common install locations
        var paths = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path)) return true;
        }

        // Check if ollama is on PATH
        try
        {
            var psi = new ProcessStartInfo("where", "ollama")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void OnInstallOllamaClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://ollama.com/download/windows") { UseShellExecute = true });
        OllamaActionButton.Content = "Check Again";
        OllamaActionButton.Click -= OnInstallOllamaClick;
        OllamaActionButton.Click += async (_, _) => await CheckOllamaAsync();
    }

    private void OnLaunchOllamaClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ollama", "serve")
            {
                UseShellExecute = true,
                CreateNoWindow = false
            });
            OllamaActionText.Text = "Launching Ollama... click Next when the terminal window appears.";
        }
        catch
        {
            OllamaActionText.Text = "Could not launch Ollama automatically. Please start it from the Start Menu.";
        }
    }

    // --- Step 2: Hardware Detection ---

    private void DetectHardware()
    {
        var info = _hardware.Detect();
        HwGpu.Text = $"{info.GpuName} ({info.VramMb} MB VRAM)";
        HwRam.Text = $"{info.SystemRamMb / 1024.0:F1} GB";
        HwNpu.Text = info.HasNpu ? info.NpuName : "Not detected";
        HwRecommendation.Text = info.RecommendedModelSize switch
        {
            "1-3B" => "Your system has limited RAM. We recommend small models (1-3B parameters) like Phi-3 Mini or Llama 3.2 3B for the best experience.",
            "3-7B" => "Your system can comfortably run medium models (3-7B). Phi-4, Llama 3.1 8B, or Mistral 7B are great choices.",
            "7-14B" => "Your system can handle larger models (7-14B). Try Qwen 2.5 14B or Llama 3.1 8B for excellent results.",
            _ => "Your system has plenty of RAM. You can run large models (14B+) like Qwen 2.5 14B or Llama 3.1 8B with ease."
        };
    }

    // --- Step 3: Model Recommendations ---

    private void PopulateRecommendations()
    {
        var info = _hardware.Detect();

        _recommendedModels.Clear();
        _recommendedModels.AddRange(new[]
        {
            new RecommendedModel
            {
                Name = "phi4:latest",
                Description = "Microsoft's Phi-4 — excellent reasoning, 14B parameters. Best overall quality.",
                ParameterSize = "14B",
                SizeFormatted = "9.1 GB",
                IsSelected = info.RecommendedModelSize == "14B+" || info.RecommendedModelSize == "7-14B"
            },
            new RecommendedModel
            {
                Name = "llama3.2:3b",
                Description = "Meta's Llama 3.2 — fast, efficient 3B model. Great for quick responses.",
                ParameterSize = "3B",
                SizeFormatted = "2.0 GB",
                IsSelected = info.RecommendedModelSize == "1-3B"
            },
            new RecommendedModel
            {
                Name = "qwen2.5:7b",
                Description = "Alibaba's Qwen 2.5 — strong 7B model, good balance of speed and quality.",
                ParameterSize = "7B",
                SizeFormatted = "4.7 GB",
                IsSelected = info.RecommendedModelSize == "3-7B"
            },
            new RecommendedModel
            {
                Name = "mistral:7b",
                Description = "Mistral 7B — fast, capable, and widely used. Great all-rounder.",
                ParameterSize = "7B",
                SizeFormatted = "4.1 GB",
                IsSelected = false
            },
        });

        RecommendedModelList.ItemsSource = _recommendedModels;
    }

    private async void OnDownloadModelsClick(object sender, RoutedEventArgs e)
    {
        var selected = _recommendedModels.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Please select at least one model to download.", "No Model Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DownloadButton.IsEnabled = false;
        DownloadProgressPanel.Visibility = Visibility.Visible;

        foreach (var model in selected)
        {
            DownloadProgressLabel.Text = $"Downloading {model.Name}...";
            DownloadProgressBar.Value = 0;
            _downloadCts = new CancellationTokenSource();

            try
            {
                await foreach (var line in _ollama.PullModelAsync(model.Name, _downloadCts.Token))
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
                                    DownloadProgressBar.Value = (double)c / t * 100;
                                    DownloadProgressLabel.Text =
                                        $"{model.Name}: {c / 1_000_000:N0}MB / {t / 1_000_000:N0}MB";
                                });
                            }
                        }
                        else if (root.TryGetProperty("status", out var status))
                        {
                            Dispatcher.Invoke(() => DownloadProgressLabel.Text = $"{model.Name}: {status.GetString()}");
                        }
                    }
                    catch { }
                }
            }
            catch (OperationCanceledException)
            {
                DownloadProgressLabel.Text = "Download cancelled";
                DownloadButton.IsEnabled = true;
                return;
            }
            catch (Exception ex)
            {
                DownloadProgressLabel.Text = $"Error downloading {model.Name}: {ex.Message}";
                DownloadButton.IsEnabled = true;
                return;
            }
        }

        DownloadProgressLabel.Text = "All models downloaded!";
        DownloadProgressBar.Value = 100;
        DownloadButton.IsEnabled = true;
    }

    // --- Step 4: Ready ---

    private void UpdateReadyStep()
    {
        var modelList = _recommendedModels.Where(m => m.IsSelected).Select(m => m.Name);
        ReadyModelsText.Text = modelList.Any()
            ? $"Models ready: {string.Join(", ", modelList)}"
            : "You can download models anytime from the Download Models button.";
    }

    // --- Navigation ---

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep >= _steps.Length - 1)
        {
            // Final step — save preferences and close
            if (AutoStartCheck.IsChecked == true)
                EnableAutoStart();
            DialogResult = true;
            Close();
            return;
        }

        // Validate current step
        if (_currentStep == 0)
        {
            // Don't block — user can proceed even if Ollama isn't running
        }

        _currentStep++;
        ShowStep(_currentStep);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 0)
        {
            _currentStep--;
            ShowStep(_currentStep);
        }
    }

    private void ShowStep(int step)
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            _steps[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
            _dots[i].Fill = i == step
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x89, 0xb4, 0xfa))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x45, 0x47, 0x5a));
        }

        BackButton.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step >= _steps.Length - 1 ? "Finish" : "Next";

        if (step == 3) UpdateReadyStep();
    }

    private static void EnableAutoStart()
    {
        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var shortcutPath = System.IO.Path.Combine(startupFolder, "SparkForge.lnk");
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

            if (!string.IsNullOrEmpty(exePath) && !File.Exists(shortcutPath))
            {
                // Create shortcut using WScript Shell
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                var shell = Activator.CreateInstance(shellType);
                if (shell == null) return;
                var shortcut = shell.GetType().InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell,
                    new object[] { shortcutPath })!;

                shortcut.GetType().InvokeMember("TargetPath",
                    System.Reflection.BindingFlags.SetProperty, null, shortcut,
                    new object[] { exePath });
                shortcut.GetType().InvokeMember("Save",
                    System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
        }
        catch
        {
            // Silently fail — user can set this up manually
        }
    }
}

public class RecommendedModel : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ParameterSize { get; set; } = "";
    public string SizeFormatted { get; set; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
