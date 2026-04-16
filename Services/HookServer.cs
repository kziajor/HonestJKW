using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using JKWMonitor.Models;

namespace JKWMonitor.Services;

public sealed class HookServer : IDisposable
{
    private readonly HttpListener              _listener = new();
    private readonly EventRouter              _router;
    private          CancellationTokenSource? _cts;

    public event Action<string>? RawPayloadReceived;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly byte[] OkResponse = "{}"u8.ToArray();

    public HookServer(EventRouter router, int port)
    {
        _router = router;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleRequest(ctx), ct);
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                RawPayloadReceived?.Invoke(body);
                HookPayload? parsed = ParsePayload(body);
                if (parsed is not null)
                    _router.Process(parsed);
            }
        }
        catch { /* never block Claude Code */ }
        finally
        {
            try
            {
                ctx.Response.StatusCode  = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.OutputStream.WriteAsync(OkResponse);
                ctx.Response.Close();
            }
            catch { }
        }
    }

    private static HookPayload? ParsePayload(string body)
    {
        var basePayload = JsonSerializer.Deserialize<HookPayload>(body, JsonOpts);
        return basePayload?.HookEventName switch
        {
            "PreToolUse" or "PostToolUse"
                => JsonSerializer.Deserialize<ToolUsePayload>(body, JsonOpts),
            "Notification"
                => JsonSerializer.Deserialize<NotificationPayload>(body, JsonOpts),
            "UserPromptSubmit"
                => JsonSerializer.Deserialize<UserPromptSubmitPayload>(body, JsonOpts),
            "Stop"
                => JsonSerializer.Deserialize<StopPayload>(body, JsonOpts),
            "SubagentStop"
                => JsonSerializer.Deserialize<SubagentStopPayload>(body, JsonOpts),
            _
                => basePayload
        };
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener.Stop();
    }
}
