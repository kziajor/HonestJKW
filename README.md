# JKW Monitor

A Windows desktop app that monitors [Claude Code](https://claude.ai/code) agent events.
Displays an animated character overlay and plays sounds in response to agent activity.
Runs as a system tray icon — no main window.

## Requirements

- Windows 10/11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or SDK for building)
- [Claude Code](https://claude.ai/code) installed and configured

## Quick start

Clone the repo and run the setup script:

```powershell
git clone <repo-url>
cd HonestJKW
.\setup.ps1
```

The script:
1. Builds and publishes the app to `Publish/`
2. Registers Claude Code hooks in `%USERPROFILE%\.claude\settings.json`

Then start the monitor manually before working with Claude Code:

```powershell
.\Publish\JKWMonitor.exe
```

> Run it once — it minimizes to the tray and stays resident. Add it to Windows startup if needed.

## How it works

Claude Code calls `hooks/jkw-monitor.ps1` on every event (PreToolUse, PostToolUse, Notification, Stop, SubagentStop). The script POSTs the JSON payload to `http://127.0.0.1:7849/`. The app receives events, maps them to states, and updates the overlay and sounds accordingly.

```
Claude Code → hooks/jkw-monitor.ps1 → HTTP POST :7849 → HookServer → EventRouter → Overlay + Sound
```

## Agent states

| State | Animation | Trigger |
|---|---|---|
| Idle | `idle.gif` | app start, returns after 5 s of inactivity |
| Working | `working.gif` | `PreToolUse` |
| WaitingForUser | `waiting.gif` | `Notification` (question / plan / permission prompt) |
| BuildError | `error.gif` | `PostToolUse` — Bash with `exit_code ≠ 0` |
| TaskComplete | `success.gif` | `Stop` |
| SubagentDone | `working.gif` | `SubagentStop` |

## Project structure

```
JKWMonitor.csproj
App.xaml / App.xaml.cs       — entry point, ShutdownMode=OnExplicitShutdown
Models/
  AppSettings.cs             — configuration (JSON in %APPDATA%\JKWMonitor\settings.json)
  HookPayload.cs             — deserialization of Claude Code hook payload
  AgentEvent.cs              — internal event model + AgentEventType enum
Services/
  SettingsService.cs         — read/write settings
  ProfileService.cs          — profile enumeration + file path resolution with fallback
  HookServer.cs              — HttpListener 127.0.0.1:7849, always responds 200 {}
  EventRouter.cs             — HookPayload → AgentEvent, fires AgentEventFired event
  SoundService.cs            — NAudio WASAPI + keep-alive silence stream
  TrayService.cs             — H.NotifyIcon tray icon + context menu
Overlay/
  OverlayWindow.xaml(.cs)    — transparent overlay window, position persisted
Assets/
  Default/                   — built-in default profile
    Icons/                   — tray icons (.ico)
    Sounds/                  — sounds (.mp3)
    Animations/              — animations (.gif)
  <YourProfile>/             — custom profiles (see below)
hooks/
  jkw-monitor.ps1            — script invoked by Claude Code hooks
  settings.json.example      — example hook configuration
setup.ps1                    — setup script (publish + hooks registration)
```

## Configuration

Settings are stored in `%APPDATA%\JKWMonitor\settings.json`:

| Field | Default | Description |
|---|---|---|
| `AnimationsEnabled` | `true` | enable/disable overlay |
| `SoundsEnabled` | `true` | enable/disable sounds |
| `EventVolume` | `0.8` | sound volume (0.0–1.0) |
| `HttpPort` | `7849` | local HTTP server port |
| `DebugMode` | `false` | debug window in tray menu |
| `ActiveProfile` | `"Default"` | active asset profile name |
| `OverlayLeft/Top` | NaN | last overlay position (auto-placed on first run) |
| `OverlayScreenName` | null | monitor on which the overlay is displayed |

Settings can be changed via the tray icon context menu.

## Custom profiles

Profiles let you swap the entire set of icons, sounds, and animations without touching the
default assets. Each profile is a folder inside `Assets/` (next to `Default/`).

### Adding a custom profile

1. Create a new folder under `Assets/` — the folder name becomes the profile name:
   ```
   Assets/
     Default/        ← built-in
     MyCharacter/    ← your new profile
   ```

2. Add any combination of files matching the names used in `Default/`:
   ```
   MyCharacter/
     Animations/
       idle.gif
       working.gif
       waiting.gif
       error.gif
       success.gif
     Icons/
       idle.ico
       working.ico
       waiting.ico
       error.ico
     Sounds/
       working.mp3
       error.mp3
       success.mp3
       notify.mp3
   ```
   You only need to include the files you want to override.
   **Any missing file falls back to the `Default` profile automatically.**

3. While the app is running, right-click the tray icon → **Profile** → select your profile.
   The change takes effect immediately — no restart required.

> **Tip:** You can drop a new profile folder into `Assets/` at any time while the app is
> running. It will appear in the Profile submenu the next time you open it.

### After publishing

The entire `Assets/` tree is copied to `Publish/Assets/` when you publish. To distribute a
custom profile, just copy your profile folder into `Assets/` next to the exe.

## Build

```powershell
dotnet build
```

## Publish

```powershell
dotnet publish -c Release -r win-x64 --no-self-contained -o ./Publish -p:PublishSingleFile=true
```

Or via the setup script:

```powershell
.\setup.ps1
```

## Adding a new event type

1. Add a variant to `AgentEventType` in `Models/AgentEvent.cs`
2. Add a case to `EventRouter.Process()` in `Services/EventRouter.cs`
3. Add an `.mp3` to `Assets/Default/Sounds/` and register the filename in `SoundService.SoundFiles`
4. Add a `.gif` to `Assets/Default/Animations/` and an `<Image>` to `Overlay/OverlayWindow.xaml`
5. Add a case to `OverlayWindow.SetState()`

## Dependencies

| Package | Description | License |
|---|---|---|
| H.NotifyIcon.Wpf 2.4.1 | System tray icon for WPF | MIT |
| NAudio 2.3.0 | Audio playback via WASAPI | MIT |
| XamlAnimatedGif 2.3.1 | Animated GIF support in WPF | MIT |
