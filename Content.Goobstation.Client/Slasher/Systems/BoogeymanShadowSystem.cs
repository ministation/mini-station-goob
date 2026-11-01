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
    public const string PostShaderId = "slasher-boogeyman";

    private static readonly string[] RunAfter =
    [
        ContentPostShaderIds.Stealth,
        ContentPostShaderIds.FloorOcclusion,
        ContentPostShaderIds.Holopad,
        ContentPostShaderIds.InteractionOutline,
        ContentPostShaderIds.TargetOutline,
        ContentPostShaderIds.DragDropOutline,
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoogeymanShadowComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BoogeymanShadowComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<GetStatusIconsEvent>(OnGetStatusIcons, after: [typeof(GhoulSystem)]);
    }

    private void OnStartup(Entity<BoogeymanShadowComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            ApplyShader((ent.Owner, sprite));
    }

    private void OnShutdown(Entity<BoogeymanShadowComponent> ent, ref ComponentShutdown args)
    {
        if (Terminating(ent))
            return;

        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.RemovePostShader((ent.Owner, sprite), PostShaderId);
    }

    private void ApplyShader(Entity<SpriteComponent> sprite)
    {
        _sprite.SetPostShader((sprite.Owner, sprite.Comp),
            new SpriteComponent.PostShaderArgs(PostShaderId, _proto.Index(Shader).InstanceUnique())
            {
                After = RunAfter,
            });
    }

    private void OnGetStatusIcons(ref GetStatusIconsEvent args)
    {
        if (args.Uid != _player.LocalEntity && HasComp<BoogeymanShadowComponent>(args.Uid))
            args.StatusIcons.Clear();
    }
}
