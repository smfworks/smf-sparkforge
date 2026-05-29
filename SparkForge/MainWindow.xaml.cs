using System.Windows;
using System.Windows.Controls;
using SparkForge.Models;
using SparkForge.Services;
using SparkForge.ViewModels;
using Microsoft.Web.WebView2.Wpf;
using WpfMedia = System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace SparkForge;

public partial class MainWindow : Window
{
    private TrayService? _trayService;
    private bool _webViewReady;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Logger.Info("MainWindow loaded");

        // Initialize tray icon
        _trayService = new TrayService(this);

        // Initialize WebView2
        try
        {
            await ChatWebView.EnsureCoreWebView2Async();
            ChatWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            ChatWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            ChatWebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            _webViewReady = true;
            Logger.Info("WebView2 initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 initialization failed — chat rendering will use plain text fallback", ex);
            _webViewReady = false;
            ShowWebViewFallback();
        }

        // Subscribe to HTML changes from the ViewModel
        if (DataContext is MainViewModel vm)
        {
            vm.ChatHtmlChanged += html =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_webViewReady)
                    {
                        ChatWebView.NavigateToString(html);
                    }
                });
            };

            vm.ThemeApplied += () =>
            {
                Dispatcher.Invoke(ApplyThemeColors);
            };

            // Log unhandled ViewModel errors
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.StatusText) &&
                    vm.StatusText?.Contains("error") == true)
                {
                    Logger.Warn($"Status: {vm.StatusText}");
                }
            };
        }

        ApplyThemeColors();
    }

    private void ShowWebViewFallback()
    {
        var parent = ChatWebView.Parent as Grid;
        if (parent == null) return;

        var index = parent.Children.IndexOf(ChatWebView);
        parent.Children.RemoveAt(index);

        var fallback = new TextBlock
        {
            Background = new WpfMedia.SolidColorBrush(
                (WpfColor)WpfMedia.ColorConverter.ConvertFromString(ThemeService.Base)!),
            Foreground = new WpfMedia.SolidColorBrush(
                (WpfColor)WpfMedia.ColorConverter.ConvertFromString(ThemeService.Text)!),
            FontFamily = new WpfMedia.FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(16),
            TextWrapping = TextWrapping.Wrap,
            Text = "⚠️ WebView2 runtime is not installed.\n\nDownload it from: https://go.microsoft.com/fwlink/p/?LinkId=2124703\n\nChat messages will appear here once WebView2 is available."
        };

        var scrollViewer = new ScrollViewer
        {
            Content = fallback,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        parent.Children.Insert(index, scrollViewer);
    }

    private void ApplyThemeColors()
    {
        var bg = (WpfMedia.Brush)new WpfMedia.BrushConverter().ConvertFromString(ThemeService.Base)!;
        var sidebarBg = (WpfMedia.Brush)new WpfMedia.BrushConverter().ConvertFromString(ThemeService.Mantle)!;
        var sidebarBorder = (WpfMedia.Brush)new WpfMedia.BrushConverter().ConvertFromString(ThemeService.Border)!;

        Background = bg;

        if (Content is Grid grid && grid.Children.Count > 0)
        {
            if (grid.Children[0] is Border sidebar)
            {
                sidebar.Background = sidebarBg;
                sidebar.BorderBrush = sidebarBorder;
            }
        }
    }

    // --- Conversation List Button Handlers ---

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Conversation conv)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RenameConversationCommand.Execute(conv);
            }
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Conversation conv)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.DeleteConversationCommand.Execute(conv);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Logger.Info("MainWindow closing");
        _trayService?.Dispose();
        base.OnClosed(e);
    }
}
