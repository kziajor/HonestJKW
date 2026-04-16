using System.IO;
using JKWMonitor.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace JKWMonitor.Services;

public sealed class SoundService : IDisposable
{
    private WasapiOut?              _keepAliveOut;
    private WasapiOut?              _currentOut;
    private AgentEventType?         _currentEventType;
    private CancellationTokenSource _currentCts = new();
    private readonly Lock           _playLock   = new();
    private bool                    _disposed;

    private AppSettings             _settings;

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

        CancellationTokenSource cts;
        lock (_playLock)
        {
            if (_currentEventType == eventType)
                return;

            _currentCts.Cancel();
            _currentOut?.Stop();
            _currentEventType = eventType;
            cts = _currentCts = new CancellationTokenSource();
        }

        Task.Run(() =>
        {
            WasapiOut? output = null;
            try
            {
                using var reader  = new AudioFileReader(path) { Volume = volume };
                var preRoll       = new SilenceProvider(reader.WaveFormat).ToSampleProvider()
                                        .Take(TimeSpan.FromMilliseconds(300));
                var chain         = new ConcatenatingSampleProvider([preRoll, reader]);

                output = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 0);
                lock (_playLock) { _currentOut = output; }

                output.Init(chain);
                output.Play();

                while (output.PlaybackState == PlaybackState.Playing && !cts.Token.IsCancellationRequested)
                    Thread.Sleep(50);

                if (cts.Token.IsCancellationRequested)
                    output.Stop();
            }
            catch { /* audio failure is non-fatal */ }
            finally
            {
                output?.Dispose();
                lock (_playLock) { if (_currentOut == output) _currentOut = null; }
            }
        }, cts.Token);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentCts.Cancel();
        _currentOut?.Stop();
        _keepAliveOut?.Stop();
        _keepAliveOut?.Dispose();
    }
}
