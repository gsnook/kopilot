using System.Collections.Generic;

namespace Kopilot;

/// <summary>
/// Lightweight summary of a custom agent definition surfaced to the UI for the
/// "List Agents" dialog. Built once from agent .md files when sessions/tier
/// folders change so the dialog can be opened without re-parsing.
/// </summary>
public sealed class AgentInfo
{
	public required string  Name        { get; init; }
	public          string? DisplayName { get; init; }
	public          string? Description { get; init; }
	public required string  FilePath    { get; init; }
	public required string  Tier        { get; init; }
}

/// <summary>
/// Lightweight summary of a skill (SKILL.md) surfaced to the UI for the
/// "List Skills" dialog. Triggers are extracted from the "## When to Use This Skill"
/// bullets when present.
/// </summary>
public sealed class SkillInfo
{
	public required string  Name        { get; init; }
	public          string? Description { get; init; }
	public required IReadOnlyList<string> Triggers { get; init; }
	public required string  FolderPath  { get; init; }
	public required string  Tier        { get; init; }
}
