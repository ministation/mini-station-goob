// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Mini.FootWalk;

/// <summary>
/// Анализ арта по пикселям для анимации ходьбы: проходит ли рисунок через центральную линию спрайта
/// (слитные юбки, сросшиеся штанины) — такие вещи нельзя разрезать на левую и правую половины.
/// </summary>
/// <remarks>
/// Раньше это спрашивали у GPU: <c>Texture.GetPixel</c> в движке вытягивает ВЕСЬ лист атласа на
/// каждый отдельный пиксель (<c>GL.GetTexImage</c> размером 4 * ширина * высота). Одна пара обуви
/// требовала до восьми таких вызовов, а новая одежда, которую клиент видел впервые, вешала кадр на
/// секунду с лишним: на живой сборке 29 чтений = 1360 мс, «MainLoop: Cannot keep up!». Столько же
/// стоило переключение модсьюта (outerClothing пересобирает свои половины).
///
/// Атлас загружаемого RSI лежит на CPU и живёт ровно до конца <see cref="IResourceCache.OnRsiLoaded"/>
/// (<c>RSIResource.Load</c> диспозит его сразу после события), поэтому профиль центральных колонок
/// снимается там же один раз на RSI. Дальше любой запрос — чтение памяти, а не обмен с видеокартой.
/// </remarks>
public sealed class RsiCentreProfiles
{
    /// <summary>Порог альфы в байтах: 0.05 из прежней проверки по нормализованному цвету.</summary>
    private const byte AlphaThreshold = 12;

    /// <summary>Профиль кадра: <c>[y]</c> — на строке <c>y</c> (0 = верх кадра) оба пикселя у центра непрозрачны.</summary>
    private readonly Dictionary<Texture, bool[]> _profiles = new();

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
    /// высотой <paramref name="footCut"/> (1 — весь кадр). Кадр без профиля считается не проходящим
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
        // низ кадра — это как раз место, где между ногами виден просвет.
        var count = height - from;
        var step = Math.Max(1, count / 4);
        for (var y = height - 1; y >= from; y -= step)
        {
            if (rows[y])
                return true;
        }

        return false;
    }

    private void OnRsiLoaded(RsiLoadedEventArgs args)
    {
        // Атлас — всегда Image<Rgba32>: и папочный RSI, и .rsic грузятся в этот формат.
        if (args.Atlas is not Image<Rgba32> sheet)
            return;

        Span<Rgba32> pixels;
        try
        {
            pixels = sheet.GetPixelSpan();
        }
        catch (ArgumentException)
        {
            // Не единый буфер — читать построчно смысла нет, остаёмся на запасном поведении.
            return;
        }

        var sheetWidth = sheet.Width;
        var rsi = args.Resource.RSI;

        foreach (var state in rsi)
        {
            var icons = state.Icons;
            for (var dir = 0; dir < icons.Length; dir++)
            {
                var frames = icons[dir];
                if (frames.Length == 0)
                    continue;

                var frame = frames[0];
                if (_profiles.ContainsKey(frame))
                    continue;

                _profiles[frame] = BuildProfile(pixels, sheetWidth, frame);
            }
        }
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
