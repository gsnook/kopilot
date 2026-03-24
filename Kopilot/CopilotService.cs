using GitHub.Copilot.SDK;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kopilot;

public enum MessageKind
{
    AssistantDelta,
    AssistantFinal,
    Reasoning,
    ToolStart,
    ToolComplete,
    ToolProgress,
    SubAgentStart,
    SubAgentComplete,
    SubAgentFailed,
    BytesUpdate,
    Error,
}

public sealed class SessionEventArgs : EventArgs
{
    public string SessionId { get; init; } = "";
    public bool IsSubAgent { get; init; }
}

public sealed class SessionMessageEventArgs : EventArgs
{
    public string  SessionId          { get; init; } = "";
    public string  Content            { get; init; } = "";
    public MessageKind Kind           { get; init; }
    public string? ToolCallId         { get; init; }
    public string? ParentToolCallId   { get; init; }
    // ToolStart extras
    public string? ToolArgSummary     { get; init; }
    // ToolComplete extras
    public bool    ToolSuccess        { get; init; }
    public string? ToolResultSummary  { get; init; }
    // SubAgent extras
    public string? SubAgentDisplayName { get; init; }
    public string? SubAgentDescription { get; init; }
    // BytesUpdate
    public double  TotalBytes          { get; init; }
}

public sealed class PermissionEventArgs : EventArgs
{
    public string OperationKind { get; init; } = "";
    public string? ToolName { get; init; }
    public string? FileName { get; init; }
    public string? CommandText { get; init; }
    /// <summary>Set by the UI before resolving Decision to true, to approve all
    /// future requests of the same OperationKind for the rest of the session.</summary>
    public bool ApproveSimilar { get; set; }
    public TaskCompletionSource<bool> Decision { get; } = new();
}

public sealed class UserInputEventArgs : EventArgs
{
    public string Question { get; init; } = "";
    public IReadOnlyList<string>? Choices { get; init; }
    public bool AllowFreeform { get; init; } = true;
    public TaskCompletionSource<string> Answer { get; } = new();
}

public sealed class CopilotService : IAsyncDisposable
{
    private CopilotClient? _client;
    private CopilotSession? _mainSession;
    private IDisposable? _lifecycleSubscription;
    private readonly Dictionary<string, CopilotSession> _sessions = new();
    private readonly Dictionary<string, string> _pendingToolNames = new();
    private readonly Dictionary<string, string> _toolCallToName = new();
    // Kinds approved for the rest of this session via "Approve Similar"
    private readonly HashSet<string> _approvedKinds = new();
    // Sessions created internally (dialog generation) — suppressed from sub-agent UI events
    private readonly ConcurrentDictionary<string, byte> _internalSessionIds = new();

    public event EventHandler<SessionEventArgs>? SessionCreated;
    public event EventHandler<SessionMessageEventArgs>? MessageReceived;
    public event EventHandler<PermissionEventArgs>? PermissionRequested;
    public event EventHandler<UserInputEventArgs>? UserInputRequested;
    public event EventHandler<string>? ConnectionStateChanged;
    public event EventHandler<string>? SessionIdleForSession;

    public string ActiveModel { get; set; } = "claude-sonnet-4.6";
    public string ActiveMode  { get; set; } = "Standard";
    public string? WorkingDirectory { get; set; }
    public bool AutoApprove { get; set; } = false;
    public bool FleetMode   { get; set; } = false;
    public bool IsConnected => _client?.State == ConnectionState.Connected;

    public async Task<string> GetVersionAsync()
    {
        if (_client == null) return "";
        try
        {
            var status = await _client.GetStatusAsync();
            return status.Version ?? "";
        }
        catch { return ""; }
    }

    public async Task<string> SendAndCaptureResponseAsync(string prompt, TimeSpan? timeout = null)
    {
        await EnsureStartedAsync();
        if (_mainSession == null)
            await CreateMainSessionAsync();

        var sb = new System.Text.StringBuilder();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<SessionMessageEventArgs>? msgHandler  = null;
        EventHandler<string>?                  idleHandler = null;

        msgHandler = (_, args) =>
        {
            if (args.SessionId != _mainSession?.SessionId) return;
            if (args.Kind is MessageKind.AssistantDelta or MessageKind.AssistantFinal)
                sb.Append(args.Content);
        };

        idleHandler = (_, sessionId) =>
        {
            if (sessionId != _mainSession?.SessionId) return;
            MessageReceived         -= msgHandler;
            SessionIdleForSession   -= idleHandler;
            tcs.TrySetResult(sb.ToString());
        };

        MessageReceived       += msgHandler;
        SessionIdleForSession += idleHandler;

        try
        {
            await _mainSession!.SendAsync(new MessageOptions { Prompt = prompt });

            var delay = timeout ?? TimeSpan.FromMinutes(5);
            var winner = await Task.WhenAny(tcs.Task, Task.Delay(delay));
            if (winner != tcs.Task)
            {
                MessageReceived       -= msgHandler;
                SessionIdleForSession -= idleHandler;
                throw new TimeoutException("Copilot did not respond within the timeout period.");
            }
            return await tcs.Task;
        }
        catch
        {
            MessageReceived       -= msgHandler;
            SessionIdleForSession -= idleHandler;
            throw;
        }
    }

    public async Task UpdateModelAsync(string model)
    {
        ActiveModel = model;
        if (_mainSession != null)
        {
            try { await _mainSession.SetModelAsync(model); }
            catch { /* best-effort; new model applied on next session if this fails */ }
        }
    }

    public async Task EnsureStartedAsync()
    {
        if (_client != null) return;

        ConnectionStateChanged?.Invoke(this, "Connecting...");

        _client = new CopilotClient(new CopilotClientOptions
        {
            Cwd = WorkingDirectory,
        });

        _lifecycleSubscription = _client.On(SessionLifecycleEventTypes.Created, evt =>
        {
            // Suppress sessions created internally for dialog generation
            if (_internalSessionIds.ContainsKey(evt.SessionId)) return;

            if (evt.SessionId != _mainSession?.SessionId)
            {
                SessionCreated?.Invoke(this, new SessionEventArgs
                {
                    SessionId = evt.SessionId,
                    IsSubAgent = true,
                });
            }
        });

        await _client.StartAsync();
        ConnectionStateChanged?.Invoke(this, "Connected");
    }

    public async Task EnsureSessionAsync()
    {
        await EnsureStartedAsync();
        if (_mainSession == null)
            await CreateMainSessionAsync();
    }

    public async Task SendMessageAsync(string prompt, IReadOnlyList<string> attachmentPaths)
    {
        await EnsureStartedAsync();

        if (_mainSession == null)
            await CreateMainSessionAsync();

        List<UserMessageDataAttachmentsItem>? attachments = null;
        if (attachmentPaths.Count > 0)
        {
            attachments = attachmentPaths
                .Select(path => (UserMessageDataAttachmentsItem)new UserMessageDataAttachmentsItemFile
                {
                    Path = path,
                    DisplayName = Path.GetFileName(path),
                })
                .ToList();
        }

        await _mainSession!.SendAsync(new MessageOptions
        {
            Prompt = prompt,
            Attachments = attachments,
        });
    }

    private async Task CreateMainSessionAsync()
    {
        var session = await _client!.CreateSessionAsync(new SessionConfig
        {
            Model = ActiveModel,
            Streaming = true,
            OnPermissionRequest = BuildPermissionHandler(),
            OnUserInputRequest = BuildUserInputHandler(),
            SystemMessage = BuildModeSystemMessage(),
        });

        _mainSession = session;
        _sessions[session.SessionId] = session;
        session.On(evt => HandleSessionEvent(session.SessionId, evt));

        if (FleetMode)
        {
#pragma warning disable GHCP001
            var fleetResult = await session.Rpc.Fleet.StartAsync();
#pragma warning restore GHCP001
            if (!fleetResult.Started)
                System.Diagnostics.Debug.WriteLine("[CopilotService] Fleet mode requested but StartAsync returned Started=false");
        }

        SessionCreated?.Invoke(this, new SessionEventArgs
        {
            SessionId = session.SessionId,
            IsSubAgent = false,
        });
    }

    private SystemMessageConfig? BuildModeSystemMessage() => ActiveMode switch
    {
        "Plan" => new SystemMessageConfig
        {
            Content = "Operate in PLAN mode: before taking any action, lay out a numbered step-by-step plan and wait for the user to confirm before executing. Always show your reasoning.",
            Mode = SystemMessageMode.Append,
        },
        "Autopilot" => new SystemMessageConfig
        {
            Content = "Operate in AUTOPILOT mode: work autonomously to complete the user's goal end-to-end. Use all available tools without asking for confirmation at each step. Summarise what you did when finished.",
            Mode = SystemMessageMode.Append,
        },
        _ => null,
    };

    /// <summary>
    /// Generates a batch of <paramref name="count"/> spoken dialog lines for the given
    /// cue type, guided by <paramref name="personality"/>.  Uses a throwaway session
    /// that is suppressed from the sub-agent UI.  Returns an empty list on failure.
    /// </summary>
    public async Task<List<string>> GenerateDialogBatchAsync(
        DialogCue cue, string personality, int count = 100)
    {
        if (_client == null || !IsConnected) return [];

        var cueDesc = cue switch
        {
            DialogCue.SessionStart =>
                $"{count} brief greeting lines played when a new work session starts (up to 15 words each)",
            DialogCue.PromptSent =>
                $"{count} acknowledgement phrases played when the user submits a prompt " +
                "(5 words or fewer — very short and snappy)",
            DialogCue.PromptComplete =>
                $"{count} task-completion celebration lines played when the assistant finishes working (up to 12 words each)",
            _ => $"{count} spoken lines",
        };

        CopilotSession? session = null;
        try
        {
            var sb  = new System.Text.StringBuilder();
            var tcs = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model     = ActiveModel,
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Content = "You are a dialog writer. Generate spoken lines exactly as instructed. " +
                              "Return ONLY the numbered list — no preamble, no explanation, no extra text.",
                    Mode = SystemMessageMode.Replace,
                },
            });

            // Register as internal so lifecycle events don't surface it in the UI
            _internalSessionIds.TryAdd(session.SessionId, 0);

            session.On(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        sb.Append(delta.Data.DeltaContent ?? "");
                        break;
                    case AssistantMessageEvent msg:
                        sb.Append(msg.Data.Content ?? "");
                        break;
                    case SessionIdleEvent:
                        tcs.TrySetResult(sb.ToString());
                        break;
                    case SessionErrorEvent:
                        tcs.TrySetResult("");
                        break;
                }
            });

            var prompt =
                $"Generate {cueDesc} for a voice assistant with this personality:\n{personality}\n\n" +
                "Format — a plain numbered list, one line per entry:\n" +
                "1. line here\n2. line here\n...\n\n" +
                "Just the spoken words — no quotes, no asterisks, no stage directions.";

            await session.SendAsync(new MessageOptions { Prompt = prompt });

            var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(90)));
            var raw = winner == tcs.Task ? await tcs.Task : "";
            return ParseNumberedList(raw);
        }
        catch { return []; }
        finally
        {
            if (session != null)
            {
                _internalSessionIds.TryRemove(session.SessionId, out _);
                try { await session.DisposeAsync(); } catch { }
            }
        }
    }

    private static List<string> ParseNumberedList(string text)
    {
        var result = new List<string>();
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var m = Regex.Match(line, @"^\d+[.)\-:\s]+(.+)$");
            if (!m.Success) continue;
            var value = m.Groups[1].Value.Trim(' ', '"', '\'', '*');
            if (!string.IsNullOrEmpty(value))
                result.Add(value);
        }
        return result;
    }


    public async Task AbortAsync()
    {
        if (_mainSession != null)
            await _mainSession.AbortAsync();
    }

    public async Task ResetSessionAsync()
    {
        if (_mainSession != null)
        {
            await _mainSession.DisposeAsync();
            _sessions.Remove(_mainSession.SessionId);
            _mainSession = null;
        }
        _approvedKinds.Clear();
    }

    /// <summary>
    /// Resets the client state so a new connection can be established
    /// (e.g. after DisposeAsync to reconnect with a different working directory).
    /// </summary>
    public void Reset()
    {
        _lifecycleSubscription?.Dispose();
        _lifecycleSubscription = null;
        _sessions.Clear();
        _mainSession = null;
        _pendingToolNames.Clear();
        _approvedKinds.Clear();
        _internalSessionIds.Clear();
        _client = null;
        ConnectionStateChanged?.Invoke(this, "Not connected");
    }

    private void HandleSessionEvent(string sessionId, SessionEvent evt)
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent delta:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content = delta.Data.DeltaContent ?? "",
                    Kind = MessageKind.AssistantDelta,
                });
                break;

            case AssistantMessageEvent msg:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content = msg.Data.Content ?? "",
                    Kind = MessageKind.AssistantFinal,
                });
                break;

            case AssistantReasoningEvent reasoning:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content = reasoning.Data.Content ?? "",
                    Kind = MessageKind.Reasoning,
                });
                break;

            case ToolExecutionStartEvent tool:
                var toolName = tool.Data.ToolName ?? "";
                var toolCallId = tool.Data.ToolCallId ?? "";
                if (!string.IsNullOrEmpty(toolCallId))
                    _toolCallToName[toolCallId] = toolName;
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId        = sessionId,
                    Content          = toolName,
                    Kind             = MessageKind.ToolStart,
                    ToolCallId       = toolCallId,
                    ToolArgSummary   = SummariseArguments(tool.Data.Arguments),
                    ParentToolCallId = tool.Data.ParentToolCallId,
                });
                break;

            case ToolExecutionCompleteEvent tool:
                var completedId = tool.Data.ToolCallId ?? "";
                var completedName = _toolCallToName.TryGetValue(completedId, out var name) ? name : completedId;
                var resultSummary = tool.Data.Success
                    ? SummariseResult(tool.Data.Result?.Content)
                    : SummariseResult(tool.Data.Error?.Message);
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId         = sessionId,
                    Content           = completedName,
                    Kind              = MessageKind.ToolComplete,
                    ToolCallId        = completedId,
                    ToolSuccess       = tool.Data.Success,
                    ToolResultSummary = resultSummary,
                    ParentToolCallId  = tool.Data.ParentToolCallId,
                });
                break;

            case ToolExecutionProgressEvent prog:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId  = sessionId,
                    Content    = prog.Data.ProgressMessage ?? "",
                    Kind       = MessageKind.ToolProgress,
                    ToolCallId = prog.Data.ToolCallId,
                });
                break;

            case SubagentStartedEvent sa:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId           = sessionId,
                    Content             = sa.Data.AgentName ?? "",
                    Kind                = MessageKind.SubAgentStart,
                    ToolCallId          = sa.Data.ToolCallId,
                    SubAgentDisplayName = sa.Data.AgentDisplayName ?? "",
                    SubAgentDescription = sa.Data.AgentDescription ?? "",
                });
                break;

            case SubagentCompletedEvent sa:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId           = sessionId,
                    Content             = sa.Data.AgentName ?? "",
                    Kind                = MessageKind.SubAgentComplete,
                    ToolCallId          = sa.Data.ToolCallId,
                    SubAgentDisplayName = sa.Data.AgentDisplayName ?? "",
                });
                break;

            case SubagentFailedEvent sa:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId           = sessionId,
                    Content             = sa.Data.Error ?? "",
                    Kind                = MessageKind.SubAgentFailed,
                    ToolCallId          = sa.Data.ToolCallId,
                    SubAgentDisplayName = sa.Data.AgentDisplayName ?? "",
                });
                break;

            case AssistantStreamingDeltaEvent stream:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId  = sessionId,
                    Kind       = MessageKind.BytesUpdate,
                    TotalBytes = stream.Data.TotalResponseSizeBytes,
                });
                break;

            case SessionIdleEvent:
                SessionIdleForSession?.Invoke(this, sessionId);
                break;

            case SessionErrorEvent error:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content = error.Data.Message ?? "Unknown error",
                    Kind = MessageKind.Error,
                });
                break;
        }
    }

    // ── Argument / result summarisation helpers ───────────────────────────────

    /// <summary>
    /// Extracts the most human-readable text from a tool's JSON arguments object.
    /// Tries common meaningful keys in priority order, falling back to the raw JSON.
    /// </summary>
    private static string? SummariseArguments(object? args)
    {
        if (args == null) return null;
        try
        {
            JsonElement el = args is JsonElement je ? je
                : JsonDocument.Parse(JsonSerializer.Serialize(args)).RootElement;

            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "command", "description", "prompt", "query",
                                             "path", "expression", "input", "content" })
                {
                    if (el.TryGetProperty(key, out var prop) &&
                        prop.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            return Truncate(val.Trim(), 80);
                    }
                }
            }
            return Truncate(el.GetRawText(), 80);
        }
        catch { return null; }
    }

    /// <summary>
    /// Returns a one-line summary of a tool result, with a line count prefix when
    /// the result spans multiple lines (e.g. "16 lines: first line of output…").
    /// </summary>
    private static string? SummariseResult(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;
        var first = lines[0].Trim();
        return lines.Length > 1
            ? $"{lines.Length} lines: {Truncate(first, 80)}"
            : Truncate(first, 100);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private PermissionRequestHandler BuildPermissionHandler()    {
        return async (request, invocation) =>
        {
            // Dynamic auto-approve: respects the checkbox state at the time of each request
            if (AutoApprove || ActiveMode == "Autopilot")
                return new PermissionRequestResult { Kind = PermissionRequestResultKind.Approved };

            // Kind previously approved for this session via "Approve Similar"
            if (_approvedKinds.Contains(request.Kind ?? ""))
                return new PermissionRequestResult { Kind = PermissionRequestResultKind.Approved };

            string? toolName = request is PermissionRequestMcp mcp ? mcp.ToolName : null;
            string? fileName = request switch
            {
                PermissionRequestWrite w => w.FileName,
                PermissionRequestRead r => r.Path,
                PermissionRequestUrl u => u.Url,
                _ => null,
            };
            string? commandText = request is PermissionRequestShell shell ? shell.FullCommandText : null;

            var args = new PermissionEventArgs
            {
                OperationKind = request.Kind ?? "",
                ToolName = toolName,
                FileName = fileName,
                CommandText = commandText,
            };

            PermissionRequested?.Invoke(this, args);

            bool approved = await args.Decision.Task;

            if (approved && args.ApproveSimilar && !string.IsNullOrEmpty(args.OperationKind))
                _approvedKinds.Add(args.OperationKind);

            return new PermissionRequestResult
            {
                Kind = approved
                    ? PermissionRequestResultKind.Approved
                    : PermissionRequestResultKind.DeniedInteractivelyByUser,
            };
        };
    }

    private UserInputHandler BuildUserInputHandler()
    {
        return async (request, invocation) =>
        {
            var args = new UserInputEventArgs
            {
                Question = request.Question ?? "",
                Choices = request.Choices,
                AllowFreeform = request.AllowFreeform ?? true,
            };

            UserInputRequested?.Invoke(this, args);

            string answer = await args.Answer.Task;
            return new UserInputResponse
            {
                Answer = answer,
                WasFreeform = true,
            };
        };
    }

    public async ValueTask DisposeAsync()
    {
        _lifecycleSubscription?.Dispose();

        foreach (var session in _sessions.Values)
            await session.DisposeAsync();
        _sessions.Clear();
        _mainSession = null;

        if (_client != null)
        {
            try { await _client.DisposeAsync(); }
            catch { /* best-effort cleanup */ }
            _client = null;
        }
    }
}
