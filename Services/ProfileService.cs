using System.IO;
using System.Text.Json;
using JKWMonitor.Models;

namespace JKWMonitor.Services;

public sealed class ProfileService
{
    private readonly AppSettings     _settings;
    private readonly SettingsService _settingsService;

    public static string AssetsRoot    => Path.Combine(AppContext.BaseDirectory, "Assets");
    public static string DefaultProfile => "Default";

    public event EventHandler? ProfileChanged;

    public ProfileService(AppSettings settings, SettingsService settingsService)
    {
        _settings        = settings;
        _settingsService = settingsService;
    }

    public string ActiveProfile => _settings.ActiveProfile;

    /// <summary>Returns the names of all profiles (subdirectory names under Assets/).</summary>
    public IReadOnlyList<string> GetProfiles()
    {
        if (!Directory.Exists(AssetsRoot))
            return [DefaultProfile];

        var dirs = Directory.GetDirectories(AssetsRoot)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();

        // Always include Default even if folder is somehow missing from listing
        if (!dirs.Contains(DefaultProfile))
            dirs.Insert(0, DefaultProfile);

        return dirs;
    }

    public void SetActiveProfile(string profileName)
    {
        if (_settings.ActiveProfile == profileName) return;
        _settings.ActiveProfile = profileName;
        _settingsService.Save(_settings);
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public ProfileSettings GetSettings()
    {
        string path = ResolveFile("", "settings.json");
        if (!File.Exists(path)) return new ProfileSettings();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("animation", out var anim))
            {
                return new ProfileSettings
                {
                    WindowWidth  = anim.TryGetProperty("windowWidth",  out var w) ? w.GetInt32() : 200,
                    WindowHeight = anim.TryGetProperty("windowHeight", out var h) ? h.GetInt32() : 220,
                };
            }
        }
        catch { /* malformed json — use defaults */ }

        return new ProfileSettings();
    }

    /// <summary>
    /// Returns the absolute path to <paramref name="filename"/> inside
    /// <paramref name="subfolder"/> for the active profile. Falls back to
    /// the Default profile if the file is missing from the active one.
    /// Returns the Default path even if that file is also absent (caller
    /// handles the missing-file case).
    /// </summary>
    public string ResolveFile(string subfolder, string filename)
    {
        string profilePath = Path.Combine(AssetsRoot, _settings.ActiveProfile, subfolder, filename);
        if (File.Exists(profilePath))
            return profilePath;

        return Path.Combine(AssetsRoot, DefaultProfile, subfolder, filename);
    }
}
