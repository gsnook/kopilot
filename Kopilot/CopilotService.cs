using GitHub.Copilot.SDK;

namespace Kopilot;

public enum MessageKind
{
    AssistantDelta,
    AssistantFinal,
    Reasoning,
    ToolStart,
    ToolComplete,
    Error,
}

public sealed class SessionEventArgs : EventArgs
{
    public string SessionId { get; init; } = "";
    public bool IsSubAgent { get; init; }
}

public sealed class SessionMessageEventArgs : EventArgs
{
    public string SessionId { get; init; } = "";
    public string Content { get; init; } = "";
    public MessageKind Kind { get; init; }
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

    public event EventHandler<SessionEventArgs>? SessionCreated;
    public event EventHandler<SessionMessageEventArgs>? MessageReceived;
    public event EventHandler<PermissionEventArgs>? PermissionRequested;
    public event EventHandler<UserInputEventArgs>? UserInputRequested;
    public event EventHandler<string>? ConnectionStateChanged;
    public event EventHandler<string>? SessionIdleForSession;

    public string ActiveModel { get; set; } = "gpt-4.1";
    public string ActiveMode  { get; set; } = "Standard";
    public string? WorkingDirectory { get; set; }
    public bool AutoApprove { get; set; } = false;
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
                    SessionId = sessionId,
                    Content = toolName,
                    Kind = MessageKind.ToolStart,
                });
                break;

            case ToolExecutionCompleteEvent tool:
                var completedId = tool.Data.ToolCallId ?? "";
                var completedName = _toolCallToName.TryGetValue(completedId, out var name) ? name : completedId;
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content = completedName,
                    Kind = MessageKind.ToolComplete,
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

    private PermissionRequestHandler BuildPermissionHandler()
    {
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
