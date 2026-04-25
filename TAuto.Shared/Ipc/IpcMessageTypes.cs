namespace TAuto.Shared.Ipc;

/// <summary>
/// Well-known IPC message type constants.
/// Prevents typos — both Manager and Worker reference these.
/// </summary>
public static class IpcMessageTypes
{
    // ── Manager → Worker ──
    public const string Start          = "start";
    public const string Stop           = "stop";
    public const string Pause          = "pause";
    public const string Resume         = "resume";
    public const string UpdateConfig     = "update_config";
    public const string UpdateVariables  = "update_variables";
    public const string RequestVariables = "request_variables";
    public const string TokenGranted     = "token_granted";
    public const string TokenDenied    = "token_denied";

    // ── Worker → Manager ──
    public const string Heartbeat      = "heartbeat";
    public const string VariablesSnapshot = "variables_snapshot";
    public const string Log            = "log";
    public const string Trace          = "trace";
    public const string StatusUpdate   = "status_update";
    public const string RequestToken   = "request_token";
    public const string ReleaseToken   = "release_token";
    public const string Ready          = "ready";
    public const string Exiting        = "exiting";
    public const string Ack            = "ack";
    public const string Nack           = "nack";
}
