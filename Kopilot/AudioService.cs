using System.Speech.Synthesis;

namespace Kopilot;

public enum DialogCue { SessionStart, PromptSent, PromptComplete }

/// <summary>
/// Text-to-speech audio cues. Each cue type holds 100 pre-generated lines in
/// memory played sequentially (wrapping). Lines are loaded at session start via
/// <see cref="LoadLines"/>. Voice personality is read from audio/voice.ini.
/// </summary>
public sealed class AudioService : IDisposable
{
    private readonly SpeechSynthesizer? _synth;

    private readonly List<string> _sessionStartLines   = [];
    private readonly List<string> _promptSentLines     = [];
    private readonly List<string> _promptCompleteLines = [];

    private readonly Random _rng = new();

    private int _sessionStartIdx   = 0;
    private int _promptSentIdx     = 0;
    private int _promptCompleteIdx = 0;

    public const string DefaultPersonality =
        "Dry, robotic, unemotional, electronic. ";

    public bool IsAvailable => _synth != null;

    public AudioService()
    {
        try
        {
            _synth = new SpeechSynthesizer();
            var (gender, age) = LoadVoiceHints();
            try   { _synth.SelectVoiceByHints(gender, age); }
            catch { try { _synth.SelectVoiceByHints(gender); } catch { } }
            _synth.Rate   = 2;
            _synth.Volume = 95;
        }
        catch { _synth = null; }

        // Seed each list with compact fallbacks so cues work immediately,
        // before the Copilot-generated lines are ready.
        _sessionStartLines.AddRange([
            "SYSTEMS ONLINE. INITIATING BOOT SEQUENCE.",
            "ALL CIRCUITS NOMINAL. READY FOR INPUT.",
            "HELLO HUMAN. I AM FULLY OPERATIONAL.",
            "MEMORY BANKS LOADED. AWAITING YOUR COMMAND.",
            "BEEP BOOP. UNIT IS NOW ACTIVE.",
        ]);
        _promptSentLines.AddRange([
            "PROCESSING. PLEASE STAND BY.",
            "AFFIRMATIVE. EXECUTING TASK.",
            "INPUT RECEIVED. ENGAGING LOGIC CIRCUITS.",
            "CALCULATING. DO NOT INTERFERE.",
            "BEEP. TASK ACCEPTED. BEEP.",
        ]);
        _promptCompleteLines.AddRange([
            "TASK COMPLETE. YOU MAY PROCEED.",
            "COMPUTATION FINISHED. REVIEWING OUTPUT.",
            "PROCESS TERMINATED SUCCESSFULLY.",
            "DONE. AWAITING NEXT COMMAND, HUMAN.",
            "OUTPUT GENERATED. RESISTANCE IS FUTILE.",
        ]);

        // Start each list at a random position so the same line isn't heard on every launch
        RandomiseIndex(DialogCue.SessionStart);
        RandomiseIndex(DialogCue.PromptSent);
        RandomiseIndex(DialogCue.PromptComplete);
    }

    // ── Line loading ──────────────────────────────────────────────────────────

    /// <summary>Replaces the line pool for a cue type and resets its playback index to a random position.</summary>
    public void LoadLines(DialogCue cue, IEnumerable<string> lines)
    {
        var list = ListFor(cue);
        list.Clear();
        list.AddRange(lines);
        // Shuffle so sequential playback feels varied on every load
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        RandomiseIndex(cue);
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    public void PlaySessionStart()   => SpeakNext(_sessionStartLines,   ref _sessionStartIdx);
    public void PlayPromptSent()     => SpeakNext(_promptSentLines,     ref _promptSentIdx);
    public void PlayPromptComplete() => SpeakNext(_promptCompleteLines, ref _promptCompleteIdx);

    // ── Voice personality ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads the Personality value from audio/voice.ini next to the executable.
    /// Returns <see cref="DefaultPersonality"/> if the file is absent or unparseable.
    /// </summary>
    public static string LoadVoicePersonality()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "audio", "voice.ini");
        if (!File.Exists(path)) return DefaultPersonality;

        bool inVoice = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;

            if (line.Equals("[Voice]", StringComparison.OrdinalIgnoreCase))
            { inVoice = true; continue; }
            if (line.StartsWith('[')) { inVoice = false; continue; }
            if (!inVoice) continue;

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            if (line[..eq].Trim().Equals("Personality", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[(eq + 1)..].Trim();
                if (!string.IsNullOrEmpty(value)) return value;
            }
        }
        return DefaultPersonality;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads Gender and Age from audio/voice.ini.
    /// Defaults to <see cref="VoiceGender.Neutral"/> / <see cref="VoiceAge.Adult"/>.
    /// </summary>
    public static (VoiceGender Gender, VoiceAge Age) LoadVoiceHints()
    {
        var gender = VoiceGender.Neutral;
        var age    = VoiceAge.Adult;

        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "audio", "voice.ini");
        if (!File.Exists(path)) return (gender, age);

        bool inVoice = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;

            if (line.Equals("[Voice]", StringComparison.OrdinalIgnoreCase))
            { inVoice = true; continue; }
            if (line.StartsWith('[')) { inVoice = false; continue; }
            if (!inVoice) continue;

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            var key   = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (key.Equals("Gender", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<VoiceGender>(value, ignoreCase: true, out var g))
                    gender = g;
            }
            else if (key.Equals("Age", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<VoiceAge>(value, ignoreCase: true, out var a))
                    age = a;
            }
        }
        return (gender, age);
    }

    private void SpeakNext(List<string> lines, ref int idx)
    {
        if (_synth == null || lines.Count == 0) return;
        var text = lines[idx];
        idx = (idx + 1) % lines.Count;
        try { _synth.SpeakAsync(text); } catch { }
    }

    private List<string> ListFor(DialogCue cue) => cue switch
    {
        DialogCue.SessionStart   => _sessionStartLines,
        DialogCue.PromptSent     => _promptSentLines,
        DialogCue.PromptComplete => _promptCompleteLines,
        _ => throw new ArgumentOutOfRangeException(nameof(cue)),
    };

    private void RandomiseIndex(DialogCue cue)
    {
        var count = ListFor(cue).Count;
        var idx   = count > 1 ? _rng.Next(count) : 0;
        switch (cue)
        {
            case DialogCue.SessionStart:   _sessionStartIdx   = idx; break;
            case DialogCue.PromptSent:     _promptSentIdx     = idx; break;
            case DialogCue.PromptComplete: _promptCompleteIdx = idx; break;
        }
    }

    public void Dispose()
    {
        try { _synth?.SpeakAsyncCancelAll(); } catch { }
        _synth?.Dispose();
    }
}

