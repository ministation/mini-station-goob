using Content.Shared.Humanoid;

namespace Content.Shared.Height;

/// <summary>
/// Mini port of Wega genetics height genes — uses HumanoidAppearanceComponent (EE height sliders).
/// </summary>
public sealed partial class HeightSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmallHeightComponent, ComponentStartup>(OnSmallHeightComponentStartup);
        SubscribeLocalEvent<BigHeightComponent, ComponentStartup>(OnBigHeightComponentStartup);

        SubscribeLocalEvent<SmallHeightComponent, ComponentShutdown>(OnSmallHeightComponentShutdown);
        SubscribeLocalEvent<BigHeightComponent, ComponentShutdown>(OnBigHeightComponentShutdown);
    }

    private void OnSmallHeightComponentStartup(Entity<SmallHeightComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        ent.Comp.LastHeight = humanoid.Height;
        _humanoid.SetHeight(ent.Owner, 0.8f, humanoid: humanoid);
    }

    private void OnBigHeightComponentStartup(Entity<BigHeightComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        ent.Comp.LastHeight = humanoid.Height;
        var target = humanoid.Height < 1.2f ? 1.2f : 1.4f;
        _humanoid.SetHeight(ent.Owner, target, humanoid: humanoid);
    }

    private void OnSmallHeightComponentShutdown(Entity<SmallHeightComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid) || ent.Comp.LastHeight == default)
            return;

        _humanoid.SetHeight(ent.Owner, ent.Comp.LastHeight, humanoid: humanoid);
    }

    private void OnBigHeightComponentShutdown(Entity<BigHeightComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid) || ent.Comp.LastHeight == default)
            return;

        _humanoid.SetHeight(ent.Owner, ent.Comp.LastHeight, humanoid: humanoid);
    }
}
