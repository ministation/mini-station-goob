using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface;
/// <summary>
/// CozetteVector maps C0 control characters to visible "empty glyph" boxes.
/// Filter them so the stack can fall back to Noto for whitespace / skip drawing.
/// </summary>
internal sealed class C0ControlFilteredFont : Font
{
    private readonly Font _inner;

    public C0ControlFilteredFont(Font inner)
    {
        _inner = inner;
    }

    private static bool IsFiltered(Rune rune) => rune.Value is < 0x20 or 0xFFFD;

    public override int GetAscent(float scale) => _inner.GetAscent(scale);

    public override int GetHeight(float scale) => _inner.GetHeight(scale);

    public override int GetDescent(float scale) => _inner.GetDescent(scale);

    public override int GetLineHeight(float scale) => _inner.GetLineHeight(scale);

    public override float DrawChar(
        DrawingHandleBase handle,
        Rune rune,
        Vector2 baseline,
        float scale,
        Color color,
        bool fallback)
    {
        if (IsFiltered(rune))
            return 0;

        return _inner.DrawChar(handle, rune, baseline, scale, color, fallback);
    }

    public override float DrawCharOutline(
        DrawingHandleBase handle,
        Rune rune,
        Vector2 baseline,
        float scale,
        TextOutline outline,
        bool fallback)
    {
        if (IsFiltered(rune))
            return 0;

        return _inner.DrawCharOutline(handle, rune, baseline, scale, outline, fallback);
    }

    public override CharMetrics? GetCharMetrics(Rune rune, float scale, bool fallback)
    {
        if (IsFiltered(rune))
            return null;

        return _inner.GetCharMetrics(rune, scale, fallback);
    }
}