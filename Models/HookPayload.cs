using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKWMonitor.Models;

public record HookPayload
{
    [JsonPropertyName("hook_event_name")] public string HookEventName { get; init; } = "";
    [JsonPropertyName("session_id")]      public string SessionId     { get; init; } = "";
}

public record ToolUsePayload : HookPayload
{
    [JsonPropertyName("tool_name")]      public string      ToolName     { get; init; } = "";
    [JsonPropertyName("tool_input")]     public JsonElement ToolInput    { get; init; }
    [JsonPropertyName("tool_response")]  public JsonElement ToolResponse { get; init; }
}

public record NotificationPayload : HookPayload
{
    [JsonPropertyName("message")]            public string Message           { get; init; } = "";
    [JsonPropertyName("notification_type")]  public string NotificationType  { get; init; } = "";
}

public record UserPromptSubmitPayload : HookPayload
{
    [JsonPropertyName("prompt")] public string Prompt { get; init; } = "";
}

public record StopPayload : HookPayload { }

public record SubagentStopPayload : HookPayload
{
    [JsonPropertyName("agent_type")]              public string AgentType            { get; init; } = "";
    [JsonPropertyName("last_assistant_message")]  public string LastAssistantMessage { get; init; } = "";
}
