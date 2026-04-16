namespace JKWMonitor.Models;

public enum AgentEventType
{
    Idle,
    Working,
    BuildError,
    TaskComplete,
    SubagentDone,
    WaitingForUser,
}

public record AgentEvent(
    AgentEventType Type,
    string         SessionId,
    string?        Detail,
    string         HookEventName,
    DateTimeOffset Timestamp
);
