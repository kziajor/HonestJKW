namespace JKWMonitor.Models;

public enum AgentEventType
{
    Idle,
    Working,
    ToolSuccess,
    BuildError,
    TaskComplete,
    SubagentDone,
    WaitingForUser,
}

public record AgentEvent(
    AgentEventType Type,
    string         SessionId,
    string?        Detail,
    DateTimeOffset Timestamp
);
