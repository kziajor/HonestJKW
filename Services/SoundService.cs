using System.IO;
using JKWMonitor.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace JKWMonitor.Services;

public sealed class SoundService : IDisposable
{
    private WasapiOut?       _keepAliveOut;
    private bool             _disposed;

    private AppSettings      _settings;

    private static readonly Dictionary<AgentEventType, string> SoundMap = new()
    {
        [AgentEventType.Working]        = @"Assets\Sounds\working.mp3",
        [AgentEventType.BuildError]     = @"Assets\Sounds\error.mp3",
        [AgentEventType.TaskComplete]   = @"Assets\Sounds\success.mp3",
        [AgentEventType.WaitingForUser] = @"Assets\Sounds\notify.mp3",
    };

    public SoundService(AppSettings settings)
    {
        _settings = settings;
        StartKeepAlive();
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    private void StartKeepAlive()
    {
        try
        {
            var format   = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            var silence  = new SilenceProvider(format).ToSampleProvider();
            var volume   = new VolumeSampleProvider(silence) { Volume = 0.001f };

            _keepAliveOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 200);
            _keepAliveOut.Init(volume);
            _keepAliveOut.Play();
        }
        catch
        {
            // No audio device available — keep-alive skipped, sounds may clip
        }
    }

    public void Play(AgentEventType eventType)
    {
        if (!_settings.SoundsEnabled)
            return;
        if (!SoundMap.TryGetValue(eventType, out string? path))
            return;
        if (!File.Exists(path))
            return;

        float volume = _settings.EventVolume;

        Task.Run(() =>
        {
            try
            {
                using var reader  = new AudioFileReader(path) { Volume = volume };
                var preRoll       = new SilenceProvider(reader.WaveFormat).ToSampleProvider()
                                        .Take(TimeSpan.FromMilliseconds(300));
                var chain         = new ConcatenatingSampleProvider([preRoll, reader]);

                using var output  = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 0);
                output.Init(chain);
                output.Play();

                while (output.PlaybackState == PlaybackState.Playing)
                    Thread.Sleep(50);
            }
            catch { /* audio failure is non-fatal */ }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _keepAliveOut?.Stop();
        _keepAliveOut?.Dispose();
    }
}
