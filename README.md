# Kopilot

**Kopilot** (nicknamed *Kopi*) is a Windows desktop GUI for the [GitHub Copilot SDK](libs/copilot-sdk). It wraps the Copilot CLI experience in a friendly WinForms interface so you can chat with Copilot, approve tool operations, and manage your session — all without leaving a graphical window.

---

## Requirements

- Windows 10 or later
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- A GitHub account with Copilot access (authenticated via `gh` CLI or environment)

---

## Getting Started

1. **Build** the solution:
   ```
   dotnet build Kopilot.sln
   ```

2. **Run** the app:
   ```
   dotnet run --project Kopilot/Kopilot.csproj
   ```
   Or launch the compiled executable directly from `Kopilot/bin/`.

3. **Connect to a project folder:**
   Click **📂 Open Folder…** in the top-left and select your working directory. This establishes a Copilot session rooted at that folder.

4. **Start chatting:**
   Type your prompt and press **Send** (or `Ctrl+Enter`).

---

## User Interface

### Main Window

The window is split into two panels:

- **Top panel** — prompt input and session controls
- **Bottom panel** — streaming output from Copilot

---

### Top Toolbar

| Control | Description |
|---|---|
| **📂 Open Folder…** | Select a project folder and connect to Copilot. Required before sending prompts. |
| **Model** | Choose the AI model: GPT-4.1, GPT-5, Claude Sonnet 4.5/4.6, Claude Opus 4.5, and others. |
| **Mode** | Execution mode — see [Modes](#modes) below. |
| **Fleet** ☐ | Enable Fleet mode to spawn multiple parallel sub-agents for complex tasks. |
| **Auto-approve** ☐ | Automatically approve all tool operations without showing a permission dialog. |
| **Send** | Submit the current prompt. Shortcut: `Ctrl+Enter`. |
| **Stop** | Cancel the current in-progress response. |

---

### Prompt Box

The large text area below the toolbar. Type your request here.

- Press **`Ctrl+Enter`** to send.
- **Drag and drop** files directly onto the box to attach them.
- Use the **▲ / ▼** history buttons on the left edge to navigate previously sent prompts.

---

### Attachments

Located above the output panel:

| Button | Description |
|---|---|
| **📄 Add File** | Attach a single file to the next prompt. |
| **📁 Add Folder** | Attach a folder to the next prompt. |

Attached items appear as labelled chips with a **×** button to remove them before sending.

---

### Output Panel

Displays the streaming Copilot response in real time. Color coding:

| Color | Meaning |
|---|---|
| 🔵 Blue | Your prompt |
| 🟢 Green | Copilot's response |
| 🟡 Yellow | Tool / operation activity |
| 🔴 Red | Errors |

---

### Quick Commands (bottom bar)

One-click shortcuts for common actions:

| Button | What it does |
|---|---|
| **❓ Help** | Ask Copilot to describe its own capabilities and available tools. |
| **⚡ PowerShell** | Open a PowerShell terminal in the current project folder (loads `scripts.ps1` if present). |
| **📂 Explorer** | Open File Explorer at the current project folder. |
| **💻 VS Code** | Launch VS Code in the project folder and send the `/ide` command to connect Copilot to it. |
| **📝 Summarize** | Request a summary of the current session from Copilot. |
| **🗑 Clear** | Clear the output panel (asks for confirmation). |
| **💾 Backup** | Ask Copilot to write a Markdown resume file for the current session. |

---

### Status Bar

The bar at the bottom of the window shows:

- **Connection status** — folder path once connected, or "Not connected"
- **Copilot version**
- **Agent status** — live activity message (e.g., "Ready for next command")
- **Session info**

---

## Modes

Select the execution mode from the **Mode** dropdown:

| Mode | Behaviour |
|---|---|
| **Standard** | Normal conversational chat. Copilot responds directly. |
| **Plan** | Copilot plans a sequence of actions before executing them. Good for complex multi-step tasks. |
| **Autopilot** | Fully autonomous execution with minimal prompting. Copilot decides and acts with little intervention. |

---

## Fleet Mode

When the **Fleet** checkbox is enabled, Copilot can spawn multiple parallel sub-agents to work on different parts of a task simultaneously. The output panel will show each agent's progress. The session completes when the last sub-agent finishes.

Best suited for large refactors, multi-file generation, or tasks that can be broken into independent workstreams.

---

## Tool Permission Dialog

When Copilot wants to execute an operation (and **Auto-approve** is off), a permission dialog appears:

| Button | Effect |
|---|---|
| **✓ Allow** | Approve this single operation and continue. |
| **✓ Approve Similar** | Approve this operation and all future operations of the same type in this session. |
| **✗ Deny** | Reject the operation. Copilot will handle the refusal and may suggest an alternative. |

Operation types include shell commands, file reads/writes, MCP tool calls, URL fetches, memory access, and hook invocations.

> **Tip:** Turn on **Auto-approve** in the toolbar if you trust the current task and want uninterrupted execution.

---

## User Input Dialog

If Copilot needs clarification mid-task, a dialog will appear with a question and (optionally) a list of predefined choices.

- Select an option from the list, **or** type a custom answer in the text box.
- Press **Submit** (or `Enter`) to continue.

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Enter` | Send prompt |
| `Enter` (in input dialog) | Submit answer |

---

## Project Structure

```
kopilot/
├── Kopilot/               # Main WinForms application
│   ├── MainForm.cs        # Primary window and interaction logic
│   ├── CopilotService.cs  # Copilot SDK integration
│   ├── AudioService.cs    # Sound cues
│   ├── PromptHistory.cs   # Prompt navigation history
│   ├── AppTheme.cs        # Dark theme color definitions
│   ├── PermissionDialog.cs
│   ├── UserInputDialog.cs
│   └── audio/             # Audio assets
├── libs/
│   └── copilot-sdk/       # GitHub Copilot SDK (submodule)
└── Kopilot.sln
```

---

## Tips

- **Start simple** — open a folder and ask *"What does this project do?"* to orient yourself.
- **Use Plan mode** for big tasks so you can review the plan before Copilot acts.
- **Summarize often** — click 📝 Summarize to capture progress before starting a new topic.
- **Backup your session** — click 💾 Backup to save a Markdown resume file you can attach to a future session.
- **Attach context** — drag in relevant files or folders before sending a prompt to give Copilot targeted context.
