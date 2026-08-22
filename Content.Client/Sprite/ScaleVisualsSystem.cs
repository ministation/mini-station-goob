using System.Numerics;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Sprite;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Sprite;

public sealed class ScaleVisualsSystem : SharedScaleVisualsSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScaleVisualsComponent, AppearanceChangeEvent>(OnChangeData);
    }

    private void OnChangeData(Entity<ScaleVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!args.AppearanceData.TryGetValue(ScaleVisuals.Scale, out var scale) ||
            args.Sprite == null) return;

        // save the original scale
        ent.Comp.OriginalScale ??= args.Sprite.Scale;

        var vecScale = (Vector2)scale;
        if (TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid) &&
            _proto.TryIndex(humanoid.Species, out SpeciesPrototype? species))
        {
            var height = Math.Clamp(humanoid.Height, species.MinHeight, species.MaxHeight);
            var width = Math.Clamp(humanoid.Width, species.MinWidth, species.MaxWidth);
            vecScale = new Vector2(width, height) * vecScale;
        }

        _sprite.SetScale((ent.Owner, args.Sprite), vecScale);
    }

    // revert to the original scale
    protected override void ResetScale(Entity<Shared.Sprite.ScaleVisualsComponent> ent)
    {
        base.ResetScale(ent);

        if (ent.Comp.OriginalScale != null)
            _sprite.SetScale(ent.Owner, ent.Comp.OriginalScale.Value);
    }
}
