# Kopi Voice — Audio Cues

Kopilot uses Windows SAPI text-to-speech (System.Speech.Synthesis) for voice cues.

## How It Works

When you open a folder and connect, Kopilot generates **100 spoken lines** for each
of three cue types by asking Copilot itself — so the dialog is always fresh.
The lines are held in memory and played **sequentially**, wrapping back to the start
once all 100 have been used.

|Cue Type|When spoken|
|-|-|
|Session-start|When a Copilot session connects|
|Prompt-sent|When a prompt is submitted|
|Prompt-complete|When Copilot finishes responding|

Generation happens in the background after connecting, so the first cue may be
silent if you send a message before generation completes.

## Customising the Voice

Edit **voice.ini** in this folder to change the personality that guides line generation.
See the comments inside that file for tips.

## Voice Settings (AudioService.cs)

* Gender: Male Robot
* Rate: 2 (slightly faster than default for an energetic feel)
* Volume: 95

