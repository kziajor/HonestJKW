using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using JKWMonitor.Models;
using JKWMonitor.Overlay;

namespace JKWMonitor.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon   _icon;
    private readonly AppSettings   _settings;
    private readonly SettingsService _settingsService;
    private readonly OverlayWindow _overlay;

    private MenuItem?     _animToggle;
    private MenuItem?     _soundToggle;
    public  ContextMenu?  TrayContextMenu { get; private set; }

    public TrayService(AppSettings settings, SettingsService settingsService, OverlayWindow overlay)
    {
        _settings        = settings;
        _settingsService = settingsService;
        _overlay         = overlay;
        _icon = new TaskbarIcon
        {
            Visibility  = Visibility.Visible,
            ToolTipText = "JKW Monitor",
            IconSource  = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/Icons/idle.ico")),
        };
        _icon.ForceCreate();

        BuildContextMenu();
        UpdateTrayIcon(AgentEventType.Idle);
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();

        var title = new MenuItem
        {
            Header    = "JKW Monitor",
            IsEnabled = false,
        };

        _animToggle = new MenuItem
        {
            Header      = "Animacje",
            IsCheckable = true,
            IsChecked   = _settings.AnimationsEnabled,
        };
        _animToggle.Click += (_, _) =>
        {
            _settings.AnimationsEnabled = _animToggle.IsChecked;
            _overlay.SetAnimationsEnabled(_settings.AnimationsEnabled);
            _settingsService.Save(_settings);
        };

        _soundToggle = new MenuItem
        {
            Header      = "Dźwięki",
            IsCheckable = true,
            IsChecked   = _settings.SoundsEnabled,
        };
        _soundToggle.Click += (_, _) =>
        {
            _settings.SoundsEnabled = _soundToggle.IsChecked;
            _settingsService.Save(_settings);
        };

        var portInfo = new MenuItem
        {
            Header    = $"Nasłuchuje na :{_settings.HttpPort}",
            IsEnabled = false,
        };

        var exit = new MenuItem { Header = "Zamknij" };
        exit.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(title);
        menu.Items.Add(new Separator());
        menu.Items.Add(_animToggle);
        menu.Items.Add(_soundToggle);
        menu.Items.Add(new Separator());
        menu.Items.Add(portInfo);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _icon.ContextMenu = menu;
        TrayContextMenu   = menu;
    }

    public void UpdateTrayIcon(AgentEventType eventType)
    {
        void SetIcon()
        {
            string iconName = eventType switch
            {
                AgentEventType.Working        => "working",
                AgentEventType.BuildError     => "error",
                AgentEventType.WaitingForUser => "waiting",
                _                             => "idle",
            };
            var uri = new Uri($"pack://application:,,,/Assets/Icons/{iconName}.ico");
            _icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(uri);
        }

        if (Application.Current.Dispatcher.CheckAccess())
            SetIcon();
        else
            Application.Current.Dispatcher.InvokeAsync(SetIcon);
    }

    public void Dispose() => _icon.Dispose();
}
