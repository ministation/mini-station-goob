// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Content.Client._Mini.FootWalk;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client._Mini.FootWalk;

/// <summary>
/// Регрессия: в собранном лаунчер-клиенте RSI упакованы в .rsic, отдельных PNG состояний на диске нет.
/// Если кадр не вырезается из атласа упаковщика, на живом клиенте ноги снова едут вместе с одеждой,
/// а рукава не машут, хотя в dev-клиенте всё работает.
/// </summary>
[TestFixture]
public sealed class RsicFrameExtractionTest
{
    private const int FrameSize = 2;

    /// <summary>Настоящий .rsic из собранного клиента: ботинки, состояние equipped-FEET с 4 направлениями.</summary>
    private const string RealRsicBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAYAAADDPmHLAAAACXBIWXMAAA7EAAAOxAGVKw4bAAACp3RFWHRyb2J1c3R0b29sYm94X3JzaWNfbWV0YQB7CiAgICAidmVyc2lvbiI6IDEsCiAgICAibGljZW5zZSI6ICJDQy1CWS1TQS0zLjAiLAogICAgImNvcHlyaWdodCI6ICJUYWtlbiBmcm9tIC92Zy9zdGF0aW9uIGF0IGh0dHBzOi8vZ2l0aHViLmNvbS92Z3N0YXRpb24tY29kZXJzL3Znc3RhdGlvbjEzL2NvbW1pdC8yZmVjMzJmNTljNjkyZTc4ZWZjYWRiMTlkMWFhZTg5YTNiMzg5YjAyLCBtb2RpZmllZCBieSBLLUR5bmFtaWMgKGdpdGh1YikuIGluaGFuZC1sZWZ0LCBpbmhhbmQtcmlnaHQgYnkgSy1EeW5hbWljIChnaXRodWIpLiIsCiAgICAic2l6ZSI6IHsKICAgICAgICAieCI6IDMyLAogICAgICAgICJ5IjogMzIKICAgIH0sCiAgICAic3RhdGVzIjogWwogICAgICAgIHsKICAgICAgICAgICAgIm5hbWUiOiAiaWNvbiIKICAgICAgICB9LAogICAgICAgIHsKICAgICAgICAgICAgIm5hbWUiOiAiZXF1aXBwZWQtRkVFVCIsCiAgICAgICAgICAgICJkaXJlY3Rpb25zIjogNAogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgICAibmFtZSI6ICJpbmhhbmQtbGVmdCIsCiAgICAgICAgICAgICJkaXJlY3Rpb25zIjogNAogICAgICAgIH0sCiAgICAgICAgewogICAgICAgICAgICAibmFtZSI6ICJpbmhhbmQtcmlnaHQiLAogICAgICAgICAgICAiZGlyZWN0aW9ucyI6IDQKICAgICAgICB9CiAgICBdCn0KWZh83gAABSdJREFUeJztnU1v3EQcxmdFxYa89oU0nEBcqp5ypd8AqQFRtd8BwQ31lk+QE1FuII6V+gGqJhWpKpVyKtd8AQQntktftqJNFhWZPlb+0dTxbrK2x+vx8/wusztOPGP/fzP2zubvnHGCGglAjgQgRwKQIwHIkQDkSAByJAA5Ewnw0crFxF4fHAzTcn5uzv3z6tWxn30xGHScaDwTzwBra2tp+eujR25//yAN/o0b14+2o77/91O3cnE56T3pS4KGU+gS8MvDh2nZ7b5/NPqtzlhcWEAhCRrOxAJkAz0z03UiXiq5CcxKIeKhlADD4b8j63B/0Olo9m86EwmAO/+52dkT60Q8TDwDPH32PC0vnD83tk7EwcQC2Od+f/rPqxNxUNtK4O69e8nnV6/qpqBhnFoArAIuzM+nN3fgzX9v0uu/fQxEiVkAs4Fhq4UI/vebm5KggZwoAAKPQPrBB6hb/vBCWgcZgEkArLTgX750yaEUzSJXAAs6sBLLuwaWfm3J1x/xABLgd1B//do1t7O97e4/eIBRnxyWokHkCpBd7we25o+63Z930zob8dnVQCwRg1u3bx99KaTgN5NcAXZ2dtyZ945vssADm/Z9bLYA+kQQByPvAbJf8frX9ywWeIz2s0tLiRPRkCvAX70nx6frwel2qL8DiIuo/iJoZXk56fX7HZR4n/caZVvb9/thr8u2G5UAAAc7O/uBe/16/6gO7/2T0ub2/TatP2X2E5UAV6585vb29nK34YSsrq66O3e3g/bBTnxevS9FXZQ97qgEePz4t7TMCwJOvm1nouxxRyMApjqbAbIjDe9tJLw9GUGuw3lTv98+sEtByPuAjY2NZGtz8502yxx3FALYybfpP+8aDLA9RBCsfb+tr7/51q2vr7vvbt50P/34wzt9CSnB2zY7fn9AmeOOQgBgAT8YDt1Mt3tsW1591e37bWwdfq+x5X2/EboP4/pTlCgEyFo9GLHtlEsVpdofjNkWsg+n7c+kRCGACIcEIEcCkCMByJEA5EgAciQAORKAHAlAjgQgRwKQIwHIkQDkSAByJAA5EoAcCUCOBCBHApAjAchplQB+nl7e+2ntq8m0RgA/R86SSMCdu9uFAoZAf/XlF9hn4mfe1JUAWhetEQBYssRhpkzp0Qp5/IwkMI38v5C0RgAEe2lxMTl/7uzIBNKi2P6ePX/hBi9ftmb0g8ICZHPlwbSnRgRnpttNRmXwlgEjv23BB6VmAD9HrSlTY1bKKsCx9fr91gUfFBZgXK7+tKkyWG0NvFFYgHG5+iIeCgkwKlcfuetIX3YiGiYWIC9XH0AEBT8+Cs0A/qivMydeVM/EAuTdFNWREy/C0JqFIFEMCUCOBCBHApAjAciRAORIAHIkADkSgBwJQI4EIEcCkCMByJEA5EgAciQAORKAHAlAjgQgRwKQU0lq2O9//Bnk//ShjCk/v+4+f/rJx0nZtLVSyaHZjoCqDja2/Pyqn09wUjt+RtZU/nm0FyAXKkCx5edX/XyCLL5kOA9VSFbqElBXgGLIzw/5fAKfqiWr5CawjgDFkJ8f8vkEIIRklX0KCBmgmPLzQzyfwKdqySoRIGSAehHm54fuc5WSlRag1/IHKDSVqs67FoLIkQDkSAByJAA5EoAcCUCOBCBHApAjAciRAORIAHIkADkSgBwJQI4EIEcCkCMByJEA5EgAciQAORKAHAlAjgQgRwKQIwHIkQDkSAByJAA5EoAcCUCOBCBHApAjAciRAORIAHL+B00v55kv4asBAAAAAElFTkSuQmCC";

    private const string RealRsicFrameHash = "987d855e4609d319451f5917f9a16637234dd6dfdc01ef7ea9ce4ce7e9d571a7";

    [Test]
    public void FindsStaticStateByOrder()
    {
        const string meta = """
            {
                "size": { "x": 2, "y": 2 },
                "states": [
                    { "name": "other", "directions": 4 },
                    { "name": "icon", "directions": 4 }
                ]
            }
            """;

        using var sheet = BuildAtlas(meta, 8);

        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "other", 3), 3);
        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "icon", 0), 4);
        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "icon", 2), 6);
    }

    [Test]
    public void AnimatedStateShiftsFollowingOffsets()
    {
        const string meta = """
            {
                "size": { "x": 2, "y": 2 },
                "states": [
                    { "name": "glove", "directions": 4, "delays": [[0.1, 0.1], [0.1], [0.1], [0.1]] },
                    { "name": "boot", "directions": 4 }
                ]
            }
            """;

        using var sheet = BuildAtlas(meta, 9);

        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "boot", 0), 5);
        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "boot", 2), 7);
    }

    [Test]
    public void SingleDirectionStateUsesFrameZero()
    {
        const string meta = """
            {
                "size": { "x": 2, "y": 2 },
                "states": [
                    { "name": "icon" },
                    { "name": "equipped" }
                ]
            }
            """;

        using var sheet = BuildAtlas(meta, 2);

        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "icon", 0), 0);
        AssertFrame(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "equipped", 3), 1);
    }

    [Test]
    public void UnknownStateOrBadMetadataReturnsNull()
    {
        const string meta = """
            {
                "size": { "x": 2, "y": 2 },
                "states": [ { "name": "icon" } ]
            }
            """;

        using var sheet = BuildAtlas(meta, 1);

        Assert.That(RsiCentreProfiles.ExtractPackedFrame(sheet, meta, "missing", 0), Is.Null);
        Assert.That(RsiCentreProfiles.ExtractPackedFrame(sheet, """{"states": []}""", "icon", 0), Is.Null);
        Assert.That(RsiCentreProfiles.ExtractPackedFrame(sheet, """{"size": {"x": 0, "y": 0}, "states": [{"name": "icon"}]}""", "icon", 0), Is.Null);
    }

    [Test]
    public void ReadsFrameFromRealPackagedRsic()
    {
        var png = Convert.FromBase64String(RealRsicBase64);
        var metaJson = RsiCentreProfiles.ReadRsicMetadata(png);
        Assert.That(metaJson, Is.Not.Null);
        Assert.That(metaJson, Does.Contain("\"states\""));

        using var stream = new MemoryStream(png);
        using var sheet = Image.Load<Rgba32>(stream);
        using var frame = RsiCentreProfiles.ExtractPackedFrame(sheet, metaJson!, "equipped-FEET", 2);
        Assert.That(frame, Is.Not.Null);
        Assert.That(frame!.Width, Is.EqualTo(32));
        Assert.That(frame.Height, Is.EqualTo(32));

        var bytes = new byte[frame.Width * frame.Height * 4];
        frame.CopyPixelDataTo(bytes);
        Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo(RealRsicFrameHash));
    }

    [Test]
    public void ReadsMetadataFromAllPngTextChunkFormats()
    {
        const string meta = """{"size": {"x": 2, "y": 2}, "states": [{"name": "icon"}]}""";

        Assert.That(RsiCentreProfiles.ReadRsicMetadata(BuildRsicPng("tEXt", meta)), Is.EqualTo(meta));
        Assert.That(RsiCentreProfiles.ReadRsicMetadata(BuildRsicPng("zTXt", meta)), Is.EqualTo(meta));
        Assert.That(RsiCentreProfiles.ReadRsicMetadata(BuildRsicPng("iTXt", meta)), Is.EqualTo(meta));
        Assert.That(RsiCentreProfiles.ReadRsicMetadata(new byte[] { 1, 2, 3 }), Is.Null);
    }

    private static void AssertFrame(Image<Rgba32> frame, int expectedFrameIndex)
    {
        Assert.That(frame, Is.Not.Null);
        using (frame)
        {
            Assert.That(frame!.Width, Is.EqualTo(FrameSize));
            Assert.That(frame.Height, Is.EqualTo(FrameSize));
            Assert.That(frame[0, 0].R, Is.EqualTo((byte) expectedFrameIndex));
        }
    }

    /// <summary>Собирает минимальный PNG с текстовым чанком нужного типа, как это делает ImageSharp.</summary>
    private static byte[] BuildRsicPng(string chunkType, string meta)
    {
        var metaBytes = System.Text.Encoding.UTF8.GetBytes(meta);
        byte[] chunkData;
        switch (chunkType)
        {
            case "tEXt":
            {
                var payload = new List<byte>();
                payload.AddRange(System.Text.Encoding.ASCII.GetBytes(RsiCentreProfiles.RsicMetaKeyword));
                payload.Add(0);
                payload.AddRange(System.Text.Encoding.ASCII.GetBytes(meta));
                chunkData = payload.ToArray();
                break;
            }
            case "zTXt":
            {
                var payload = new List<byte>();
                payload.AddRange(System.Text.Encoding.ASCII.GetBytes(RsiCentreProfiles.RsicMetaKeyword));
                payload.Add(0);
                payload.Add(0);
                payload.AddRange(ZlibCompress(metaBytes));
                chunkData = payload.ToArray();
                break;
            }
            case "iTXt":
            {
                var payload = new List<byte>();
                payload.AddRange(System.Text.Encoding.ASCII.GetBytes(RsiCentreProfiles.RsicMetaKeyword));
                payload.Add(0);
                payload.Add(0); // без сжатия
                payload.Add(0); // метод сжатия
                payload.Add(0); // пустой language tag
                payload.Add(0); // пустой translated keyword
                payload.AddRange(metaBytes);
                chunkData = payload.ToArray();
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(chunkType));
        }

        var png = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        png.AddRange(BigEndian(chunkData.Length));
        png.AddRange(System.Text.Encoding.ASCII.GetBytes(chunkType));
        png.AddRange(chunkData);
        png.AddRange(new byte[4]); // CRC наш разбор не проверяет
        png.AddRange(BigEndian(0));
        png.AddRange(System.Text.Encoding.ASCII.GetBytes("IEND"));
        png.AddRange(new byte[4]);
        return png.ToArray();
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (var deflate = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
            deflate.Write(data, 0, data.Length);

        output.Write(Adler32(data));
        return output.ToArray();
    }

    private static byte[] Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        var result = (b << 16) | a;
        return new[] { (byte) (result >> 24), (byte) (result >> 16), (byte) (result >> 8), (byte) result };
    }

    private static byte[] BigEndian(int value)
    {
        return new[] { (byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value };
    }

    /// <summary>Собирает атлас ровно по правилам упаковщика движка, чтобы проверять раскладку кадров.</summary>
    private static Image<Rgba32> BuildAtlas(string metaJson, int expectedFrames)
    {
        using var doc = JsonDocument.Parse(metaJson);
        var states = doc.RootElement.GetProperty("states");
        var frameCounts = new List<int>();
        foreach (var state in states.EnumerateArray())
        {
            var directions = state.TryGetProperty("directions", out var dirElement) ? dirElement.GetInt32() : 1;
            var hasDelays = state.TryGetProperty("delays", out var delays) && delays.ValueKind == JsonValueKind.Array;
            var frames = 0;
            for (var dir = 0; dir < directions; dir++)
            {
                frames += hasDelays && dir < delays.GetArrayLength() && delays[dir].ValueKind == JsonValueKind.Array
                    ? Math.Max(1, delays[dir].GetArrayLength())
                    : 1;
            }

            frameCounts.Add(frames);
        }

        var total = frameCounts.Sum();
        Assert.That(total, Is.EqualTo(expectedFrames));

        var columns = (int) MathF.Ceiling(MathF.Sqrt(total));
        var rows = (int) MathF.Ceiling((float) total / columns);
        var sheet = new Image<Rgba32>(columns * FrameSize, rows * FrameSize);

        var index = 0;
        foreach (var frames in frameCounts)
        {
            for (var frame = 0; frame < frames; frame++)
            {
                var color = new Rgba32((byte) index, 0, 0, 255);
                var left = index % columns * FrameSize;
                var top = index / columns * FrameSize;
                for (var y = 0; y < FrameSize; y++)
                {
                    for (var x = 0; x < FrameSize; x++)
                        sheet[left + x, top + y] = color;
                }

                index++;
            }
        }

        return sheet;
    }
}
