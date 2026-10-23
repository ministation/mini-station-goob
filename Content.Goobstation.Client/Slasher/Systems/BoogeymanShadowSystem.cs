using Content.Client._Shitcode.Heretic;
using Content.Client.Graphics;
using Content.Goobstation.Shared.Slasher.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Slasher.Systems;

/// <summary>
/// Makes entities invisible in darkness and visible in light, and hides their status icons from everyone else.
/// </summary>
public sealed partial class BoogeymanShadowSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "SlasherBoogeyman";
    public const string PostShaderId = "boogeyman-shadow";

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _proto.Index(Shader).InstanceUnique();

        SubscribeLocalEvent<BoogeymanShadowComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BoogeymanShadowComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<GetStatusIconsEvent>(OnGetStatusIcons, after: [typeof(GhoulSystem)]);
    }

    private void OnStartup(Entity<BoogeymanShadowComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Use the ordered post-shader pipeline (same slot as the stealth shader) instead of the legacy
        // PostShader setter. The legacy path bypasses post-shader ordering and clears any other
        // post-shaders on the entity, which breaks compositing on the pooled render target pipeline.
        _sprite.SetPostShader((ent, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, _shader)
        {
            Before = ContentPostShaderIds.BeforeOutlines,
        });
    }

    private void OnShutdown(Entity<BoogeymanShadowComponent> ent, ref ComponentShutdown args)
    {
        if (Terminating(ent))
            return;

        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.RemovePostShader((ent, sprite), PostShaderId);
    }

    private void OnGetStatusIcons(ref GetStatusIconsEvent args)
    {
        if (args.Uid != _player.LocalEntity && HasComp<BoogeymanShadowComponent>(args.Uid))
            args.StatusIcons.Clear();
    }
}
