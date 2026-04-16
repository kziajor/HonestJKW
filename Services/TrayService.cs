using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using JKWMonitor.Models;
using JKWMonitor.Overlay;

namespace JKWMonitor.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon    _icon;
    private readonly AppSettings    _settings;
    private readonly SettingsService _settingsService;
    private readonly OverlayWindow  _overlay;
    private readonly ProfileService _profileService;

    private MenuItem?    _animToggle;
    private MenuItem?    _soundToggle;
    private MenuItem?    _debugToggle;
    private MenuItem?    _profileMenu;
    public  ContextMenu? TrayContextMenu { get; private set; }

    public TrayService(AppSettings settings, SettingsService settingsService,
                       OverlayWindow overlay, ProfileService profileService)
    {
        _settings        = settings;
        _settingsService = settingsService;
        _overlay         = overlay;
        _profileService  = profileService;

        _icon = new TaskbarIcon
        {
            Visibility  = Visibility.Visible,
            ToolTipText = "JKW Monitor",
            IconSource  = LoadIcon("idle"),
        };
        _icon.ForceCreate();

        BuildContextMenu();
        UpdateTrayIcon(AgentEventType.Idle);
    }

    private BitmapImage LoadIcon(string name)
    {
        string path = _profileService.ResolveFile("Icons", $"{name}.ico");
        if (File.Exists(path))
            return new BitmapImage(new Uri(path));

        // Fallback: empty image rather than crashing
        return new BitmapImage();
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
            Header      = "Animations",
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
            Header      = "Sounds",
            IsCheckable = true,
            IsChecked   = _settings.SoundsEnabled,
        };
        _soundToggle.Click += (_, _) =>
        {
            _settings.SoundsEnabled = _soundToggle.IsChecked;
            _settingsService.Save(_settings);
        };

        _debugToggle = new MenuItem
        {
            Header      = "Debug mode",
            IsCheckable = true,
            IsChecked   = _settings.DebugMode,
        };
        _debugToggle.Click += (_, _) =>
        {
            _settings.DebugMode = _debugToggle.IsChecked;
            _overlay.SetDebugMode(_settings.DebugMode);
            _settingsService.Save(_settings);
        };

        _profileMenu = new MenuItem { Header = "Profile" };
        _profileMenu.SubmenuOpened += (_, _) => RefreshProfileSubmenu();
        RefreshProfileSubmenu();

        var portInfo = new MenuItem
        {
            Header    = $"Listening on :{_settings.HttpPort}",
            IsEnabled = false,
        };

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(title);
        menu.Items.Add(new Separator());
        menu.Items.Add(_animToggle);
        menu.Items.Add(_soundToggle);
        menu.Items.Add(_debugToggle);
        menu.Items.Add(new Separator());
        menu.Items.Add(_profileMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(portInfo);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _icon.ContextMenu = menu;
        TrayContextMenu   = menu;
    }

    private void RefreshProfileSubmenu()
    {
        if (_profileMenu is null) return;
        _profileMenu.Items.Clear();

        string active   = _profileService.ActiveProfile;
        var    profiles = _profileService.GetProfiles();

        foreach (string profile in profiles)
        {
            string captured = profile;
            var item = new MenuItem
            {
                Header      = profile,
                IsCheckable = true,
                IsChecked   = profile == active,
            };
            item.Click += (_, _) =>
            {
                _profileService.SetActiveProfile(captured);
                RefreshProfileSubmenu();
            };
            _profileMenu.Items.Add(item);
        }
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
            _icon.IconSource = LoadIcon(iconName);
        }

        if (Application.Current.Dispatcher.CheckAccess())
            SetIcon();
        else
            Application.Current.Dispatcher.InvokeAsync(SetIcon);
    }

    public void Dispose() => _icon.Dispose();
}
