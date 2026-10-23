// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
/// </remarks>
public sealed class RsiCentreProfiles
{
    /// <summary>Порог альфы в байтах: 0.05 из прежней проверки по нормализованному цвету.</summary>
    private const byte AlphaThreshold = 12;

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
                var pngPath = rsi.Path / (stateId.Name + ".png");
                if (_resources.TryContentFileRead(pngPath, out var stream))
                {
                    using (stream)
                    {
                        using var sheet = Image.Load<Rgba32>(stream);
                        var frameSize = rsi.Size;
                        var columns = sheet.Width / frameSize.X;
                        var index = (int) dir;
                        var column = index % columns;
                        var row = index / columns;
                        var left = column * frameSize.X;
                        var top = row * frameSize.Y;
                        if (left >= 0 && top >= 0
                            && left + frameSize.X <= sheet.Width
                            && top + frameSize.Y <= sheet.Height)
                        {
                            frame = new Image<Rgba32>(frameSize.X, frameSize.Y);
                            var source = sheet.GetPixelSpan();
                            var target = frame.GetPixelSpan();
                            for (var y = 0; y < frameSize.Y; y++)
                            {
                                source.Slice((top + y) * sheet.Width + left, frameSize.X)
                                    .CopyTo(target.Slice(y * frameSize.X, frameSize.X));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.WarningS("footwalk", $"cannot read {rsi.Path} state {stateId}: {e}");
        }

        _frames[key] = frame;
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
