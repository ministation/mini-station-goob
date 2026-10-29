// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Goobstation.Shared.Fishing;
using Content.Goobstation.Shared.Fishing.Aquarium;
using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Fishing.Aquarium;

/// <summary>
/// Draws the water, the glass and the fish swimming inside it.
/// Positions come from the NetEntity hash plus local time, so every client sees the same tank
/// without any extra server traffic.
/// </summary>
public sealed partial class AquariumTankControl : Control
{
    /// <summary>
    /// Height of one fish relative to the tank height.
    /// </summary>
    private const float FishHeightFraction = 0.12f;

    /// <summary>
    /// Part of the tank height the fish may swim in, on top of the sand.
    /// </summary>
    private const float VerticalMargin = 0.16f;

    private const float DriftSpeed = 0.03f;
    private const float BobAmplitude = 0.02f;
    private const float SandHeight = 0.14f;

    private static readonly Color GlassColor = Color.FromHex("#9fb8c8");
    private static readonly Color SandColor = Color.FromHex("#6b5a3a");
    private static readonly Color DeadColor = Color.FromHex("#7a7a7a");

    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly SpriteSystem _sprite;
    private readonly List<FishVisual> _fish = new();

    private float _time;
    private bool _broken;
    private bool _hasAlive;
    private AquariumFluid _fluid = AquariumFluid.Freshwater;

    public AquariumTankControl()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _ent.System<SpriteSystem>();
        MouseFilter = MouseFilterMode.Ignore;
        MinHeight = 120f;
    }

    /// <summary>
    /// Rebuilds the fish that are drawn in the tank. Called whenever the window receives a new UI state.
    /// </summary>
    public void SetFish(AquariumFishData[] fish, bool glassBroken, AquariumFluid fluid)
    {
        _broken = glassBroken;
        _fluid = fluid;
        _hasAlive = false;
        _fish.Clear();

        foreach (var data in fish)
        {
            if (CreateVisual(data) is not { } visual)
                continue;

            _fish.Add(visual);
            _hasAlive |= visual.Alive;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // An empty or fully dead tank is static, no reason to animate it.
        if (!_hasAlive)
            return;

        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        if (box.Width <= 0 || box.Height <= 0)
            return;

        var left = (float) box.Left;
        var top = (float) box.Top;
        var right = (float) box.Right;
        var bottom = (float) box.Bottom;
        var width = right - left;
        var height = bottom - top;

        var water = new UIBox2(left, top, right, bottom);
        handle.DrawRect(water, GetWaterColor());

        var sandTop = bottom - height * SandHeight;
        handle.DrawRect(new UIBox2(left, sandTop, right, bottom), SandColor);

        var fishHeight = height * FishHeightFraction;
        var minX = left + 4f;
        var minY = top + height * VerticalMargin;
        var maxY = sandTop - fishHeight - 4f;

        foreach (var fish in _fish)
        {
            if (fish.Texture is not { } texture)
                continue;

            var fishWidth = fishHeight * fish.Aspect;
            var maxX = right - fishWidth - 4f;
            var spanX = MathF.Max(0f, maxX - minX);
            var spanY = MathF.Max(0f, maxY - minY);

            float x;
            float y;

            if (fish.Alive)
            {
                // Drift left, the direction the aquarium sprites face.
                var progress = PositiveModulo(fish.SeedX - _time * DriftSpeed * fish.Speed, 1f);
                x = minX + progress * spanX;
                y = minY + fish.SeedY * spanY
                    + MathF.Sin(_time * 1.3f * fish.Speed + fish.Phase) * height * BobAmplitude;
            }
            else
            {
                x = minX + fish.SeedX * spanX;
                y = sandTop - fishHeight + 2f;
            }

            var modulate = fish.Alive ? fish.Color : DeadColor;
            handle.DrawTextureRect(texture, new UIBox2(x, y, x + fishWidth, y + fishHeight), modulate);
        }

        handle.DrawRect(water, GlassColor, false);

        if (!_broken)
            return;

        // Cracks, so a broken tank is obvious at a glance.
        handle.DrawLine(new Vector2(left + width * 0.28f, top), new Vector2(left + width * 0.4f, bottom), GlassColor);
        handle.DrawLine(new Vector2(left + width * 0.72f, top), new Vector2(left + width * 0.6f, bottom), GlassColor);
        handle.DrawLine(new Vector2(left + width * 0.4f, top + height * 0.45f),
            new Vector2(left + width * 0.6f, top + height * 0.4f), GlassColor);
    }

    private Color GetWaterColor()
    {
        if (_broken)
            return Color.FromHex("#241a1a");

        return _fluid switch
        {
            AquariumFluid.Saltwater => Color.FromHex("#163a63"),
            AquariumFluid.SulphWater => Color.FromHex("#4a4a1a"),
            AquariumFluid.Air => Color.FromHex("#1B1B1E"),
            _ => Color.FromHex("#1d4f6e"),
        };
    }

    private FishVisual? CreateVisual(AquariumFishData data)
    {
        var proto = ResolveProto(data);
        var specifier = proto?.AquariumSprite ?? proto?.Sprite;
        if (specifier == null)
            return null;

        var texture = _sprite.Frame0(specifier);
        var seed = (uint) data.Entity.Id;

        return new FishVisual
        {
            Texture = texture,
            Alive = data.Alive,
            Color = proto?.AquariumColor ?? Color.White,
            SeedX = Hash01(seed, 1),
            SeedY = Hash01(seed, 2),
            Phase = Hash01(seed, 3) * MathF.Tau,
            Speed = 0.5f + Hash01(seed, 4),
            Aspect = (float) texture.Width / texture.Height,
        };
    }

    private FishPrototype? ResolveProto(AquariumFishData data)
    {
        if (string.IsNullOrEmpty(data.Prototype))
            return null;

        if (_proto.TryIndex<EntityPrototype>(data.Prototype, out var entityProto) &&
            entityProto.TryGetComponent<FishComponent>("Fish", out var fishComponent) &&
            fishComponent.Fish is { } species &&
            _proto.TryIndex(species, out FishPrototype? fishProto))
            return fishProto;

        // shortcut: most fish entities share the id of their species, so try that before giving up.
        return _proto.TryIndex<FishPrototype>(data.Prototype, out var direct) ? direct : null;
    }

    /// <summary>
    /// Small deterministic hash used to place and animate a fish based on its NetEntity.
    /// </summary>
    private static float Hash01(uint seed, uint salt)
    {
        var value = seed * 747796405u + salt * 2891336453u;
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        value ^= value >> 16;
        return (value & 0xFFFFFF) / (float) 0x1000000;
    }

    private static float PositiveModulo(float value, float modulo)
    {
        var result = value % modulo;
        return result < 0f ? result + modulo : result;
    }

    private sealed class FishVisual
    {
        public Texture? Texture;
        public Color Color = Color.White;
        public bool Alive;
        public float SeedX;
        public float SeedY;
        public float Phase;
        public float Speed;
        public float Aspect = 1f;
    }
}
