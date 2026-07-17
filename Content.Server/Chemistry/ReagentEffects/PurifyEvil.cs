using System.Threading;
using Content.Shared.EntityEffects;
using Content.Shared.Jittering;
using Content.Shared.WhiteDream.BloodCult.BloodCultist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.ReagentEffects;

[UsedImplicitly]
public sealed partial class PurifyEvil : EntityEffectBase<PurifyEvil>
{
    [DataField]
    public float Amplitude = 10.0f;

    [DataField]
    public float Frequency = 4.0f;

    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(30.0f);

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-purify-evil");
    }
}

public sealed partial class PurifyEvilEntityEffectSystem : EntityEffectSystem<TransformComponent, PurifyEvil>
{
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<PurifyEvil> args)
    {
        if (!TryComp(entity, out BloodCultistComponent? bloodCultist) ||
            bloodCultist.DeconvertToken is not null)
        {
            return;
        }

        var effect = args.Effect;
        _jitter.DoJitter(entity, effect.Time, true, effect.Amplitude, effect.Frequency);

        bloodCultist.DeconvertToken = new CancellationTokenSource();
        var uid = entity.Owner;
        Robust.Shared.Timing.Timer.Spawn(effect.Time, () => DeconvertCultist(uid),
            bloodCultist.DeconvertToken.Token);
    }

    private void DeconvertCultist(EntityUid uid)
    {
        if (HasComp<BloodCultistComponent>(uid))
            RemComp<BloodCultistComponent>(uid);
    }
}
