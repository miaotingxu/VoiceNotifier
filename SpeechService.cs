using System.Speech.Synthesis;

namespace VoiceNotifier;
internal sealed class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly object _sync = new();
    public SpeechService(AppConfig config) { _synth.Rate = config.Rate; _synth.Volume = config.Volume; }
    public void Speak(string text) { if (string.IsNullOrWhiteSpace(text)) return; lock (_sync) _synth.Speak(text); }
    public void Dispose() { lock (_sync) _synth.Dispose(); }
}
