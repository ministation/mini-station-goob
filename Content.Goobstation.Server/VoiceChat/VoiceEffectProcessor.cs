// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.VoiceChat;
using NWaves.Effects;
using NWaves.Effects.Base;
using NWaves.Filters.Base;
using NWaves.Filters.BiQuad;
using NWaves.Signals.Builders;
using NWaves.Signals.Builders.Base;
using NWaves.Utils;

namespace Content.Goobstation.Server.VoiceChat;

public enum VoiceMuffle : byte
{
    None,
    Light,
    Heavy,
}

public readonly record struct VoiceEffectSettings(VoiceEffect Effect, VoiceMuffle Muffle, bool Megaphone = false);

public sealed class VoiceEffectProcessor
{
    private const int SampleRate = VoiceCodec.SampleRate;
    private const int FftSize = 512;
    private const int HopSize = 128;

    private VoiceEffect _effect = VoiceEffect.None;
    private VoiceMuffle _muffle = VoiceMuffle.None;
    private bool _megaphone;
    private IOnlineFilter? _voice;
    private IOnlineFilter? _megaphoneFilter;
    private IOnlineFilter? _muffleFilter;

    public void Process(short[] buffer, int offset, int count, VoiceEffectSettings settings)
    {
        var effect = settings.Effect;
        var muffle = settings.Muffle;
        if (effect == VoiceEffect.Muffled)
        {
            effect = VoiceEffect.None;
            muffle = VoiceMuffle.Heavy;
        }

        if (effect != _effect)
        {
            _effect = effect;
            _voice = CreateVoice(effect);
        }

        if (muffle != _muffle)
        {
            _muffle = muffle;
            _muffleFilter = CreateMuffle(muffle);
        }

        if (settings.Megaphone != _megaphone)
        {
            _megaphone = settings.Megaphone;
            _megaphoneFilter = _megaphone ? CreateMegaphone() : null;
        }

        if (_voice == null && _megaphoneFilter == null && _muffleFilter == null)
            return;

        for (var i = offset; i < offset + count; i++)
        {
            var sample = buffer[i] / 32768f;
            if (_voice != null)
                sample = _voice.Process(sample);
            if (_megaphoneFilter != null)
                sample = _megaphoneFilter.Process(sample);
            if (_muffleFilter != null)
                sample = _muffleFilter.Process(sample);

            // Mini: мягкий ограничитель в конце цепочки: пресеты с усиленем больше
            // единицы иначе уходят в жесткий клип и звучат как треск.
            buffer[i] = (short) Math.Clamp(Limit(sample) * 32767f, short.MinValue, short.MaxValue);
        }
    }

    private static float Limit(float sample)
    {
        const float knee = 0.8f;
        var magnitude = MathF.Abs(sample);
        if (magnitude <= knee)
            return sample;

        var limited = knee + (1f - knee) * MathF.Tanh((magnitude - knee) / (1f - knee));
        return MathF.CopySign(limited, sample);
    }

    private static IOnlineFilter? CreateVoice(VoiceEffect effect)
    {
        return effect switch
        {
            VoiceEffect.Robot => Chain(
                Mix(new RobotEffect(HopSize, FftSize), 1.6f)),
            VoiceEffect.Alien => Chain(
                PitchShift(0.8, 0.4f),
                Mix(new TremoloEffect(SampleRate, 0.5f, 28f, 1f), 1f),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 0f, -2f), 1f)),
            VoiceEffect.Abductor => Chain(
                PitchShift(1.35, 0.72f),
                new RingModulator(90f, 0.45f),
                Mix(new VibratoEffect(SampleRate, 6f, 0.002f), 1f)),
            VoiceEffect.Deep => Chain(
                PitchShift(0.7, 0.35f)),
            VoiceEffect.High => Chain(
                PitchShift(1.5, 0.45f)),
            VoiceEffect.Whisper => Chain(
                Mix(new WhisperEffect(HopSize, FftSize), 1.35f)),
            VoiceEffect.Radio => Chain(
                new HighPassFilter(350.0 / SampleRate, 0.707),
                new LowPassFilter(2800.0 / SampleRate, 0.707),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 4f, -4f), 1f),
                new NoiseMixer(0.012f)),
            // Mini: voice mods picked on the voice chat web page.
            VoiceEffect.Echo => Chain(
                Mix(new EchoEffect(SampleRate, 0.24f, 0.38f, InterpolationMode.Linear, 0.5f), 0.85f),
                new GainFilter(1.15f)),
            VoiceEffect.Cave => Chain(
                new HighPassFilter(180.0 / SampleRate, 0.707),
                Mix(new EchoEffect(SampleRate, 0.3f, 0.52f, InterpolationMode.Linear, 0.6f), 0.8f),
                new LowPassFilter(2600.0 / SampleRate, 0.707),
                new DcBlocker(),
                new GainFilter(1.4f)),
            VoiceEffect.Cathedral => Chain(
                new HighPassFilter(200.0 / SampleRate, 0.707),
                Mix(new EchoEffect(SampleRate, 0.05f, 0.4f, InterpolationMode.Linear, 0.2f), 0.5f, 0.7f),
                Mix(new EchoEffect(SampleRate, 0.17f, 0.55f, InterpolationMode.Linear, 0.3f), 0.45f, 0.75f),
                new LowPassFilter(3200.0 / SampleRate, 0.707),
                new DcBlocker(),
                new GainFilter(1.1f)),
            VoiceEffect.Ghost => Chain(
                PitchShift(0.8, 0.9f),
                Mix(new VibratoEffect(SampleRate, 0.8f, 0.004f), 1f),
                Mix(new EchoEffect(SampleRate, 0.35f, 0.45f, InterpolationMode.Linear, 0.6f), 0.5f),
                Mix(new TremoloEffect(SampleRate, 0.3f, 0.7f, 1f), 1f),
                new DcBlocker(),
                new GainFilter(0.85f)),
            VoiceEffect.Demon => Chain(
                PitchShift(0.7, 1f),
                new LowShelfFilter(120.0 / SampleRate, 1.0, 1.5),
                new LowPassFilter(2200.0 / SampleRate, 0.707),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 2.5f, -3f), 1f),
                Mix(new EchoEffect(SampleRate, 0.16f, 0.32f, InterpolationMode.Linear, 0.4f), 0.3f),
                new DcBlocker(),
                new GainFilter(1.5f)),
            VoiceEffect.Telephone => Chain(
                new HighPassFilter(400.0 / SampleRate, 1.2),
                new LowPassFilter(3000.0 / SampleRate, 1.2),
                Mix(new BitCrusherEffect(8), 1f),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 2f, -4f), 1f),
                new GainFilter(1.8f)),
            VoiceEffect.Megaphone => Chain(
                new HighPassFilter(500.0 / SampleRate, 0.9),
                new LowPassFilter(4000.0 / SampleRate, 0.9),
                Mix(new DistortionEffect(DistortionMode.HardClipping, 8f, -8f), 1f),
                Mix(new EchoEffect(SampleRate, 0.006f, 0.3f, InterpolationMode.Linear, 0.05f), 0.45f),
                new DcBlocker(),
                new GainFilter(2.5f)),
            VoiceEffect.Bitcrush => Chain(
                Mix(new BitCrusherEffect(6), 1f),
                new HighPassFilter(150.0 / SampleRate, 0.707),
                new LowPassFilter(6000.0 / SampleRate, 0.707),
                new GainFilter(1.2f)),
            VoiceEffect.Underwater => Chain(
                new LowPassFilter(500.0 / SampleRate, 0.707),
                new LowPassFilter(500.0 / SampleRate, 0.707),
                Mix(new ChorusEffect(SampleRate, new[] { 0.5f }, new[] { 0.004f }), 0.6f, 0.6f),
                Mix(new TremoloEffect(SampleRate, 0.15f, 0.3f, 1f), 1f),
                new GainFilter(1.5f)),
            VoiceEffect.Vibrato => Chain(
                Mix(new VibratoEffect(SampleRate, 5.5f, 0.004f), 1f, 0.25f),
                new GainFilter(1.1f)),
            VoiceEffect.OldRadio => Chain(
                new HighPassFilter(500.0 / SampleRate, 0.9),
                new LowPassFilter(2500.0 / SampleRate, 0.9),
                Mix(new DistortionEffect(DistortionMode.SoftClipping, 2f, -4f), 1f),
                Mix(new BitCrusherEffect(9), 1f),
                Mix(new VibratoEffect(SampleRate, 0.4f, 0.002f), 0.5f, 0.6f),
                new NoiseMixer(0.012f),
                new GainFilter(2f)),
            // Mini: female voice presets. The phase vocoder lifts the pitch, the shelf filters
            // brighten the upper formants and the high-pass removes the male chest rumble.
            VoiceEffect.Female => Chain(
                new HighPassFilter(120.0 / SampleRate, 0.707),
                PitchShift(1.2, 0.95f),
                new HighShelfFilter(2600.0 / SampleRate, 0.707, 3.5),
                new DcBlocker(),
                new GainFilter(1.15f)),
            VoiceEffect.FemaleSoft => Chain(
                new HighPassFilter(110.0 / SampleRate, 0.707),
                PitchShift(1.14, 0.95f),
                new HighShelfFilter(2200.0 / SampleRate, 0.707, 2.5),
                new LowPassFilter(7200.0 / SampleRate, 0.707),
                new DcBlocker(),
                new GainFilter(1.2f)),
            VoiceEffect.FemaleBright => Chain(
                new HighPassFilter(140.0 / SampleRate, 0.707),
                PitchShift(1.3, 0.85f),
                Mix(new VibratoEffect(SampleRate, 5f, 0.0015f), 1f, 0.35f),
                new HighShelfFilter(3400.0 / SampleRate, 0.707, 4.5),
                new DcBlocker(),
                new GainFilter(1.25f)),
            VoiceEffect.FemaleLow => Chain(
                new HighPassFilter(95.0 / SampleRate, 0.707),
                PitchShift(1.09, 1f),
                new LowShelfFilter(320.0 / SampleRate, 0.707, 1.5),
                new HighShelfFilter(2500.0 / SampleRate, 0.707, 2),
                new DcBlocker(),
                new GainFilter(1.1f)),
            _ => null,
        };
    }

    private static IOnlineFilter CreateMegaphone()
    {
        return Chain(
            new HighPassFilter(500.0 / SampleRate, 0.9),
            new LowPassFilter(3600.0 / SampleRate, 0.9),
            Mix(new DistortionEffect(DistortionMode.HardClipping, 12f, -8f), 1f),
            new LowPassFilter(4200.0 / SampleRate, 0.707));
    }

    private static IOnlineFilter? CreateMuffle(VoiceMuffle muffle)
    {
        return muffle switch
        {
            VoiceMuffle.Light => Chain(
                new LowPassFilter(1800.0 / SampleRate, 0.707)),
            VoiceMuffle.Heavy => Chain(
                new LowPassFilter(700.0 / SampleRate, 0.707),
                new LowPassFilter(700.0 / SampleRate, 0.707)),
            _ => null,
        };
    }

    private static PitchShiftVocoderEffect PitchShift(double shift, float gain)
    {
        return Mix(new PitchShiftVocoderEffect(SampleRate, shift, FftSize, HopSize), gain);
    }

    private static T Mix<T>(T effect, float wet, float dry = 0f) where T : IMixable
    {
        effect.Wet = wet;
        effect.Dry = dry;
        return effect;
    }

    private static IOnlineFilter Chain(params IOnlineFilter[] filters)
    {
        return new FilterChain(filters);
    }

    private sealed class RingModulator(float frequency, float mix) : IOnlineFilter
    {
        private readonly SignalBuilder _oscillator = new SineBuilder()
            .SetParameter("frequency", frequency)
            .SampledAt(SampleRate);

        public float Process(float sample)
        {
            return sample * (1f - mix) + sample * _oscillator.NextSample() * mix;
        }

        public void Reset()
        {
            _oscillator.Reset();
        }
    }

    private sealed class NoiseMixer(float level) : IOnlineFilter
    {
        private readonly SignalBuilder _noise = new WhiteNoiseBuilder()
            .SetParameter("min", -1)
            .SetParameter("max", 1)
            .SampledAt(SampleRate);

        public float Process(float sample)
        {
            return sample + _noise.NextSample() * level;
        }

        public void Reset()
        {
            _noise.Reset();
        }
    }

    /// <summary>
    /// Simple output gain used to level the new presets after their filter chains.
    /// </summary>
    private sealed class GainFilter(float gain) : IOnlineFilter
    {
        public float Process(float sample)
        {
            return sample * gain;
        }

        public void Reset()
        {
        }
    }

    /// <summary>
    /// One-pole DC blocker: ring modulation and asymmetric drive can leave a constant offset
    /// that would otherwise eat headroom in the following presets.
    /// </summary>
    private sealed class DcBlocker : IOnlineFilter
    {
        private const float R = 0.995f;

        private float _lastInput;
        private float _lastOutput;

        public float Process(float sample)
        {
            var output = sample - _lastInput + R * _lastOutput;
            _lastInput = sample;
            _lastOutput = output;
            return output;
        }

        public void Reset()
        {
            _lastInput = 0f;
            _lastOutput = 0f;
        }
    }
}
