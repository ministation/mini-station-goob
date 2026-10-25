// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YamlDotNet.RepresentationModel;

namespace Content.Client._Mini.FootWalk;

/// <summary>
/// Нарезает одежду для покачивания при ходьбе: половины обуви, половинки штанин, боковые полосы,
/// торс с вырезанной нижней полосой и рукава, обрезанные по рисунку руки. Это замена прежних
/// клип-шейдеров: они на v293 могли рисоваться как прямоугольник из соседних спрайтов атласа, а
/// обычные слои с готовыми текстурами не умеют.
/// </summary>
/// <remarks>
/// Клиент предзагружает ВСЕ RSI в шаге «Texture preload», который выполняется раньше инициализации
/// систем контента. Поэтому подписка на <c>IResourceCache.OnRsiLoaded</c> из системы ходьбы не
/// получает ни одного события (атлас к этому моменту уже загружен и выгружен), и все вырезки молча
/// откатывались к цельной одежде. Вместо события нужный кадр читается с диска при первом показе
/// вещи: файл состояния RSI нарезан на кадры ровно так же, как его грузит движок, и кешируется.
/// В собранном лаунчер-клиенте PNG-файлов состояний нет: упаковщик складывает каждый RSI в
/// одиночный .rsic-атлас. Поэтому после неудачи с PNG кадр вырезается из .rsic по тем же правилам,
/// по которым его собирал упаковщик, иначе на живом клиенте вся одежда откатывалась к цельной.
/// </remarks>
public sealed class RsiCentreProfiles
{
    /// <summary>Порог альфы в байтах: 0.05 из прежней проверки по нормализованному цвету.</summary>
    private const byte AlphaThreshold = 12;

    /// <summary>Имя текстового поля PNG, в которое упаковщик кладёт исходный meta.json.</summary>
    internal const string RsicMetaKeyword = "robusttoolbox_rsic_meta";

    private IResourceManager? _resources;

    /// <summary>Кадры состояний, уже прочитанные с диска (null - прочитать не удалось).</summary>
    private readonly Dictionary<(string Rsi, string State, int Dir), Image<Rgba32>?> _frames = new();

    /// <summary>Лениво нарезанные текстуры, в том числе неудачные: повтор не строится.</summary>
    private readonly Dictionary<(string Rsi, string State, int Dir, WalkCutMode Mode, float Cut), Texture?> _cuts = new();

    /// <summary>Рукава: копия одежды, обрезанная по рисунку руки, в том числе неудачные.</summary>
    private readonly Dictionary<(string Garment, string GarmentState, string Limb, string LimbState, int Dir), Texture?> _sleeves = new();

    public void Initialize(IResourceManager resources)
    {
        _resources = resources;
    }

    /// <summary>
    /// Готовая вырезка кадра для покачивания. Null, если кадр прочитать не удалось (анимированное
    /// состояние, отсутствующий файл): тогда вещь качается целиком, без разрезов.
    /// </summary>
    public Texture? GetCut(RSI rsi, RSI.StateId state, RsiDirection facing, WalkCutMode mode, float cut)
    {
        var key = (rsi.Path.ToString(), state.Name ?? string.Empty, (int) facing, mode, cut);
        if (_cuts.TryGetValue(key, out var cached))
            return cached;

        var frame = GetFrame(rsi, state, facing);
        var texture = frame == null ? null : BuildCut(frame, mode, cut);
        _cuts[key] = texture;
        return texture;
    }

    /// <summary>
    /// Копия одежды, обрезанная по рисунку руки: её можно двигать вместе с махом руки, и открытой кожи
    /// у плеча не остаётся, потому что на месте остаётся исходная одежда, а копия рисуется поверх.
    /// </summary>
    public Texture? GetSleeveCut(
        RSI garmentRsi,
        RSI.StateId garmentState,
        RSI limbRsi,
        RSI.StateId limbState,
        RsiDirection facing)
    {
        var key = (
            garmentRsi.Path.ToString(),
            garmentState.Name ?? string.Empty,
            limbRsi.Path.ToString(),
            limbState.Name ?? string.Empty,
            (int) facing);

        if (_sleeves.TryGetValue(key, out var cached))
            return cached;

        var garment = GetFrame(garmentRsi, garmentState, facing);
        var limb = GetFrame(limbRsi, limbState, facing);
        if (garment == null || limb == null
            || garment.Width != limb.Width || garment.Height != limb.Height)
        {
            _sleeves[key] = null;
            return null;
        }

        var sleeve = garment.Clone();
        var sleevePixels = sleeve.GetPixelSpan();
        var limbPixels = limb.GetPixelSpan();
        for (var i = 0; i < sleevePixels.Length; i++)
        {
            if (limbPixels[i].A > AlphaThreshold)
                continue;

            sleevePixels[i].A = 0;
        }

        var texture = Texture.LoadFromImage(sleeve, "walk-sleeve");
        sleeve.Dispose();
        _sleeves[key] = texture;
        return texture;
    }

    private Image<Rgba32>? GetFrame(RSI rsi, RSI.StateId stateId, RsiDirection dir)
    {
        var key = (rsi.Path.ToString(), stateId.Name ?? string.Empty, (int) dir);
        if (_frames.TryGetValue(key, out var cached))
            return cached;

        Image<Rgba32>? frame = null;
        try
        {
            if (_resources != null
                && rsi.TryGetState(stateId, out var state)
                && !state.IsAnimated)
            {
                // Состояние с одним направлением всегда показывает нулевой кадр, как и движок.
                var direction = state.RsiDirections == RsiDirectionType.Dir1 ? 0 : (int) dir;

                // В dev-клиенте ресурсы лежат россыпью, в собранном клиенте весь RSI упакован в .rsic.
                frame = TryReadLooseFrame(rsi, stateId, direction)
                    ?? TryReadPackedFrame(rsi, stateId, direction);
            }
        }
        catch (Exception e)
        {
            // Штатный откат: вещь качается целиком, без разрезов.
            Logger.DebugS("footwalk", $"cannot read {rsi.Path} state {stateId}: {e.Message}");
        }

        _frames[key] = frame;
        return frame;
    }

    private Image<Rgba32>? TryReadLooseFrame(RSI rsi, RSI.StateId stateId, int direction)
    {
        var pngPath = rsi.Path / (stateId.Name + ".png");
        if (!_resources!.TryContentFileRead(pngPath, out var stream))
            return null;

        using (stream)
        {
            using var sheet = Image.Load<Rgba32>(stream);
            var frameSize = rsi.Size;
            var columns = sheet.Width / frameSize.X;
            if (columns <= 0)
                return null;

            return CropFrame(sheet, frameSize, direction % columns, direction / columns);
        }
    }

    private Image<Rgba32>? TryReadPackedFrame(RSI rsi, RSI.StateId stateId, int direction)
    {
        var rsicPath = rsi.Path.WithExtension("rsic");
        if (!_resources!.TryContentFileRead(rsicPath, out var stream))
            return null;

        using (stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var png = buffer.ToArray();

            var metaJson = ReadRsicMetadata(png);
            if (metaJson == null)
                return null;

            using var imageStream = new MemoryStream(png, writable: false);
            using var sheet = Image.Load<Rgba32>(imageStream);
            return ExtractPackedFrame(sheet, metaJson, stateId.Name ?? string.Empty, direction);
        }
    }

    /// <summary>
    /// Достаёт исходный meta.json из PNG-метаданных .rsic: упаковщик кладёт его в текстовый чанк
    /// <see cref="RsicMetaKeyword"/>, который ImageSharp пишет как tEXt, zTXt или iTXt.
    /// Разбор чанков ручной: типы метаданных ImageSharp и System.Text.Json недоступны в песочнице клиента.
    /// </summary>
    internal static string? ReadRsicMetadata(byte[] png)
    {
        if (png.Length < 8
            || png[0] != 0x89
            || png[1] != 0x50
            || png[2] != 0x4E
            || png[3] != 0x47)
        {
            return null;
        }

        var offset = 8;
        while (offset + 8 <= png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            if (length < 0 || offset + 12 + length > png.Length)
                return null;

            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var dataStart = offset + 8;

            switch (type)
            {
                case "tEXt":
                    if (TryReadTextChunk(png, dataStart, length, out var text))
                        return text;
                    break;
                case "zTXt":
                    if (TryReadCompressedTextChunk(png, dataStart, length, out var compressed))
                        return compressed;
                    break;
                case "iTXt":
                    if (TryReadInternationalTextChunk(png, dataStart, length, out var international))
                        return international;
                    break;
            }

            if (type == "IEND")
                break;

            offset = dataStart + length + 4;
        }

        return null;
    }

    private static bool TryReadTextChunk(byte[] png, int start, int length, out string? value)
    {
        value = null;
        var keywordEnd = FindZero(png, start, start + length);
        if (keywordEnd < 0 || !IsRsicKeyword(png, start, keywordEnd - start))
            return false;

        value = Encoding.ASCII.GetString(png, keywordEnd + 1, start + length - keywordEnd - 1);
        return true;
    }

    private static bool TryReadCompressedTextChunk(byte[] png, int start, int length, out string? value)
    {
        value = null;
        var keywordEnd = FindZero(png, start, start + length);
        if (keywordEnd < 0 || !IsRsicKeyword(png, start, keywordEnd - start))
            return false;

        var methodOffset = keywordEnd + 1;
        if (methodOffset >= start + length || png[methodOffset] != 0)
            return false;

        value = DecompressZlib(png, methodOffset + 1, start + length - methodOffset - 1, Encoding.ASCII);
        return value != null;
    }

    private static bool TryReadInternationalTextChunk(byte[] png, int start, int length, out string? value)
    {
        value = null;
        var end = start + length;
        var keywordEnd = FindZero(png, start, end);
        if (keywordEnd < 0 || !IsRsicKeyword(png, start, keywordEnd - start))
            return false;

        var compressionFlagOffset = keywordEnd + 1;
        var compressionMethodOffset = compressionFlagOffset + 1;
        if (compressionMethodOffset >= end)
            return false;

        var languageEnd = FindZero(png, compressionMethodOffset + 1, end);
        if (languageEnd < 0)
            return false;

        var translatedEnd = FindZero(png, languageEnd + 1, end);
        if (translatedEnd < 0)
            return false;

        var textStart = translatedEnd + 1;
        var textLength = end - textStart;
        value = png[compressionFlagOffset] == 0
            ? Encoding.UTF8.GetString(png, textStart, textLength)
            : DecompressZlib(png, textStart, textLength, Encoding.UTF8);
        return value != null;
    }

    private static string? DecompressZlib(byte[] data, int start, int length, Encoding encoding)
    {
        // zlib: 2-байтовый заголовок, raw deflate, 4-байтовый adler32. DeflateStream в песочнице
        // разрешён, а ZLibStream нет, поэтому заголовок и контрольную сумму отрезаем сами.
        if (length < 6)
            return null;

        using var compressed = new MemoryStream(data, start + 2, length - 6, false);
        using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
        // Переменная именно TextReader: StreamReader.ReadToEnd в песочнице клиента не разрешён.
        using TextReader reader = new StreamReader(deflate, encoding);
        return reader.ReadToEnd();
    }

    private static int FindZero(byte[] data, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (data[i] == 0)
                return i;
        }

        return -1;
    }

    private static bool IsRsicKeyword(byte[] data, int start, int length)
    {
        if (length != RsicMetaKeyword.Length)
            return false;

        for (var i = 0; i < length; i++)
        {
            if (data[start + i] != (byte) RsicMetaKeyword[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Вырезает кадр состояния из атласа .rsic. Раскладка повторяет RsiLoading.GenerateAtlas движка:
    /// кадры всех состояний идут подряд (состояние за состоянием, направление за направлением),
    /// строками по <c>ceil(sqrt(всех кадров))</c> кадров. Исходный meta.json лежит в PNG-метаданных.
    /// </summary>
    internal static Image<Rgba32>? ExtractPackedFrame(Image<Rgba32> sheet, string metaJson, string stateName, int direction)
    {
        var root = ParseMetaRoot(metaJson);
        if (root == null
            || GetChild(root, "size") is not YamlMappingNode sizeNode
            || GetChild(root, "states") is not YamlSequenceNode states)
        {
            return null;
        }

        var frameSize = new Vector2i(GetInt(sizeNode, "x") ?? 0, GetInt(sizeNode, "y") ?? 0);
        if (frameSize.X <= 0 || frameSize.Y <= 0)
            return null;

        var totalFrames = 0;
        var stateOffset = -1;
        int[]? targetFramesPerDir = null;

        foreach (var stateNode in states.Children)
        {
            if (stateNode is not YamlMappingNode state)
                return null;

            var name = GetScalar(state, "name");
            var dirCount = GetInt(state, "directions") ?? 1;
            if (dirCount <= 0)
                return null;

            var delays = GetChild(state, "delays") as YamlSequenceNode;
            var framesPerDir = new int[dirCount];
            for (var dir = 0; dir < dirCount; dir++)
            {
                var frames = delays != null
                    && dir < delays.Children.Count
                    && delays.Children[dir] is YamlSequenceNode delayList
                        ? Math.Max(1, delayList.Children.Count)
                        : 1;
                framesPerDir[dir] = frames;
            }

            if (stateOffset < 0 && name == stateName)
            {
                stateOffset = totalFrames;
                targetFramesPerDir = framesPerDir;
            }

            totalFrames += framesPerDir.Sum();
        }

        if (stateOffset < 0 || targetFramesPerDir == null || totalFrames <= 0)
            return null;

        if (direction < 0 || direction >= targetFramesPerDir.Length)
            direction = 0;

        var frameIndex = stateOffset;
        for (var dir = 0; dir < direction; dir++)
            frameIndex += targetFramesPerDir[dir];

        var columns = sheet.Width / frameSize.X;
        if (columns <= 0 || sheet.Width % frameSize.X != 0 || sheet.Height % frameSize.Y != 0)
            return null;

        var rows = sheet.Height / frameSize.Y;
        if (frameIndex >= columns * rows)
            return null;

        return CropFrame(sheet, frameSize, frameIndex % columns, frameIndex / columns);
    }

    private static YamlMappingNode? ParseMetaRoot(string metaJson)
    {
        try
        {
            var yaml = new YamlStream();
            using var reader = new StringReader(metaJson);
            yaml.Load(reader);
            return yaml.Documents.Count > 0 ? yaml.Documents[0].RootNode as YamlMappingNode : null;
        }
        catch
        {
            return null;
        }
    }

    private static YamlNode? GetChild(YamlMappingNode mapping, string key)
    {
        // Именно через IEnumerable: свойство Children возвращает внутренний IOrderedDictionary,
        // который не входит в песочницу клиента.
        foreach (var entry in (IEnumerable<KeyValuePair<YamlNode, YamlNode>>) mapping)
        {
            if (entry.Key is YamlScalarNode scalar && scalar.Value == key)
                return entry.Value;
        }

        return null;
    }

    private static string? GetScalar(YamlMappingNode mapping, string key)
    {
        return GetChild(mapping, key) is YamlScalarNode scalar ? scalar.Value : null;
    }

    private static int? GetInt(YamlMappingNode mapping, string key)
    {
        return int.TryParse(GetScalar(mapping, key), out var value) ? value : null;
    }

    private static Image<Rgba32>? CropFrame(Image<Rgba32> sheet, Vector2i frameSize, int column, int row)
    {
        var left = column * frameSize.X;
        var top = row * frameSize.Y;
        if (left < 0 || top < 0
            || left + frameSize.X > sheet.Width
            || top + frameSize.Y > sheet.Height)
        {
            return null;
        }

        var frame = new Image<Rgba32>(frameSize.X, frameSize.Y);
        var source = sheet.GetPixelSpan();
        var target = frame.GetPixelSpan();
        for (var y = 0; y < frameSize.Y; y++)
        {
            source.Slice((top + y) * sheet.Width + left, frameSize.X)
                .CopyTo(target.Slice(y * frameSize.X, frameSize.X));
        }

        return frame;
    }

    private static Texture? BuildCut(Image<Rgba32> frame, WalkCutMode mode, float cut)
    {
        var width = frame.Width;
        var height = frame.Height;
        if (width < 4 || height < 4)
            return null;

        var bandHeight = Math.Min(height - 1, Math.Max(1, (int) MathF.Round(height * cut)));
        var halfWidth = width / 2;

        int xFrom, xTo, yFrom, yTo;
        switch (mode)
        {
            case WalkCutMode.HalfL:
                xFrom = 0;
                xTo = halfWidth;
                yFrom = 0;
                yTo = height;
                break;
            case WalkCutMode.HalfR:
                xFrom = halfWidth;
                xTo = width;
                yFrom = 0;
                yTo = height;
                break;
            case WalkCutMode.Band:
                xFrom = 0;
                xTo = width;
                yFrom = height - bandHeight;
                yTo = height;
                break;
            case WalkCutMode.BandHalfL:
                xFrom = 0;
                xTo = halfWidth;
                yFrom = height - bandHeight;
                yTo = height;
                break;
            case WalkCutMode.BandHalfR:
                xFrom = halfWidth;
                xTo = width;
                yFrom = height - bandHeight;
                yTo = height;
                break;
            case WalkCutMode.Top:
                xFrom = 0;
                xTo = width;
                yFrom = 0;
                yTo = height - bandHeight;
                break;
            default:
                return null;
        }

        if (xTo <= xFrom || yTo <= yFrom)
            return null;

        // ProcessPixelRows/PixelAccessor are not on the client sandbox allowlist, so the cut is
        // done through a plain pixel span.
        var cutImage = frame.Clone();
        var span = cutImage.GetPixelSpan();
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            if (y < yFrom || y >= yTo)
            {
                for (var x = 0; x < width; x++)
                    span[rowOffset + x].A = 0;
                continue;
            }

            for (var x = 0; x < xFrom; x++)
                span[rowOffset + x].A = 0;
            for (var x = xTo; x < width; x++)
                span[rowOffset + x].A = 0;
        }

        var texture = Texture.LoadFromImage(cutImage, "walk-cut");
        cutImage.Dispose();
        return texture;
    }
}

/// <summary>Какая часть кадра остаётся в вырезке для покачивания.</summary>
public enum WalkCutMode : byte
{
    /// <summary>Левая половина кадра по всей высоте: обувь в анфас.</summary>
    HalfL,

    /// <summary>Правая половина кадра по всей высоте: обувь в анфас.</summary>
    HalfR,

    /// <summary>Нижняя полоса целиком: ботинки скафандра и штаны в профиль.</summary>
    Band,

    /// <summary>Левая половина нижней полосы: штанина и полуботинок скафандра в анфас.</summary>
    BandHalfL,

    /// <summary>Правая половина нижней полосы: штанина и полуботинок скафандра в анфас.</summary>
    BandHalfR,

    /// <summary>Кадр без нижней полосы: торс юниформы и корпус скафандра.</summary>
    Top,
}
