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

        SourceInitialized += (_, _) =>
        {
            ForceTopmost();
            SetDebugMode(_settings.DebugMode);
        };
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

            // ToolSuccess does not change the current animation — agent is still mid-task.
            if (eventType == AgentEventType.ToolSuccess)
                return;

            string key = eventType switch
            {
                AgentEventType.Working        => "working",
                AgentEventType.SubagentDone   => "working",
                AgentEventType.WaitingForUser => "waiting",
                AgentEventType.TaskComplete   => "success",
                AgentEventType.BuildError     => "error",
                _                             => "idle",
            };

            if (_animImages.TryGetValue(key, out var entry) && entry.HasGif)
                entry.Image.Visibility = Visibility.Visible;

            // Only TaskComplete schedules an idle transition (after 5 min).
            // All other states stay active until the next event arrives.
            if (eventType == AgentEventType.TaskComplete)
            {
                _idleTimer = new System.Threading.Timer(
                    _ => SetState(AgentEventType.Idle),
                    null,
                    TimeSpan.FromMinutes(5),
                    System.Threading.Timeout.InfiniteTimeSpan);
            }
        });
    }

    private const double NormalHeight = 220;
    private const double DebugHeight  = 440;

    public void SetDebugMode(bool enabled)
    {
        Dispatcher.InvokeAsync(() =>
        {
            // Adjust position so bottom edge stays fixed
            double delta = enabled ? DebugHeight - NormalHeight : NormalHeight - DebugHeight;
            Top    -= delta;
            Height  = enabled ? DebugHeight : NormalHeight;

            var debugRow = (System.Windows.Controls.RowDefinition)
                ((System.Windows.Controls.Grid)Content).RowDefinitions[0];
            debugRow.Height = enabled
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        });
    }

    public void AppendDebugEntry(string gifName, string hookEventName, string? toolName)
    {
        Dispatcher.InvokeAsync(() =>
        {
            string separator = string.IsNullOrEmpty(RawPayloadText.Text) ? "" : "\n\n";
            string entry = $"{separator}gif:   {gifName}\nevent: {hookEventName}";
            if (!string.IsNullOrEmpty(toolName))
                entry += $"\ntool:  {toolName}";
            RawPayloadText.Text += entry;
            RawPayloadText.ScrollToEnd();
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
