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
    // Maps ToolCallId → ToolName so ToolComplete can reference the originating tool name
    private readonly Dictionary<string, string> _pendingToolNames = new();
    private readonly Dictionary<string, string> _toolCallToName = new();

    public event EventHandler<SessionEventArgs>? SessionCreated;
    public event EventHandler<SessionMessageEventArgs>? MessageReceived;
    public event EventHandler<PermissionEventArgs>? PermissionRequested;
    public event EventHandler<UserInputEventArgs>? UserInputRequested;
    public event EventHandler<string>? ConnectionStateChanged;
    public event EventHandler<string>? SessionIdleForSession;

    public string ActiveModel { get; set; } = "gpt-4.1";
    public string? WorkingDirectory { get; set; }
    public bool AutoApprove { get; set; } = false;
    public bool IsConnected => _client?.State == ConnectionState.Connected;

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
            OnPermissionRequest = AutoApprove
                ? PermissionHandler.ApproveAll
                : BuildPermissionHandler(),
            OnUserInputRequest = BuildUserInputHandler(),
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
