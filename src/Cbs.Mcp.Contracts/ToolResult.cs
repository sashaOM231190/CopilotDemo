namespace Cbs.Mcp.Contracts;

public sealed record ToolError(
    string Code,
    string Message,
    bool Retryable = false,
    IReadOnlyDictionary<string, string>? Details = null);

public sealed record ToolResult<T>(
    bool Success,
    T? Data,
    ToolError? Error)
{
    public static ToolResult<T> FromData(T data) => new(true, data, null);

    public static ToolResult<T> FromError(
        string code,
        string message,
        bool retryable = false,
        IReadOnlyDictionary<string, string>? details = null) =>
        new(false, default, new ToolError(code, message, retryable, details));
}
