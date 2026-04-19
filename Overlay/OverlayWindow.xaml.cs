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
    private readonly ProfileService   _profileService;
    private System.Threading.Timer?   _idleTimer;
    private DispatcherTimer?          _topmostTimer;
    private bool                      _debugMode = false;
    private double                    _profileWidth  = 200;
    private double                    _profileHeight = 220;

    // key → (Image control, candidate gif paths)
    private readonly Dictionary<string, (Image Image, string[] Paths)> _animImages = [];

    private readonly List<string> _logEntries = [];
    private const int MaxLogEntries = 100;

    private static readonly nint HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE              = 0x0002;
    private const uint SWP_NOSIZE              = 0x0001;
    private const uint SWP_NOACTIVATE          = 0x0010;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip,
        MonitorEnumProcDelegate lpfnEnum, nint dwData);

    private delegate bool MonitorEnumProcDelegate(nint hMonitor, nint hdcMonitor,
        ref RECT lprcMonitor, nint dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private static MONITORINFOEX? GetMonitorInfoEx(nint hMonitor)
    {
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return GetMonitorInfo(hMonitor, ref info) ? info : null;
    }

    private static bool MonitorExists(string name)
    {
        bool found = false;
        MonitorEnumProcDelegate proc = (nint hMon, nint _, ref RECT _, nint _) =>
        {
            if (GetMonitorInfoEx(hMon) is { } info && info.szDevice == name)
            {
                found = true;
                return false;
            }
            return true;
        };
        EnumDisplayMonitors(nint.Zero, nint.Zero, proc, nint.Zero);
        return found;
    }

    private static string? GetCurrentMonitorName(nint hwnd)
    {
        nint hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        return hMon != nint.Zero ? GetMonitorInfoEx(hMon)?.szDevice : null;
    }

    public OverlayWindow(AppSettings settings, SettingsService settingsService, ProfileService profileService)
    {
        _settings        = settings;
        _settingsService = settingsService;
        _profileService  = profileService;
        InitializeComponent();
        LoadAnimations();
        _profileService.ProfileChanged += (_, _) => Dispatcher.InvokeAsync(LoadAnimations);

        SourceInitialized += (_, _) =>
        {
            SetInitialPosition();
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
            string[] paths = _profileService.ResolveFiles("Animations", $"{name}*.gif");
            _animImages[name] = (image, paths);
        }

        ApplyProfileSize();
        SetState(AgentEventType.Idle);
    }

    private void ApplyProfileSize()
    {
        var ps = _profileService.GetSettings();
        _profileWidth  = ps.WindowWidth;
        _profileHeight = ps.WindowHeight;

        double newHeight = _debugMode ? _profileHeight * 2 : _profileHeight;
        Width  = _profileWidth;
        Height = newHeight;

        if (IsLoaded)
            EnsureOnScreen();
    }

    private void SetInitialPosition()
    {
        if (!double.IsNaN(_settings.OverlayLeft) && !double.IsNaN(_settings.OverlayTop))
        {
            Left = _settings.OverlayLeft;
            Top  = _settings.OverlayTop;
            // EnsureOnScreen handles both out-of-bounds and disconnected monitor cases
            EnsureOnScreen();
        }
        else
        {
            SetDefaultPosition();
        }
    }

    private void SetDefaultPosition()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right  - Width  - 16;
        Top  = area.Bottom - Height - 16;
    }

    /// <summary>
    /// Checks whether the window fits within the work area of the monitor it is on.
    /// If not (or if the monitor is gone), snaps to the bottom-right corner of the
    /// nearest / primary monitor's work area.
    /// </summary>
    private void EnsureOnScreen()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero) return;

        // Logical→physical and physical→logical scale factors
        double invX = 1.0, invY = 1.0; // logical → physical
        double scaleX = 1.0, scaleY = 1.0; // physical → logical
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget is not null)
        {
            invX   = source.CompositionTarget.TransformToDevice.M11;
            invY   = source.CompositionTarget.TransformToDevice.M22;
            scaleX = source.CompositionTarget.TransformFromDevice.M11;
            scaleY = source.CompositionTarget.TransformFromDevice.M22;
        }

        // Find monitor that contains the window's top-left corner (physical coords)
        var pt    = new POINT { X = (int)(Left * invX), Y = (int)(Top * invY) };
        nint hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);

        double waLeft, waTop, waRight, waBottom;
        if (hMon != nint.Zero && GetMonitorInfoEx(hMon) is { } info)
        {
            // Convert physical work area to logical pixels
            waLeft   = info.rcWork.Left   * scaleX;
            waTop    = info.rcWork.Top    * scaleY;
            waRight  = info.rcWork.Right  * scaleX;
            waBottom = info.rcWork.Bottom * scaleY;
        }
        else
        {
            // Fallback: primary screen work area (already logical)
            var wa = SystemParameters.WorkArea;
            waLeft = wa.Left; waTop = wa.Top; waRight = wa.Right; waBottom = wa.Bottom;
        }

        bool outOfBounds =
            Left          < waLeft   ||
            Top           < waTop    ||
            Left + Width  > waRight  ||
            Top  + Height > waBottom;

        if (outOfBounds)
        {
            Left = waRight  - Width  - 16;
            Top  = waBottom - Height - 16;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
        _settings.OverlayLeft        = Left;
        _settings.OverlayTop         = Top;
        _settings.OverlayScreenName  = GetCurrentMonitorName(new WindowInteropHelper(this).Handle);
        _settingsService.Save(_settings);
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ContextMenu is not null)
        {
            ContextMenu.Placement         = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            ContextMenu.PlacementTarget   = this;
            ContextMenu.PlacementRectangle = System.Windows.Rect.Empty;
            ContextMenu.HorizontalOffset  = 0;
            ContextMenu.VerticalOffset    = 0;
            ContextMenu.IsOpen            = true;
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

            string key = eventType switch
            {
                AgentEventType.Working        => "working",
                AgentEventType.SubagentDone   => "working",
                AgentEventType.WaitingForUser => "waiting",
                AgentEventType.TaskComplete   => "success",
                AgentEventType.BuildError     => "error",
                _                             => "idle",
            };

            if (_animImages.TryGetValue(key, out var entry) && entry.Paths.Length > 0)
            {
                string path = entry.Paths[Random.Shared.Next(entry.Paths.Length)];
                AnimationBehavior.SetSourceUri(entry.Image, new Uri(path));
                entry.Image.Visibility = Visibility.Visible;
            }

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

    public void SetDebugMode(bool enabled)
    {
        if (enabled == _debugMode) return;
        _debugMode = enabled;
        Dispatcher.InvokeAsync(() =>
        {
            double normalH = _profileHeight;
            double debugH  = _profileHeight * 2;
            double delta = enabled ? debugH - normalH : normalH - debugH;
            Top    -= delta;
            Height  = enabled ? debugH : normalH;

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
            string entry = $"gif:   {gifName}\nevent: {hookEventName}";
            if (!string.IsNullOrEmpty(toolName))
                entry += $"\ntool:  {toolName}";

            _logEntries.Add(entry);
            if (_logEntries.Count > MaxLogEntries)
                _logEntries.RemoveAt(0);

            RawPayloadText.Text = string.Join("\n\n", _logEntries);
            RawPayloadText.ScrollToEnd();
        });
    }

    private void CopyLogsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(RawPayloadText.Text))
            Clipboard.SetText(RawPayloadText.Text);
    }

    public void SetAnimationsEnabled(bool enabled)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (enabled) EnsureOnScreen();
            Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
        });
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
