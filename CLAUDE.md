# JKW Monitor

Aplikacja desktopowa Windows (.NET 10 WPF) monitorująca zdarzenia Claude Code agenta.
Wyświetla animacje postaci w overlay + odtwarza dźwięki. Ikonka w tray systemowym.

## Architektura

Pojedynczy projekt WPF bez warstw Clean Architecture — mała narzędziowa aplikacja.

```
Models/         — POCO records (AppSettings, HookPayload, AgentEvent)
Services/       — logika biznesowa bez zależności od UI
  SettingsService   — JSON w %APPDATA%\JKWMonitor\settings.json
  ProfileService    — lista profili z Assets/, rozwiązywanie ścieżek z fallbackiem do Default
  HookServer        — HttpListener 127.0.0.1:7849, zawsze odpowiada 200 {}
  EventRouter       — HookPayload → AgentEvent, C# event AgentEventFired
  SoundService      — NAudio WASAPI + keep-alive silence stream
  TrayService       — H.NotifyIcon TaskbarIcon + context menu
Overlay/        — przezroczyste okno overlay
Assets/         — profile assetów
  Default/      — profil domyślny (ikony .ico, dźwięki .mp3, animacje .gif)
  <Profil>/     — dodatkowe profile użytkownika
hooks/          — skrypt bash dla hooków Claude Code
```

## Kluczowe zasady

- `HookServer` ZAWSZE odpowiada `200 {}` — nigdy nie blokuje Claude Code
- `SoundService` gra ciszę w tle (volume 0.001) przez WASAPI Shared mode — trzyma USB DAC aktywny, nie blokuje innych aplikacji
- `App.xaml` używa `ShutdownMode="OnExplicitShutdown"` — brak okna ≠ zamknięcie
- Overlay jest przeciągalne; pozycja persystowana w `AppSettings`
- `ProfileService.ResolveFile(subfolder, filename)` — szuka w aktywnym profilu, fallback do `Default`

## Stany agenta → animacje

| AgentEventType | Animacja | Kiedy |
|---|---|---|
| Idle | idle.gif | Start apki, powrót po 5 s |
| Working | working.gif | PreToolUse |
| WaitingForUser | waiting.gif | Notification (pytanie/plan/uprawnienie) |
| BuildError | error.gif | PostToolUse Bash exit_code≠0 |
| TaskComplete | success.gif | Stop |
| SubagentDone | working.gif | SubagentStop |

## Dodawanie nowego typu zdarzenia

1. Dodaj wariant do `AgentEventType` w `Models/AgentEvent.cs`
2. Dodaj case do `EventRouter.Process()` w `Services/EventRouter.cs`
3. Dodaj `.mp3` do `Assets/Default/Sounds/` i zarejestruj nazwę pliku w `SoundService.SoundFiles`
4. Dodaj `.gif` do `Assets/Default/Animations/` i `<Image>` do `Overlay/OverlayWindow.xaml`
5. Dodaj case do `OverlayWindow.SetState()`

## Budowanie

```powershell
dotnet build
```

## Publikowanie

Gdy użytkownik poprosi o opublikowanie zmian ("opublikuj", "publish", "zbuduj do publish"):

```powershell
dotnet publish -c Release -r win-x64 --no-self-contained -o ./Publish -p:PublishSingleFile=true
```

Katalog wyjściowy: `./Publish` w katalogu projektu.
Cały katalog `Assets/` (wszystkie profile) jest kopiowany do `Publish/Assets/`.
Po publikacji skrypt hooków `hooks/jkw-monitor.sh` wskazuje na `./Publish/JKWMonitor.exe` — aplikacja musi być uruchomiona ręcznie przed pracą z Claude Code.

## Konfiguracja hooków Claude Code

Hooki są skonfigurowane w `~/.claude/settings.json` i wywołują `hooks/jkw-monitor.ps1`
(PowerShell — gwarantowany na Windows 11, bez zależności od curl).
Skrypt POSTuje payload na `http://127.0.0.1:7849/`. Aplikacja musi być uruchomiona ręcznie.
Jeśli nie działa, skrypt po cichu kończy się z exit 0 — Claude Code nie jest blokowany.

## Assety zastępcze (placeholder)

Przed dostarczeniem finalnych animacji/ikon wystarczą proste pliki w `Assets/Default/`:
- **Ikony** (`Assets/Default/Icons/`): idle.ico, working.ico, error.ico, waiting.ico — dowolne kolorowe ikony 16x16+32x32
- **Dźwięki** (`Assets/Default/Sounds/`): error.mp3, success.mp3, notify.mp3, working.mp3 — krótkie pliki MP3
- **Animacje** (`Assets/Default/Animations/`): idle.gif, working.gif, waiting.gif, error.gif, success.gif — mogą być statyczne GIFy na start

## Nie rób

- Nie dodawaj ASP.NET/Kestrel — HttpListener wystarczy
- Nie używaj WinForms NotifyIcon — jest H.NotifyIcon.Wpf
- Nie używaj floating NuGet versions (`*`, `^`)
- Nie dodawaj warstw Clean Architecture — to celowo single-project
