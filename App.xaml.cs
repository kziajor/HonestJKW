using System.Threading;
using System.Windows;
using JKWMonitor.Models;
using JKWMonitor.Overlay;
using JKWMonitor.Services;

namespace JKWMonitor;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private bool          _ownsMutex;

    private SettingsService? _settingsService;
    private AppSettings?     _settings;
    private EventRouter?     _router;
    private SoundService?    _soundService;
    private HookServer?      _hookServer;
    private TrayService?     _trayService;
    private OverlayWindow?   _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard
        _instanceMutex = new Mutex(initiallyOwned: true, "JKWMonitor_SingleInstance", out bool isNewInstance);
        _ownsMutex = isNewInstance;
        if (!isNewInstance)
        {
            MessageBox.Show("JKW Monitor jest już uruchomiony.", "JKW Monitor",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settingsService = new SettingsService();
        _settings        = _settingsService.Load();
        _router          = new EventRouter();

        _overlay      = new OverlayWindow(_settings, _settingsService);
        _soundService = new SoundService(_settings);
        _trayService  = new TrayService(_settings, _settingsService, _overlay);
        _hookServer   = new HookServer(_router, _settings.HttpPort);

        if (_trayService.TrayContextMenu is not null)
            _overlay.SetTrayContextMenu(_trayService.TrayContextMenu);

        _router.AgentEventFired += OnAgentEvent;

        if (_settings.AnimationsEnabled)
            _overlay.Show();

        try
        {
            _hookServer.Start();
        }
        catch (System.Net.HttpListenerException ex)
        {
            MessageBox.Show(
                $"Nie można uruchomić serwera HTTP na porcie {_settings.HttpPort}.\n\n{ex.Message}\n\n" +
                "Upewnij się, że żaden inny proces nie używa tego portu.",
                "JKW Monitor — błąd startu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            // Continue running — tray and overlay still work, just no hook listening
        }
    }

    private void OnAgentEvent(object? sender, AgentEvent ev)
    {
        _soundService?.Play(ev.Type);
        _trayService?.UpdateTrayIcon(ev.Type);
        _overlay?.SetState(ev.Type);

        // Return tray icon to idle after transient states
        if (ev.Type is AgentEventType.BuildError or AgentEventType.TaskComplete)
        {
            Task.Delay(5000).ContinueWith(_ =>
                _trayService?.UpdateTrayIcon(AgentEventType.Idle));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hookServer?.Dispose();
        _trayService?.Dispose();
        _soundService?.Dispose();
        _settingsService?.Save(_settings!);
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
