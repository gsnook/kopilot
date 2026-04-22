using GitHub.Copilot.SDK;
using System.Text;
using System.Text.Json;

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
    SkillInvoked,
    CustomAgentsUpdated,
    BytesUpdate,
    Error,
    Status,
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
    // SubAgent completion stats
    public string? SubAgentModel       { get; init; }
    public double? SubAgentTotalCalls  { get; init; }
    public double? SubAgentTotalTokens { get; init; }
    public double? SubAgentDurationMs  { get; init; }
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

public sealed class ContextUsageEventArgs : EventArgs
{
    public string SessionId       { get; init; } = "";
    public double InputTokens     { get; init; }
    public double MaxPromptTokens { get; init; }
    public double Percent => MaxPromptTokens > 0 ? (InputTokens / MaxPromptTokens) * 100.0 : 0;
}

public sealed class CopilotService : IAsyncDisposable
{
    private CopilotClient? _client;
    private CopilotSession? _mainSession;
    private IDisposable? _lifecycleSubscription;
    private IDisposable? _lifecycleDeletedSubscription;
    private readonly Dictionary<string, CopilotSession> _sessions = new();
    private readonly Dictionary<string, string> _pendingToolNames = new();
    private readonly Dictionary<string, string> _toolCallToName = new();
    // Kinds approved for the rest of this session via "Approve Similar"
    private readonly HashSet<string> _approvedKinds = new();
    // Latest SDK-registered slash commands from CommandsChangedEvent (main session only)
    private CommandsChangedDataCommandsItem[] _cachedSdkCommands = [];
    // Status message produced by BuildSystemMessage(); emitted once the session ID is known
    private string? _pendingStatusMessage;

    private CancellationTokenSource? _keepAliveCts;
    private const int KeepAliveIntervalSeconds = 30;

    public event EventHandler<SessionEventArgs>? SessionCreated;
    public event EventHandler<SessionMessageEventArgs>? MessageReceived;
    public event EventHandler<PermissionEventArgs>? PermissionRequested;
    public event EventHandler<UserInputEventArgs>? UserInputRequested;
    public event EventHandler<string>? ConnectionStateChanged;
    public event EventHandler<string>? SessionIdleForSession;
    /// <summary>
    /// Fires when a non-main, non-internal session is destroyed by the CLI.
    /// The UI uses this as a reliable "sub-agent session truly ended" signal so it
    /// can recover from missing or out-of-order subagent.completed/failed events.
    /// </summary>
    public event EventHandler<string>? SubAgentSessionEnded;
    public event EventHandler<ContextUsageEventArgs>? ContextUsageChanged;

    // Most recent input-token reading from the main session (size of the prompt
    // window the model just consumed) and the active model's prompt-token ceiling.
    private double _currentInputTokens     = 0;
    private double _currentMaxPromptTokens = 0;
    // Cache of per-model prompt-token limits, populated by ListModelsAsync.
    private readonly Dictionary<string, double> _modelPromptLimits =
        new(StringComparer.OrdinalIgnoreCase);

    public double CurrentInputTokens     => _currentInputTokens;
    public double CurrentMaxPromptTokens => _currentMaxPromptTokens;

    public string ActiveModel { get; set; } = "claude-sonnet-4.6";
    public string ActiveMode  { get; set; } = "Standard";
    public string? WorkingDirectory { get; set; }
    public string? KopilotPath => WorkingDirectory == null ? null
        : Path.Combine(WorkingDirectory, ".kopilot");
    /// <summary>Optional organization-level tier folder (set from kopilot.ini).</summary>
    public string? OrgFolder { get; set; }
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

    /// <summary>
    /// Starts the client if needed and returns the available model IDs from the SDK.
    /// Returns an empty list on any error (e.g., not authenticated).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync()
    {
        try
        {
            await EnsureStartedAsync();
            var models = await _client!.ListModelsAsync();

            // Capture per-model prompt-token ceiling for the context meter.
            foreach (var m in models)
            {
                if (string.IsNullOrEmpty(m.Id)) continue;
                var limits = m.Capabilities.Limits;
                var max = limits.MaxPromptTokens
                       ?? (limits.MaxContextWindowTokens > 0 ? limits.MaxContextWindowTokens : 0);
                if (max > 0) _modelPromptLimits[m.Id] = max;
            }
            // Reflect the current model's limit immediately so the meter has a
            // denominator before the first turn arrives.
            _currentMaxPromptTokens = LookupMaxPromptTokens(ActiveModel);

            return models
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Returns the prompt-token ceiling for <paramref name="modelId"/>.  Prefers the
    /// SDK-reported value; otherwise picks a reasonable default based on the model
    /// family prefix; otherwise falls back to a generic 200K window.
    /// </summary>
    private double LookupMaxPromptTokens(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return 200_000;
        if (_modelPromptLimits.TryGetValue(modelId, out var v) && v > 0) return v;

        // Family-level fallbacks for models the SDK has not reported limits for.
        if (modelId.StartsWith("gpt-4.1",  StringComparison.OrdinalIgnoreCase)) return 1_000_000;
        if (modelId.StartsWith("gpt-5",    StringComparison.OrdinalIgnoreCase)) return 200_000;
        if (modelId.StartsWith("claude-",  StringComparison.OrdinalIgnoreCase)) return 200_000;
        return 200_000;
    }

    // True when an AssistantUsageEvent represents a top-level user-driven
    // request, as opposed to a sub-agent or sampling call whose tokens should
    // not be charged to the visible context-window meter.  The SDK schema says
    // Initiator is absent for user-initiated calls, but some models emit the
    // literal string "user", so we accept both forms.  Known non-user values
    // ("sub-agent", "mcp-sampling", and any future additions) are rejected.
    private static bool IsUserInitiatedUsage(string? initiator)
    {
        if (string.IsNullOrEmpty(initiator)) return true;
        return string.Equals(initiator, "user", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the slash commands currently available in the active session.
    /// Merges SDK-registered commands (from CommandsChangedEvent) with user-invocable
    /// skills obtained via an on-demand skills.list RPC call.  Returns an empty list
    /// when no session exists.
    /// </summary>
    public async Task<IReadOnlyList<(string Name, string? Description)>> GetCommandListAsync()
    {
        if (_mainSession == null) return [];

        var results = new List<(string Name, string? Description)>();

        // SDK-registered commands (kept current via CommandsChangedEvent)
        foreach (var cmd in _cachedSdkCommands)
            results.Add((cmd.Name, cmd.Description));

        // User-invocable skills are also slash-commandable — fetch on demand
        try
        {
#pragma warning disable GHCP001
            var skillsResult = await _mainSession.Rpc.Skills.ListAsync();
#pragma warning restore GHCP001
            foreach (var skill in skillsResult.Skills)
            {
                if (!skill.UserInvocable || !skill.Enabled) continue;
                if (!results.Exists(r => string.Equals(r.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                    results.Add((skill.Name, string.IsNullOrWhiteSpace(skill.Description) ? null : skill.Description));
            }
        }
        catch { /* best-effort; SDK commands already captured above */ }

        results.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return results;
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
        _currentMaxPromptTokens = LookupMaxPromptTokens(model);
        if (_mainSession != null)
        {
            try { await _mainSession.SetModelAsync(model); }
            catch { /* best-effort; new model applied on next session if this fails */ }
        }
    }

    public async Task EnsureStartedAsync()
    {
        if (_client != null) return;

        if (KopilotPath != null)
            Directory.CreateDirectory(KopilotPath);

        await ConnectAsync();
        StartKeepAlive();
    }

    private async Task ConnectAsync()
    {
        ConnectionStateChanged?.Invoke(this, "Connecting...");

        _client = new CopilotClient(new CopilotClientOptions
        {
            CliPath = ResolveCliFromPath(),
            Cwd = WorkingDirectory,
            Environment = BuildCliEnvironment(),
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

        _lifecycleDeletedSubscription = _client.On(SessionLifecycleEventTypes.Deleted, evt =>
        {
            // The main session being deleted is handled elsewhere (reconnect/reset paths).
            if (evt.SessionId == _mainSession?.SessionId) return;

            SubAgentSessionEnded?.Invoke(this, evt.SessionId);
        });

        await _client.StartAsync();
        ConnectionStateChanged?.Invoke(this, "Connected");
    }

    private void StartKeepAlive()
    {
        if (_keepAliveCts != null) return;
        _keepAliveCts = new CancellationTokenSource();
        _ = RunKeepAliveAsync(_keepAliveCts.Token);
    }

    private void StopKeepAlive()
    {
        _keepAliveCts?.Cancel();
        _keepAliveCts?.Dispose();
        _keepAliveCts = null;
    }

    private async Task RunKeepAliveAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(KeepAliveIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_client == null) continue;
                try
                {
                    using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    pingCts.CancelAfter(TimeSpan.FromSeconds(10));
                    await _client.PingAsync(cancellationToken: pingCts.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    await TryReconnectAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private async Task TryReconnectAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;

        ConnectionStateChanged?.Invoke(this, "Reconnecting...");

        _lifecycleSubscription?.Dispose();
        _lifecycleSubscription = null;
        _lifecycleDeletedSubscription?.Dispose();
        _lifecycleDeletedSubscription = null;

        foreach (var session in _sessions.Values)
            try { await session.DisposeAsync(); } catch { }
        _sessions.Clear();
        _mainSession = null;
        _cachedSdkCommands = [];
        _pendingToolNames.Clear();
        _approvedKinds.Clear();

        if (_client != null)
        {
            try { await _client.DisposeAsync(); } catch { }
            _client = null;
        }

        if (cancellationToken.IsCancellationRequested) return;

        try
        {
            await ConnectAsync();
            await CreateMainSessionAsync();
        }
        catch (OperationCanceledException) { }
        catch
        {
            ConnectionStateChanged?.Invoke(this, "Not connected");
        }
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

        var options = new MessageOptions { Prompt = prompt, Attachments = attachments };

        try
        {
            await _mainSession!.SendAsync(options);
        }
        catch (IOException ex) when (ex.Message.Contains("Session not found", StringComparison.OrdinalIgnoreCase))
        {
            // The CLI cleaned up the server-side session after an idle period.
            // Recreate the session transparently and retry — the user sees the
            // new session header but their prompt still goes through normally.
            await RecoverExpiredSessionAsync();
            await _mainSession!.SendAsync(options);
        }
    }

    /// <summary>
    /// Recovers from a server-side session expiry without tearing down the CLI transport.
    /// Tries to resume the original session first (preserving conversation history); falls
    /// back to creating a fresh session only if the disk data is also gone.
    /// </summary>
    private async Task RecoverExpiredSessionAsync()
    {
        var expiredSessionId = _mainSession?.SessionId;

        if (_mainSession != null)
        {
            var stale = _mainSession;
            _sessions.Remove(stale.SessionId);
            _mainSession = null;
            try { await stale.DisposeAsync(); } catch { }
        }

        // The CLI keeps session data on disk even after unloading it from memory.
        // Resuming with the original ID restores the full conversation history and
        // requires no UI update (session ID is unchanged).
        if (expiredSessionId != null)
        {
            try
            {
                var session = await _client!.ResumeSessionAsync(expiredSessionId, new ResumeSessionConfig
                {
                    Model = ActiveModel,
                    Streaming = true,
                    OnPermissionRequest = BuildPermissionHandler(),
                    OnUserInputRequest = BuildUserInputHandler(),
                    SystemMessage = BuildSystemMessage(),
                });

                _mainSession = session;
                _sessions[session.SessionId] = session;
                session.On(evt => HandleSessionEvent(session.SessionId, evt));
                EmitPendingStatus(session.SessionId);
                return;
            }
            catch { /* Session disk data also gone; fall through to a fresh session. */ }
        }

        await CreateMainSessionAsync();
    }

    private async Task CreateMainSessionAsync()
    {
        var agents    = LoadTierAgents();
        var skillDirs = LoadTierSkillDirectories();

        var session = await _client!.CreateSessionAsync(new SessionConfig
        {
            Model = ActiveModel,
            Streaming = true,
            OnPermissionRequest = BuildPermissionHandler(),
            OnUserInputRequest = BuildUserInputHandler(),
            SystemMessage = BuildSystemMessage(),
            CustomAgents     = agents.Count    > 0 ? agents    : null,
            SkillDirectories = skillDirs.Count > 0 ? skillDirs : null,
        });

        _mainSession = session;
        _sessions[session.SessionId] = session;
        session.On(evt => HandleSessionEvent(session.SessionId, evt));

        if (FleetMode)
        {
            try
            {
#pragma warning disable GHCP001
                var fleetResult = await session.Rpc.Fleet.StartAsync(
                    prompt: WorkingDirectory != null ? BuildFleetScopeDirective() : null);
#pragma warning restore GHCP001
                if (!fleetResult.Started)
                    throw new InvalidOperationException(
                        "Fleet mode could not be activated. " +
                        "The server returned Started=false — Fleet may not be available for the selected model or account.");
            }
            catch
            {
                // Roll back the session so the next send retries cleanly
                _sessions.Remove(session.SessionId);
                _mainSession = null;
                try { await session.DisposeAsync(); } catch { }
                throw;
            }
        }

        SessionCreated?.Invoke(this, new SessionEventArgs
        {
            SessionId = session.SessionId,
            IsSubAgent = false,
        });
        EmitPendingStatus(session.SessionId);
    }

    private void EmitPendingStatus(string sessionId)
    {
        if (_pendingStatusMessage == null) return;
        var msg = _pendingStatusMessage;
        _pendingStatusMessage = null;
        MessageReceived?.Invoke(this, new SessionMessageEventArgs
        {
            SessionId = sessionId,
            Content   = msg,
            Kind      = MessageKind.Status,
        });
    }

    private SystemMessageConfig? BuildSystemMessage()
    {
        var parts = new List<string>();

        // Scratchpad directive — always present when a working directory is set
        if (KopilotPath != null)
        {
            parts.Add(
                $"KOPILOT SCRATCHPAD: You have a dedicated scratch directory at \"{KopilotPath}\". " +
                "Write ALL temporary files here — SQLite databases, logs, intermediate outputs, diff backups, " +
                "and any other files you create during a task. " +
                "Never create temporary files in the project root or elsewhere.");
        }

        // Tiered instructions: Personal -> Org -> Project
        var loadedTiers = new List<string>();
        foreach (var (label, folder) in GetTierFolders())
        {
            var instructionsPath = Path.Combine(folder, "kopilot-instructions.md");
            if (!File.Exists(instructionsPath)) continue;
            try
            {
                var instructions = File.ReadAllText(instructionsPath);
                if (!string.IsNullOrWhiteSpace(instructions))
                {
                    parts.Add($"{label} INSTRUCTIONS (from kopilot-instructions.md):\n\n{instructions.Trim()}");
                    loadedTiers.Add(label);
                }
            }
            catch { /* best-effort; skip if unreadable */ }
        }
        if (loadedTiers.Count > 0)
            _pendingStatusMessage = $"Instructions loaded: {string.Join(", ", loadedTiers)}";

        // Mode-specific directive
        switch (ActiveMode)
        {
            case "Plan":
                parts.Add(
                    "PLAN MODE: Before taking any action, lay out a numbered step-by-step plan " +
                    "and wait for the user to confirm before executing. Always show your reasoning.");
                break;
            case "Autopilot":
                parts.Add(
                    "AUTOPILOT MODE: Work autonomously to complete the user's goal end-to-end. " +
                    "Use all available tools without asking for confirmation at each step. " +
                    "Summarise what you did when finished.");
                break;
        }

        // Fleet scope restriction
        if (FleetMode && WorkingDirectory != null)
        {
            parts.Add(BuildFleetScopeDirective());
        }

        if (parts.Count == 0) return null;

        return new SystemMessageConfig
        {
            Content = string.Join("\n\n", parts),
            Mode = SystemMessageMode.Append,
        };
    }

    private string BuildFleetScopeDirective() =>
        $"FLEET SCOPE RESTRICTION: You and every sub-agent you spawn are scoped strictly to the " +
        $"project root \"{WorkingDirectory}\". " +
        "Do NOT read, write, list, or access any path outside this root unless the user has " +
        "explicitly provided an absolute path to an external location in their message. " +
        "Treat all relative paths as relative to the project root. " +
        "If a task would require leaving the project root without explicit user permission, stop and ask.";

    // ── 3-Tier helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the environment dictionary to pass to the Copilot CLI process.
    /// Inherits the current process environment and sets
    /// <c>COPILOT_CUSTOM_INSTRUCTIONS_DIRS</c> so the CLI can discover
    /// <c>AGENTS.md</c> and <c>.github/instructions/**/*.instructions.md</c>
    /// files in the personal and org tier folders.
    ///
    /// The project tier is already covered by the <c>Cwd</c> option; the CLI
    /// natively loads instructions from its working directory.  Personal
    /// <c>copilot-instructions.md</c> is loaded automatically from
    /// <c>~/.copilot</c> by the CLI, but <c>AGENTS.md</c> and
    /// <c>.github/instructions/</c> in that folder require an explicit entry
    /// in this env var.
    ///
    /// Any directories already present in the process-level
    /// <c>COPILOT_CUSTOM_INSTRUCTIONS_DIRS</c> are preserved after the
    /// Kopilot-managed tiers so that user-level customisation is not lost.
    /// </summary>
    private IReadOnlyDictionary<string, string> BuildCliEnvironment()
    {
        // Inherit the full process environment so the CLI continues to work.
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string k && entry.Value is string v)
                env[k] = v;
        }

        // Build COPILOT_CUSTOM_INSTRUCTIONS_DIRS in Personal -> Org order.
        // Deduplication is case-insensitive (Windows paths).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new List<string>();

        var personal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot");
        if (Directory.Exists(personal) && seen.Add(personal))
            dirs.Add(personal);

        if (!string.IsNullOrEmpty(OrgFolder) && Directory.Exists(OrgFolder) && seen.Add(OrgFolder))
            dirs.Add(OrgFolder);

        // Preserve any extra directories the user has configured in the env var.
        if (env.TryGetValue("COPILOT_CUSTOM_INSTRUCTIONS_DIRS", out var existing))
        {
            foreach (var d in existing.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (seen.Add(d))
                    dirs.Add(d);
            }
        }

        if (dirs.Count > 0)
            env["COPILOT_CUSTOM_INSTRUCTIONS_DIRS"] = string.Join(",", dirs);
        else
            env.Remove("COPILOT_CUSTOM_INSTRUCTIONS_DIRS");

        return env;
    }

    /// <summary>
    /// Searches PATH for a <c>copilot.exe</c> installation and returns its full path.
    /// Returns <c>null</c> when not found; the SDK will then fall back to its bundled CLI.
    /// </summary>
    private static string? ResolveCliFromPath()
    {
        var pathVar = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var candidate = Path.Combine(trimmed, "copilot.exe");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Returns the active tier folders in Personal -> Org -> Project order.
    /// Only tiers whose root directory actually exists are included.
    /// </summary>
    internal IEnumerable<(string Label, string Path)> GetTierFolders()
    {
        var personal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");
        if (!string.IsNullOrEmpty(personal) && Directory.Exists(personal))
            yield return ("PERSONAL", personal);

        if (!string.IsNullOrEmpty(OrgFolder) && Directory.Exists(OrgFolder))
            yield return ("ORG", OrgFolder);

        if (!string.IsNullOrEmpty(WorkingDirectory) && Directory.Exists(WorkingDirectory))
            yield return ("PROJECT", WorkingDirectory);
    }

    /// <summary>
    /// Discovers agent definition files (*.md) from the <c>agents/</c> subdirectory of each
    /// tier folder.  A later tier's definition silently overrides an earlier tier's agent with
    /// the same name (project beats org beats personal).
    /// </summary>
    private List<CustomAgentConfig> LoadTierAgents()
    {
        var map = new Dictionary<string, CustomAgentConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, folder) in GetTierFolders())
        {
            var agentsDir = System.IO.Path.Combine(folder, "agents");
            if (!Directory.Exists(agentsDir)) continue;

            foreach (var file in Directory.GetFiles(agentsDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                var agent = ParseAgentFile(file);
                if (agent != null)
                    map[agent.Name] = agent;
            }
        }

        return [.. map.Values];
    }

    /// <summary>
    /// Returns all existing <c>skills/</c> subdirectories across the tier folders, in
    /// Personal -> Org -> Project order.
    /// </summary>
    private List<string> LoadTierSkillDirectories()
    {
        var dirs = new List<string>();

        foreach (var (_, folder) in GetTierFolders())
        {
            var skillsDir = System.IO.Path.Combine(folder, "skills");
            if (Directory.Exists(skillsDir))
                dirs.Add(skillsDir);
        }

        return dirs;
    }

    /// <summary>
    /// Parses an agent definition Markdown file.  Supports an optional YAML front matter
    /// block delimited by <c>---</c> lines that may supply <c>name</c>, <c>displayName</c>,
    /// <c>description</c>, and <c>tools</c>.  The file stem is used as the agent name when
    /// no front matter is present.  Returns null when the file cannot be read or has no
    /// usable prompt body.
    /// </summary>
    private static CustomAgentConfig? ParseAgentFile(string filePath)
    {
        string content;
        try { content = File.ReadAllText(filePath); }
        catch { return null; }

        content = content.Replace("\r\n", "\n");

        string? name        = null;
        string? displayName = null;
        string? description = null;
        List<string>? tools = null;
        string prompt       = content;

        if (content.StartsWith("---\n"))
        {
            var end = content.IndexOf("\n---\n", 4);
            if (end >= 0)
            {
                var frontMatter = content[4..end];
                prompt = content[(end + 5)..].TrimStart();

                var inTools = false;
                tools = [];

                foreach (var rawLine in frontMatter.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.Equals("tools:", StringComparison.OrdinalIgnoreCase))
                    {
                        inTools = true;
                        continue;
                    }

                    if (inTools)
                    {
                        if (line.StartsWith("- "))
                        {
                            tools.Add(line[2..].Trim());
                            continue;
                        }
                        inTools = false;
                    }

                    var col = line.IndexOf(':');
                    if (col < 0) continue;

                    var key = line[..col].Trim().ToLowerInvariant();
                    var val = line[(col + 1)..].Trim().Trim('"', '\'');

                    switch (key)
                    {
                        case "name":        name        = val; break;
                        case "displayname": displayName = val; break;
                        case "description": description = val; break;
                    }
                }

                if (tools.Count == 0) tools = null;
            }
        }

        name ??= System.IO.Path.GetFileNameWithoutExtension(filePath);

        if (string.IsNullOrWhiteSpace(prompt)) return null;

        return new CustomAgentConfig
        {
            Name        = name,
            DisplayName = displayName,
            Description = description,
            Tools       = tools,
            Prompt      = prompt,
        };
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
        _currentInputTokens = 0;
    }

    /// <summary>
    /// Asks the CLI to compact the current session in place, summarising history
    /// to free context-window headroom while preserving the session ID.
    /// Returns true on success; false if the SDK call throws (e.g. the experimental
    /// endpoint is unavailable for this server build).
    /// </summary>
    public async Task<bool> CompactSessionAsync()
    {
        if (_mainSession == null) return false;
        try
        {
#pragma warning disable GHCP001
            var result = await _mainSession.Rpc.History.CompactAsync();
#pragma warning restore GHCP001
            if (!result.Success) return false;

            // Optimistically zero the meter — the next AssistantUsageEvent will
            // restore the true value once the user sends another prompt.
            _currentInputTokens = 0;
            ContextUsageChanged?.Invoke(this, new ContextUsageEventArgs
            {
                SessionId       = _mainSession.SessionId,
                InputTokens     = 0,
                MaxPromptTokens = _currentMaxPromptTokens,
            });
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Tears down the current main session, opens a fresh one in the same workspace,
    /// and seeds it with <paramref name="summary"/> via <paramref name="seedPrompt"/>.
    /// The new session inherits the active model, mode, and Fleet setting.
    /// </summary>
    public async Task RestartSessionWithSummaryAsync(string summary, string seedPrompt)
    {
        await ResetSessionAsync();
        await EnsureSessionAsync();

        _currentInputTokens = 0;
        ContextUsageChanged?.Invoke(this, new ContextUsageEventArgs
        {
            SessionId       = _mainSession?.SessionId ?? "",
            InputTokens     = 0,
            MaxPromptTokens = _currentMaxPromptTokens,
        });

        var fullPrompt = seedPrompt + "\r\n\r\n" + summary;
        await _mainSession!.SendAsync(new MessageOptions { Prompt = fullPrompt });
    }

    /// <summary>
    /// Resets the client state so a new connection can be established
    /// (e.g. after DisposeAsync to reconnect with a different working directory).
    /// </summary>
    public void Reset()
    {
        StopKeepAlive();
        _lifecycleSubscription?.Dispose();
        _lifecycleSubscription = null;
        _lifecycleDeletedSubscription?.Dispose();
        _lifecycleDeletedSubscription = null;
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
                    SubAgentModel       = sa.Data.Model,
                    SubAgentTotalCalls  = sa.Data.TotalToolCalls,
                    SubAgentTotalTokens = sa.Data.TotalTokens,
                    SubAgentDurationMs  = sa.Data.DurationMs,
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
                    SubAgentModel       = sa.Data.Model,
                    SubAgentTotalCalls  = sa.Data.TotalToolCalls,
                    SubAgentTotalTokens = sa.Data.TotalTokens,
                    SubAgentDurationMs  = sa.Data.DurationMs,
                });
                break;

            case SkillInvokedEvent skill:
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId           = sessionId,
                    Content             = skill.Data.Name,
                    Kind                = MessageKind.SkillInvoked,
                    SubAgentDescription = skill.Data.Description,
                });
                break;

            case SessionCustomAgentsUpdatedEvent agents:
                if (agents.Data.Agents.Length == 0 && agents.Data.Errors.Length == 0) break;
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content   = FormatCustomAgentsSummary(agents.Data),
                    Kind      = MessageKind.CustomAgentsUpdated,
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

            case CommandsChangedEvent cmds:
                // Cache SDK-registered slash commands for the main session only.
                if (sessionId == _mainSession?.SessionId)
                    _cachedSdkCommands = cmds.Data.Commands;
                break;

            case AssistantUsageEvent usage:
                // Update the context-window meter from the main user-driven flow only:
                // ignore sub-agents (ParentToolCallId set) and non-user initiators
                // (e.g. "sub-agent", "mcp-sampling").  The SDK schema documents
                // Initiator as absent for user-initiated calls, but some models
                // (observed: claude-opus-4.7) emit the literal string "user"
                // instead, so we accept both.  Use an allow-list so any future
                // non-user initiator type stays correctly excluded.
                if (sessionId == _mainSession?.SessionId
                    && usage.Data.ParentToolCallId == null
                    && IsUserInitiatedUsage(usage.Data.Initiator)
                    && usage.Data.InputTokens.HasValue)
                {
                    _currentInputTokens = usage.Data.InputTokens.Value;
                    if (_currentMaxPromptTokens <= 0)
                        _currentMaxPromptTokens = LookupMaxPromptTokens(usage.Data.Model);
                    ContextUsageChanged?.Invoke(this, new ContextUsageEventArgs
                    {
                        SessionId       = sessionId ?? "",
                        InputTokens     = _currentInputTokens,
                        MaxPromptTokens = _currentMaxPromptTokens,
                    });
                }
                break;

            case SessionInfoEvent info
                when sessionId == _mainSession?.SessionId
                  && string.Equals(info.Data.InfoType, "context_window", StringComparison.OrdinalIgnoreCase):
                MessageReceived?.Invoke(this, new SessionMessageEventArgs
                {
                    SessionId = sessionId,
                    Content   = info.Data.Message ?? "",
                    Kind      = MessageKind.Status,
                });
                break;
        }
    }

    // ── Argument / result summarisation helpers ───────────────────────────────

    private static string FormatCustomAgentsSummary(SessionCustomAgentsUpdatedData data)
    {
        var invocable = data.Agents.Where(a => a.UserInvocable).Select(a => a.DisplayName).ToArray();
        var sb = new StringBuilder();
        if (invocable.Length > 0)
            sb.Append($"{invocable.Length} custom agent{(invocable.Length == 1 ? "" : "s")} loaded: {string.Join(", ", invocable)}");
        foreach (var err  in data.Errors)   sb.Append($"\r\n  ✗ {err}");
        foreach (var warn in data.Warnings) sb.Append($"\r\n  ⚠ {warn}");
        return sb.ToString();
    }

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
        StopKeepAlive();
        _lifecycleSubscription?.Dispose();
        _lifecycleDeletedSubscription?.Dispose();

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
