using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using XamlAnimatedGif;
using JKWMonitor.Models;
using JKWMonitor.Services;

namespace JKWMonitor.Overlay;

public partial class OverlayWindow : Window
{
    private readonly AppSettings      _settings;
    private readonly SettingsService  _settingsService;
    private System.Threading.Timer?   _idleTimer;
    private DispatcherTimer?          _topmostTimer;

    // key → (Image control, gif exists)
    private readonly Dictionary<string, (Image Image, bool HasGif)> _animImages = [];

    private static readonly nint HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE    = 0x0002;
    private const uint SWP_NOSIZE    = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    public OverlayWindow(AppSettings settings, SettingsService settingsService)
    {
        _settings        = settings;
        _settingsService = settingsService;
        InitializeComponent();
        SetInitialPosition();
        LoadAnimations();

        SourceInitialized += (_, _) => ForceTopmost();
        Deactivated       += (_, _) => ForceTopmost();

        // Re-assert every 2 s — some fullscreen apps steal topmost
        _topmostTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(2), DispatcherPriority.Background,
            (_, _) => ForceTopmost(), Dispatcher);
        _topmostTimer.Start();
    }

    private void ForceTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void SetTrayContextMenu(ContextMenu menu)
    {
        ContextMenu = menu;
    }

    private void LoadAnimations()
    {
        string animDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Animations");

        var entries = new[]
        {
            ("idle",    IdleAnim),
            ("working", WorkingAnim),
            ("waiting", WaitingAnim),
            ("error",   ErrorAnim),
            ("success", SuccessAnim),
        };

        foreach (var (name, image) in entries)
        {
            string path = Path.Combine(animDir, $"{name}.gif");
            bool exists = File.Exists(path);
            if (exists)
                AnimationBehavior.SetSourceUri(image, new Uri(path));
            _animImages[name] = (image, exists);
        }
    }

    private void SetInitialPosition()
    {
        if (!double.IsNaN(_settings.OverlayLeft) && !double.IsNaN(_settings.OverlayTop))
        {
            Left = _settings.OverlayLeft;
            Top  = _settings.OverlayTop;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right  - Width  - 16;
            Top  = area.Bottom - Height - 16;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
        _settings.OverlayLeft = Left;
        _settings.OverlayTop  = Top;
        _settingsService.Save(_settings);
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ContextMenu is not null)
        {
            ContextMenu.PlacementTarget = this;
            ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    public void SetState(AgentEventType eventType)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _idleTimer?.Dispose();
            _idleTimer = null;

            HideAll();

            // TaskComplete (Stop) shows waiting — Claude is always waiting for user after stopping.
            // success.gif reserved for future explicit success signals.
            string key = eventType switch
            {
                AgentEventType.Working        => "working",
                AgentEventType.SubagentDone   => "working",
                AgentEventType.WaitingForUser => "waiting",
                AgentEventType.TaskComplete   => "waiting",
                AgentEventType.BuildError     => "error",
                _                             => "idle",
            };

            if (_animImages.TryGetValue(key, out var entry) && entry.HasGif)
                entry.Image.Visibility = Visibility.Visible;

            switch (eventType)
            {
                // After task complete: go idle in 20 seconds.
                case AgentEventType.TaskComplete:
                    _idleTimer = new System.Threading.Timer(
                        _ => SetState(AgentEventType.Idle),
                        null,
                        TimeSpan.FromSeconds(20),
                        System.Threading.Timeout.InfiniteTimeSpan);
                    break;

                // After working/subagent: go idle in 5 minutes if no new activity.
                case AgentEventType.Working:
                case AgentEventType.SubagentDone:
                    _idleTimer = new System.Threading.Timer(
                        _ => SetState(AgentEventType.Idle),
                        null,
                        TimeSpan.FromMinutes(5),
                        System.Threading.Timeout.InfiniteTimeSpan);
                    break;

                // After build error: go idle in 5 seconds (brief notification).
                case AgentEventType.BuildError:
                    _idleTimer = new System.Threading.Timer(
                        _ => SetState(AgentEventType.Idle),
                        null,
                        TimeSpan.FromSeconds(5),
                        System.Threading.Timeout.InfiniteTimeSpan);
                    break;

                // WaitingForUser (Notification): no idle timer — Claude is blocked on user input.
            }
        });
    }

    public void SetAnimationsEnabled(bool enabled)
    {
        Dispatcher.InvokeAsync(() =>
            Visibility = enabled ? Visibility.Visible : Visibility.Hidden);
    }

    private void HideAll()
    {
        IdleAnim.Visibility    = Visibility.Collapsed;
        WorkingAnim.Visibility = Visibility.Collapsed;
        WaitingAnim.Visibility = Visibility.Collapsed;
        ErrorAnim.Visibility   = Visibility.Collapsed;
        SuccessAnim.Visibility = Visibility.Collapsed;
    }
}
