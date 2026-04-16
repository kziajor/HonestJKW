using System.Text.Json;
using JKWMonitor.Models;

namespace JKWMonitor.Services;

public sealed class EventRouter
{
    public event EventHandler<AgentEvent>? AgentEventFired;

    public void Process(HookPayload payload)
    {
        AgentEvent? ev = payload switch
        {
            UserPromptSubmitPayload p
                => new AgentEvent(AgentEventType.Working, p.SessionId, null, p.HookEventName, DateTimeOffset.Now),

            ToolUsePayload { HookEventName: "PreToolUse" } p
                => p.ToolName is "Bash" or "ExitPlanMode"
                    ? new AgentEvent(AgentEventType.WaitingForUser, p.SessionId, p.ToolName, p.HookEventName, DateTimeOffset.Now)
                    : new AgentEvent(AgentEventType.Working, p.SessionId, p.ToolName, p.HookEventName, DateTimeOffset.Now),

            ToolUsePayload { HookEventName: "PostToolUse" } p
                => DerivePostToolUse(p),

            NotificationPayload p
                => new AgentEvent(AgentEventType.WaitingForUser, p.SessionId, p.Message, p.HookEventName, DateTimeOffset.Now),

            StopPayload p
                => new AgentEvent(AgentEventType.TaskComplete, p.SessionId, null, p.HookEventName, DateTimeOffset.Now),

            SubagentStopPayload p
                => new AgentEvent(AgentEventType.SubagentDone, p.SessionId, p.AgentType, p.HookEventName, DateTimeOffset.Now),

            _ => null
        };

        if (ev is not null)
            AgentEventFired?.Invoke(this, ev);
    }

    private static AgentEvent DerivePostToolUse(ToolUsePayload p)
    {
        if (p.ToolName == "Bash")
        {
            int exitCode = p.ToolResponse.ValueKind == JsonValueKind.Object
                && p.ToolResponse.TryGetProperty("exit_code", out var ec)
                ? ec.GetInt32() : 0;

            if (exitCode != 0)
            {
                string? command = p.ToolInput.ValueKind == JsonValueKind.Object
                    && p.ToolInput.TryGetProperty("command", out var cmd)
                    ? cmd.GetString() : null;

                return new AgentEvent(AgentEventType.BuildError, p.SessionId, command, p.HookEventName, DateTimeOffset.Now);
            }
        }

        return new AgentEvent(AgentEventType.Working, p.SessionId, p.ToolName, p.HookEventName, DateTimeOffset.Now);
    }
}
