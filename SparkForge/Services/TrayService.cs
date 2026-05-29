using System.Drawing;
using System.Windows;
using WF = System.Windows.Forms;

namespace SparkForge.Services;

public class TrayService : IDisposable
{
    private readonly Window _mainWindow;
    private WF.NotifyIcon? _notifyIcon;
    private bool _minimizeToTray = true;

    public TrayService(Window mainWindow)
    {
        _mainWindow = mainWindow;
        InitializeTray();
    }

    private void InitializeTray()
    {
        _notifyIcon = new WF.NotifyIcon
        {
            Text = "SparkForge",
            Icon = CreateTrayIcon(),
            Visible = true
        };

        var contextMenu = new WF.ContextMenuStrip();

        var showItem = new WF.ToolStripMenuItem("Show", null, (_, _) => ShowWindow());
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new WF.ToolStripSeparator());

        var newChatItem = new WF.ToolStripMenuItem("New Chat", null, (_, _) =>
        {
            ShowWindow();
        });
        contextMenu.Items.Add(newChatItem);

        contextMenu.Items.Add(new WF.ToolStripSeparator());

        var exitItem = new WF.ToolStripMenuItem("Exit", null, (_, _) =>
        {
            _minimizeToTray = false;
            _notifyIcon!.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        // Handle window close — minimize to tray instead
        _mainWindow.Closing += OnWindowClosing;
        _mainWindow.StateChanged += OnWindowStateChanged;
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_minimizeToTray)
        {
            e.Cancel = true;
            _mainWindow.Hide();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow.WindowState == WindowState.Minimized && _minimizeToTray)
        {
            _mainWindow.Hide();
        }
    }

    private void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private static Icon CreateTrayIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var brush = new SolidBrush(Color.FromArgb(0x89, 0xb4, 0xfa));
        g.FillEllipse(brush, 2, 2, 28, 28);

        using var font = new Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(0x1e, 0x1e, 0x2e));
        var textSize = g.MeasureString("AI", font);
        g.DrawString("AI", font, textBrush, (32 - textSize.Width) / 2, (32 - textSize.Height) / 2);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, WF.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
