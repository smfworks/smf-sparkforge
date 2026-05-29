using System.Windows;
using System.Windows.Threading;
using SparkForge.Services;
using SparkForge.Views;

namespace SparkForge;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Register global exception handlers first
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Logger.Fatal("Unhandled AppDomain exception", ex);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Fatal("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nDetails have been saved to the log file.\n{Logger.GetLogPath()}",
                "SparkForge - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        Logger.Info("=== SparkForge Starting ===");
        Logger.Info($"Version: 1.0.0 | OS: {Environment.OSVersion} | .NET: {Environment.Version}");

        base.OnStartup(e);

        try
        {
            // Check if this is first run (no database yet)
            var dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SparkForge",
                "chats.db");

            var isFirstRun = !System.IO.File.Exists(dbPath);
            Logger.Info($"First run: {isFirstRun}");

            if (isFirstRun)
            {
                var wizard = new SetupWizard();
                var result = wizard.ShowDialog();

                if (result == false)
                {
                    Logger.Info("User cancelled setup wizard — shutting down");
                    Shutdown();
                    return;
                }
            }

            // Create and show main window
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Logger.Info("MainWindow shown");
        }
        catch (Exception ex)
        {
            Logger.Fatal("Startup failed", ex);
            MessageBox.Show(
                $"Failed to start SparkForge:\n\n{ex.Message}\n\nCheck the log file for details:\n{Logger.GetLogPath()}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info($"=== SparkForge Shutting Down (exit code: {e.ApplicationExitCode}) ===");
        base.OnExit(e);
    }
}
