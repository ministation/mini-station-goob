// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Interactable.Components;
using Content.Client.Graphics;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Goobstation.Shared.Slasher.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Stealth;

public sealed class StealthSystem : SharedStealthSystem
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Stealth";

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _protoMan.Index(Shader).InstanceUnique();

        SubscribeLocalEvent<StealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StealthComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    public override void Update(float frameTime) // for stealth to return to harpy after flight ends
    // used for any system that forces a different shader on something while also having stealth from cobra gland
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StealthComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var stealth, out var sprite))
        {
            var hasStealthShader = _sprite.TryGetPostShader((uid, sprite), ContentPostShaderIds.Stealth, out var entry)
                && entry.GetScreenTexture
                && entry.RaiseShaderEvent;

            if (stealth.Enabled && !hasStealthShader)
                SetShader(uid, true, stealth, sprite);
            else if (!stealth.Enabled && _sprite.HasPostShader((uid, sprite), ContentPostShaderIds.Stealth))
                SetShader(uid, false, stealth, sprite);
        }
    }

    public override void SetEnabled(EntityUid uid, bool value, StealthComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Enabled == value)
            return;

        base.SetEnabled(uid, value, component);
        SetShader(uid, value, component);
    }

    private void SetShader(EntityUid uid, bool enabled, StealthComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false))
            return;

        _sprite.SetColor((uid, sprite), Color.White);
        if (enabled)
        {
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.Stealth, _shader)
            {
                GetScreenTexture = true,
                RaiseShaderEvent = true,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
        else
        {
            _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.Stealth);
        }

        if (enabled)
        {
            if (RemCompDeferred<InteractionOutlineComponent>(uid))
                component.HadOutline = true;
        }
        else if (component.HadOutline && !TerminatingOrDeleted(uid))
        {
            EnsureComp<InteractionOutlineComponent>(uid);
            component.HadOutline = false;
        }
    }

    private void OnStartup(EntityUid uid, StealthComponent component, ComponentStartup args)
    {
        SetShader(uid, component.Enabled, component);
    }

    private void OnShutdown(EntityUid uid, StealthComponent component, ComponentShutdown args)
    {
        if (!Terminating(uid))
            SetShader(uid, false, component);
    }

    private void OnShaderRender(EntityUid uid, StealthComponent component, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != ContentPostShaderIds.Stealth)
            return;

        // Distortion effect uses screen coordinates. If a player moves, the entities appear to move on screen. this
        // makes the distortion very noticeable.

        // So we need to use relative screen coordinates. The reference frame we use is the parent's position on screen.
        // this ensures that if the Stealth is not moving relative to the parent, its relative screen position remains
        // unchanged.
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return; // should never happen, but lets not kill the client.
        var parentXform = Transform(parent);
        var reference = args.Viewport.WorldToLocal(_transformSystem.GetWorldPosition(parentXform));
        reference.X = -reference.X;
        var visibleToLocal = IsVisibleToLocalPlayer(uid);
        var visibility = GetRenderedVisibility(GetVisibility(uid, component), visibleToLocal);

        if (visibleToLocal &&
            TryComp<SlasherIncorporealComponent>(uid, out var slasher) &&
            slasher.IsIncorporeal)
        {
            // Keep slasher readable for its own player only.
            visibility = MathF.Max(visibility, 0.35f);
        }

        // Goobstation - Proper invisibility: changes -1 to -1.5
        // actual visual visibility effect is limited to -1.5 to 1.
        visibility = Math.Clamp(visibility, -1.5f, 1f);

        args.Shader.SetParameter("reference", reference);
        args.Shader.SetParameter("visibility", visibility);

        visibility = MathF.Max(0, visibility);
        _sprite.SetColor((uid, args.Sprite), new Color(visibility, visibility, 1, 1));
    }

    /// <summary>
    /// Visibility that actually gets rendered for one client. Fully hidden sprites leave a distortion
    /// smear on the screen, which is how other players used to spot an "invisible" entity: show that
    /// effect only to the entity's own player, and render untouched screen (nothing at all) for everyone
    /// else. At -1.5 the shader's distortion is zero, so the sprite blends into the background.
    /// </summary>
    public static float GetRenderedVisibility(float visibility, bool visibleToLocalPlayer)
    {
        if (visibility > 0f)
            return visibility;

        return visibleToLocalPlayer ? MathF.Max(visibility, -0.5f) : -1.5f;
    }

    /// <summary>
    /// Whether the local player is the cloaked entity itself, or holds/contains it. Used to keep the
    /// distortion effect readable for the cloaked player while everyone else sees nothing.
    /// </summary>
    private bool IsVisibleToLocalPlayer(EntityUid uid)
    {
        if (_player.LocalEntity is not { Valid: true } local)
            return false;

        if (local == uid)
            return true;

        for (var current = Transform(local).ParentUid; current.IsValid(); current = Transform(current).ParentUid)
        {
            if (current == uid)
                return true;
        }

        for (var current = Transform(uid).ParentUid; current.IsValid(); current = Transform(current).ParentUid)
        {
            if (current == local)
                return true;
        }

        return false;
    }
}
