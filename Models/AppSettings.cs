namespace JKWMonitor.Models;

public sealed class AppSettings
{
    public bool AnimationsEnabled { get; set; } = true;
    public bool SoundsEnabled     { get; set; } = true;
    public bool DebugMode         { get; set; } = false;
    public int  HttpPort          { get; set; } = 7849;
    public float EventVolume      { get; set; } = 0.8f;
    public double OverlayLeft     { get; set; } = double.NaN;
    public double OverlayTop      { get; set; } = double.NaN;
}
