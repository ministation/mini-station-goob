// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.TypanWar;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Updates capture flag sprites from replicated owner state.
/// </summary>
public sealed class TypanWarCaptureFlagSystem : EntitySystem
{
    private static readonly ResPath BannerRsi = new("Structures/Decoration/banner.rsi");

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarCaptureFlagComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TypanWarCaptureFlagComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(EntityUid uid, TypanWarCaptureFlagComponent component, ComponentStartup args)
    {
        ApplyVisual(uid, component);
    }

    private void OnAfterState(EntityUid uid, TypanWarCaptureFlagComponent component, ref AfterAutoHandleStateEvent args)
    {
        ApplyVisual(uid, component);
    }

    private void ApplyVisual(EntityUid uid, TypanWarCaptureFlagComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var state = component.CaptureOwner switch
        {
            TypanWarCaptureOwner.Nanotrasen => "banner",
            TypanWarCaptureOwner.Typan => "banner_syndicate",
            _ => "banner-white",
        };

        _sprite.LayerSetRsi((uid, sprite), 0, BannerRsi, state);
    }
}
