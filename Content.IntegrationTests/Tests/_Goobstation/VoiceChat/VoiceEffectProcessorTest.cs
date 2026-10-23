using Content.Goobstation.Server.VoiceChat;
using Content.Goobstation.Shared.VoiceChat;

namespace Content.IntegrationTests.Tests._Goobstation.VoiceChat;

/// <summary>
/// Mini: guards the extra voice mods added for the voice chat web page. Every preset must run on
/// real DSP (no silent fallback), stay inside the codec range and not leave a noticeable DC offset.
/// </summary>
[TestFixture]
public sealed class VoiceEffectProcessorTest
{
    private static short[] MakeSpeechLikeSignal()
    {
        const int sampleRate = VoiceCodec.SampleRate;
        var samples = new short[sampleRate / 2];
        var random = new Random(1234);

        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double) sampleRate;
            var envelope = 0.35 + 0.25 * Math.Sin(2 * Math.PI * 3.1 * t);
            var value = 0.4 * Math.Sin(2 * Math.PI * 120 * t)
                        + 0.2 * Math.Sin(2 * Math.PI * 240 * t)
                        + 0.08 * (random.NextDouble() * 2 - 1);
            samples[i] = (short) Math.Clamp(value * envelope * 28000, short.MinValue, short.MaxValue);
        }

        return samples;
    }

    private static short[] Process(VoiceEffect effect, short[] input, VoiceMuffle muffle = VoiceMuffle.None, bool megaphone = false)
    {
        var processor = new VoiceEffectProcessor();
        var buffer = (short[]) input.Clone();
        var settings = new VoiceEffectSettings(effect, muffle, megaphone);

        for (var offset = 0; offset < buffer.Length; offset += VoiceCodec.FrameSamples)
        {
            var count = Math.Min(VoiceCodec.FrameSamples, buffer.Length - offset);
            processor.Process(buffer, offset, count, settings);
        }

        return buffer;
    }

    [Test]
    public void NoneLeavesSignalUntouched()
    {
        var input = MakeSpeechLikeSignal();
        var output = Process(VoiceEffect.None, input);

        Assert.That(output, Is.EqualTo(input));
    }

    [Test]
    public void EveryEffectActuallyProcessesTheSignal()
    {
        var input = MakeSpeechLikeSignal();

        foreach (var effect in Enum.GetValues<VoiceEffect>())
        {
            if (effect == VoiceEffect.None)
                continue;

            var output = Process(effect, input);
            Assert.That(output, Is.Not.EqualTo(input), $"{effect} did not change the signal, is it wired into CreateVoice?");
        }
    }

    [Test]
    public void EveryEffectStaysInsideCodecRange()
    {
        var input = MakeSpeechLikeSignal();

        foreach (var effect in Enum.GetValues<VoiceEffect>())
        {
            var output = Process(effect, input);
            long sum = 0;
            foreach (var sample in output)
            {
                Assert.That(sample, Is.InRange(short.MinValue, short.MaxValue), $"{effect} produced an out of range sample");
                sum += sample;
            }

            var mean = sum / (double) output.Length;
            Assert.That(Math.Abs(mean), Is.LessThan(2000), $"{effect} left a DC offset of {mean:F0}");
        }
    }

    [Test]
    public void EveryEffectStaysAudible()
    {
        var input = MakeSpeechLikeSignal();
        var inputRms = Rms(input);

        foreach (var effect in Enum.GetValues<VoiceEffect>())
        {
            var output = Process(effect, input);
            var ratio = Rms(output) / inputRms;
            Assert.That(ratio, Is.GreaterThan(0.05), $"{effect} is nearly silent (RMS ratio {ratio:F3})");
            Assert.That(ratio, Is.LessThan(6), $"{effect} is too loud (RMS ratio {ratio:F3})");
        }
    }

    private static double Rms(short[] samples)
    {
        double sum = 0;
        foreach (var sample in samples)
        {
            var value = sample / 32768.0;
            sum += value * value;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    [Test]
    public void FemalePresetsRaisePitch()
    {
        var input = MakeSpeechLikeSignal();
        var inputPitch = EstimatePitch(input);
        var presets = new[]
        {
            VoiceEffect.Female,
            VoiceEffect.FemaleSoft,
            VoiceEffect.FemaleBright,
            VoiceEffect.FemaleLow,
        };

        foreach (var effect in presets)
        {
            var output = Process(effect, input);
            var ratio = EstimatePitch(output) / inputPitch;
            Assert.That(ratio, Is.GreaterThan(1.04), $"{effect} did not raise the pitch (ratio {ratio:F2})");
            Assert.That(ratio, Is.LessThan(1.6), $"{effect} shifted the pitch too far (ratio {ratio:F2})");
        }
    }

    private static double EstimatePitch(short[] samples)
    {
        const int minLag = VoiceCodec.SampleRate / 400;
        const int maxLag = VoiceCodec.SampleRate / 70;
        const int window = 2048;
        var start = Math.Max(0, samples.Length / 2 - window / 2);

        var bestLag = minLag;
        var best = double.NegativeInfinity;
        for (var lag = minLag; lag <= maxLag; lag++)
        {
            double correlation = 0;
            double energyA = 0;
            double energyB = 0;
            for (var i = 0; i < window; i++)
            {
                var a = samples[start + i] / 32768.0;
                var b = samples[start + i + lag] / 32768.0;
                correlation += a * b;
                energyA += a * a;
                energyB += b * b;
            }

            if (energyA <= 0 || energyB <= 0)
                continue;

            var normalized = correlation / Math.Sqrt(energyA * energyB);
            if (normalized > best)
            {
                best = normalized;
                bestLag = lag;
            }
        }

        return VoiceCodec.SampleRate / (double) bestLag;
    }

    [Test]
    public void MuffleAndMegaphoneCanBeCombinedWithAnyEffect()
    {
        var input = MakeSpeechLikeSignal();

        foreach (var effect in Enum.GetValues<VoiceEffect>())
        {
            Assert.DoesNotThrow(() => Process(effect, input, VoiceMuffle.Heavy, true), $"{effect} failed with muffle and megaphone");
        }
    }
}
