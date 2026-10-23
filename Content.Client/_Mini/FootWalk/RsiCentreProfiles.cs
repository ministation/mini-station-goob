// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Mini.FootWalk;

/// <summary>
/// Каким образом вырезается одежда для покачивания при ходьбе: кадры загруженных RSI лежат на CPU
/// ровно до конца <see cref="IResourceCache.OnRsiLoaded"/> (<c>RSIResource.Load</c> диспозит атлас
/// сразу после события), поэтому всё, что нужно от пикселей, делается там же один раз на RSI.
/// </summary>
/// <remarks>
/// Раньше это спрашивали у GPU: <c>Texture.GetPixel</c> в движке вытягивает ВЕСЬ лист атласа на
/// каждый отдельный пиксель (<c>GL.GetTexImage</c> размером 4 * ширина * высота). Одна пара обуви
/// требовала до восьми таких вызовов, а новая одежда, которую клиент видел впервые, вешала кадр на
/// секунду с лишним: на живой сборке 29 чтений = 1360 мс, «MainLoop: Cannot keep up!».
///
/// Из атласа снимаются две вещи:
/// 1. профиль центральных колонок каждого кадра: проходит ли рисунок через центральную линию
///    (слитные юбки, сросшиеся штанины) - такие вещи нельзя резать на левую и правую половины;
/// 2. для состояний одежды (<c>equipped-...</c>) копии кадров, из которых лениво нарезаются
///    текстуры половин и полос. Это замена прежних клип-шейдеров: они на v293 могли рисоваться
///    как прямоугольник из соседних спрайтов атласа, а обычные слои с готовыми текстурами не умеют.
/// </remarks>
public sealed class RsiCentreProfiles
{
    /// <summary>Порог альфы в байтах: 0.05 из прежней проверки по нормализованному цвету.</summary>
    private const byte AlphaThreshold = 12;

    /// <summary>Профиль кадра: <c>[y]</c> - на строке <c>y</c> (0 = верх кадра) оба пикселя у центра непрозрачны.</summary>
    private readonly Dictionary<Texture, bool[]> _profiles = new();

    /// <summary>Кадры состояний одежды, вырезанные из атласа на время жизни RSI.</summary>
    private readonly Dictionary<Texture, Image<Rgba32>> _frames = new();

    /// <summary>Лениво нарезанные текстуры, в том числе неудачные: повтор не строится.</summary>
    private readonly Dictionary<(Texture Frame, WalkCutMode Mode, float Cut), Texture?> _cuts = new();

    public void Subscribe(IResourceCache cache)
    {
        cache.OnRsiLoaded += OnRsiLoaded;
    }

    public void Unsubscribe(IResourceCache cache)
    {
        cache.OnRsiLoaded -= OnRsiLoaded;
    }

    /// <summary>
    /// Есть ли у кадра непрозрачные пиксели с обеих сторон от центральной линии внутри нижней полосы
    /// высотой <paramref name="footCut"/> (1 - весь кадр). Кадр без профиля считается не проходящим
    /// через центр: тогда работают обычные левая/правая половины.
    /// </summary>
    public bool CrossesCentre(Texture frame, float footCut)
    {
        if (!_profiles.TryGetValue(frame, out var rows))
            return false;

        var height = rows.Length;
        if (frame.Width < 4 || height < 2)
            return false;

        var from = Math.Max(0, (int) (height * (1f - footCut)));

        // Прежняя проверка шла снизу вверх и останавливалась на первой же строке, где центр сомкнут:
        // низ кадра - это как раз место, где между ногами виден просвет.
        var count = height - from;
        var step = Math.Max(1, count / 4);
        for (var y = height - 1; y >= from; y -= step)
        {
            if (rows[y])
                return true;
        }

        return false;
    }

    /// <summary>
    /// Готовая вырезка кадра для покачивания: половины обуви, полосы штанин и бот, верх с вырезанной
    /// полосой. Null, если кадр не из состояния одежды или текстуру построить не удалось: тогда
    /// вещь качается целиком, без разрезов.
    /// </summary>
    public Texture? GetCut(Texture frame, WalkCutMode mode, float cut)
    {
        if (!_frames.TryGetValue(frame, out var image))
            return null;

        var key = (frame, mode, cut);
        if (_cuts.TryGetValue(key, out var cached))
            return cached;

        var texture = BuildCut(image, mode, cut);
        _cuts[key] = texture;
        return texture;
    }

    private void OnRsiLoaded(RsiLoadedEventArgs args)
    {
        // Атлас - всегда Image<Rgba32>: и папочный RSI, и .rsic грузятся в этот формат.
        if (args.Atlas is not Image<Rgba32> sheet)
            return;

        Span<Rgba32> pixels;
        try
        {
            pixels = sheet.GetPixelSpan();
        }
        catch (ArgumentException)
        {
            // Не единый буфер - читать построчно смысла нет, остаёмся на запасном поведении.
            return;
        }

        var sheetWidth = sheet.Width;
        var rsi = args.Resource.RSI;

        foreach (var state in rsi)
        {
            var cutCandidate = state.StateId.Name?.StartsWith("equipped-", StringComparison.Ordinal) == true;

            var icons = state.Icons;
            for (var dir = 0; dir < icons.Length; dir++)
            {
                var frames = icons[dir];
                if (frames.Length == 0)
                    continue;

                var frame = frames[0];
                if (!_profiles.ContainsKey(frame))
                    _profiles[frame] = BuildProfile(pixels, sheetWidth, frame);

                if (cutCandidate && !_frames.ContainsKey(frame))
                    _frames[frame] = CropFrame(sheet, pixels, sheetWidth, frame);
            }
        }
    }

    private static Image<Rgba32> CropFrame(Image<Rgba32> sheet, ReadOnlySpan<Rgba32> pixels, int sheetWidth, Texture frame)
    {
        var region = frame is AtlasTexture atlas ? atlas.SubRegion : new UIBox2(0, 0, frame.Width, frame.Height);
        var x = Math.Max(0, (int) region.Left);
        var y = Math.Max(0, (int) region.Top);
        var width = Math.Min((int) region.Width, sheet.Width - x);
        var height = Math.Min((int) region.Height, sheet.Height - y);
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Frame region is outside the sheet");

        var crop = new Image<Rgba32>(width, height);
        var source = pixels;
        var target = crop.GetPixelSpan();
        for (var row = 0; row < height; row++)
        {
            source.Slice((y + row) * sheetWidth + x, width).CopyTo(target.Slice(row * width, width));
        }

        return crop;
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
        // done through a plain pixel span, like the centre profiles above.
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

    private static bool[] BuildProfile(ReadOnlySpan<Rgba32> pixels, int sheetWidth, Texture frame)
    {
        var region = frame is AtlasTexture atlas ? atlas.SubRegion : new UIBox2(0, 0, frame.Width, frame.Height);

        var width = (int) region.Width;
        var height = (int) region.Height;
        var rows = new bool[height];
        if (width < 4 || height < 2)
            return rows;

        var left = (int) region.Left + width / 2 - 1;
        var right = (int) region.Left + width / 2;
        var top = (int) region.Top;

        for (var y = 0; y < height; y++)
        {
            var offset = (top + y) * sheetWidth + left;
            if (offset < 0 || offset + 1 >= pixels.Length)
                break;

            rows[y] = pixels[offset].A > AlphaThreshold && pixels[offset + 1].A > AlphaThreshold;
        }

        return rows;
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
