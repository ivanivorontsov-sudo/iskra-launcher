using Iskra.Core.Themes;

namespace Iskra.Core.Media;

public enum SoundEvent { Open, Close, Launch, Error, Notify, Warn, DeviceIn, DeviceOut, Click, Theme }

/// <summary>
/// Процедурный синтезатор звуков тем: всё генерируется кодом, никаких чужих сэмплов и лицензий.
/// Выдаёт WAV (PCM 16 бит, моно, 44,1 кГц).
/// </summary>
public static class SoundSynth
{
    public const int Rate = 44100;
    private enum Wave { Sine, Triangle, Square, Pulse25, Saw, Noise, Bell }

    private readonly record struct Note(double Freq, double Start, double Dur, double Vol = 1, double GlideTo = 0);

    private sealed record Voice(Wave Wave, double Attack, double Decay, double Lowpass, double Echo, double EchoDelay = 0.11);

    public static byte[] Wav(SoundPack pack, SoundEvent ev)
    {
        var samples = Render(pack, ev);
        return ToWav(samples);
    }

    public static float[] Render(SoundPack pack, SoundEvent ev)
    {
        if (pack == SoundPack.None) return new float[Rate / 50];
        var (voice, notes, gain) = Design(pack, ev);
        double total = notes.Max(n => n.Start + n.Dur) + voice.Decay + (voice.Echo > 0 ? voice.EchoDelay * 4 : 0) + 0.02;
        int len = (int)(total * Rate);
        var buf = new float[len];
        var rng = new Random(pack.GetHashCode() * 31 + (int)ev);
        foreach (var n in notes)
        {
            int start = (int)(n.Start * Rate);
            int count = (int)((n.Dur + voice.Decay) * Rate);
            double phase = 0;
            for (int i = 0; i < count && start + i < len; i++)
            {
                double t = (double)i / Rate;
                double f = n.GlideTo > 0 ? n.Freq + (n.GlideTo - n.Freq) * Math.Min(1, t / Math.Max(0.001, n.Dur)) : n.Freq;
                phase += f / Rate;
                double env = t < voice.Attack ? t / voice.Attack
                    : t < n.Dur ? 1.0
                    : Math.Exp(-(t - n.Dur) / Math.Max(0.005, voice.Decay / 4));
                buf[start + i] += (float)(Osc(voice.Wave, phase, rng) * env * n.Vol);
            }
        }
        // Однополюсный ФНЧ — смягчает «цифровую» резкость.
        if (voice.Lowpass > 0)
        {
            double a = Math.Exp(-2 * Math.PI * voice.Lowpass / Rate), y = 0;
            for (int i = 0; i < len; i++) { y = (1 - a) * buf[i] + a * y; buf[i] = (float)y; }
        }
        // Эхо (простая линия задержки с обратной связью).
        if (voice.Echo > 0)
        {
            int d = (int)(voice.EchoDelay * Rate);
            for (int i = d; i < len; i++) buf[i] += (float)(buf[i - d] * voice.Echo);
        }
        // Нормализация и мягкие края.
        float peak = 0.0001f;
        foreach (var s in buf) peak = Math.Max(peak, Math.Abs(s));
        float k = (float)(gain / peak);
        int fade = Math.Min(len / 4, Rate / 200);
        for (int i = 0; i < len; i++)
        {
            float edge = i < fade ? (float)i / fade : i > len - fade ? (float)(len - i) / fade : 1f;
            buf[i] = Math.Clamp(buf[i] * k * edge, -1f, 1f);
        }
        return buf;
    }

    private static double Osc(Wave w, double phase, Random rng)
    {
        double p = phase - Math.Floor(phase);
        return w switch
        {
            Wave.Sine => Math.Sin(2 * Math.PI * p),
            Wave.Triangle => 4 * Math.Abs(p - 0.5) - 1,
            Wave.Square => p < 0.5 ? 0.6 : -0.6,
            Wave.Pulse25 => p < 0.25 ? 0.6 : -0.6,
            Wave.Saw => (2 * p - 1) * 0.7,
            Wave.Noise => rng.NextDouble() * 2 - 1,
            Wave.Bell => Math.Sin(2 * Math.PI * p) + 0.45 * Math.Sin(2 * Math.PI * p * 2.76) + 0.2 * Math.Sin(2 * Math.PI * p * 5.4),
            _ => 0,
        };
    }

    private static double N(int semitonesFromA4) => 440.0 * Math.Pow(2, semitonesFromA4 / 12.0);

    private static (Voice voice, Note[] notes, double gain) Design(SoundPack pack, SoundEvent ev)
    {
        // Аккорды/мотивы на событие; у каждого пакета свой тембр.
        int[] up = { 3, 7, 10 }, down = { 10, 7, 3 }, err = { -2, -6 }, warn = { 5, 1 };
        int[] motif = ev switch
        {
            SoundEvent.Open => new[] { 3, 10 },
            SoundEvent.Close => new[] { 10, 3 },
            SoundEvent.Launch => new[] { 7, 15 },
            SoundEvent.Error => err,
            SoundEvent.Warn => warn,
            SoundEvent.Notify => up,
            SoundEvent.DeviceIn => new[] { 0, 7, 12 },
            SoundEvent.DeviceOut => new[] { 12, 7, 0 },
            SoundEvent.Click => new[] { 15 },
            SoundEvent.Theme => new[] { 0, 4, 7, 12, 16 },
            _ => down,
        };
        double step = ev is SoundEvent.Click ? 0.02 : 0.07;
        double dur = ev is SoundEvent.Click ? 0.015 : 0.06;
        Voice v;
        int octave = 12;
        double gain = 0.5;
        switch (pack)
        {
            case SoundPack.Glass: v = new(Wave.Bell, 0.002, 0.5, 6000, 0.25); octave = 24; step *= 0.9; break;
            case SoundPack.Metro: v = new(Wave.Sine, 0.001, 0.12, 5000, 0); octave = 12; step *= 0.6; dur *= 0.5; break;
            case SoundPack.Soft: v = new(Wave.Triangle, 0.006, 0.3, 2400, 0.15); octave = 12; break;
            case SoundPack.Neon: v = new(Wave.Saw, 0.003, 0.28, 2800, 0.35, 0.09); octave = 12; break;
            case SoundPack.Paper: v = new(Wave.Noise, 0.004, 0.08, 3200, 0); octave = 0; step *= 1.4; dur *= 1.3; gain = 0.32; break;
            case SoundPack.Terminal: v = new(Wave.Square, 0.001, 0.02, 7000, 0); octave = 15; step *= 1.2; break;
            case SoundPack.Chip: v = new(Wave.Pulse25, 0.001, 0.05, 9000, 0); octave = 12; step *= 0.8; break;
            case SoundPack.Space: v = new(Wave.Sine, 0.01, 0.6, 3000, 0.45, 0.16); octave = 12; break;
            case SoundPack.Zen: v = new(Wave.Bell, 0.004, 0.9, 4200, 0.3, 0.14); octave = 12; motif = Pentatonic(motif); break;
            default: v = new(Wave.Sine, 0.005, 0.2, 4000, 0); break;
        }
        var notes = new Note[motif.Length];
        for (int i = 0; i < motif.Length; i++)
        {
            double glide = pack == SoundPack.Space ? N(motif[i] + octave + 2) : 0;
            notes[i] = new Note(N(motif[i] + octave), i * step, dur, 1.0 - i * 0.12, glide);
        }
        return (v, notes, gain);
    }

    private static int[] Pentatonic(int[] m)
    {
        int[] scale = { 0, 2, 4, 7, 9, 12, 14, 16, 19 };
        return m.Select(x => scale.OrderBy(s => Math.Abs(s - x)).First()).ToArray();
    }

    public static byte[] ToWav(float[] samples)
    {
        using var ms = new MemoryStream(44 + samples.Length * 2);
        using var w = new BinaryWriter(ms);
        int dataLen = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples) w.Write((short)Math.Round(s * 32000));
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Соответствие событий звуковой схемы Windows событиям синтезатора.</summary>
    public static SoundEvent ForWindowsEvent(string ev) => ev switch
    {
        ".Default" => SoundEvent.Warn,
        "SystemAsterisk" => SoundEvent.Notify,
        "SystemExclamation" => SoundEvent.Warn,
        "SystemHand" => SoundEvent.Error,
        "DeviceConnect" => SoundEvent.DeviceIn,
        "DeviceDisconnect" => SoundEvent.DeviceOut,
        "WindowsUAC" => SoundEvent.Open,
        _ => SoundEvent.Notify,
    };
}
