// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Medical.Components;
using Content.Shared._Mini.Ninja.Base.HealthAnalyzer;
using Content.Shared.Clothing;
using Content.Shared.Mobs.Components;
using Content.Shared.MedicalScanner;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Mini.Ninja.Base.HealthAnalyzer;

/// <summary>
/// Opens the standard health analyzer UI on the suit, scanning the wearer.
/// </summary>
public sealed class NinjaSelfHealthAnalyzerSystem : SharedNinjaSelfHealthAnalyzerSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSelfHealthAnalyzerComponent, NinjaSelfAnalyzeEvent>(OnAnalyze);
        SubscribeLocalEvent<NinjaSelfHealthAnalyzerComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnAnalyze(Entity<NinjaSelfHealthAnalyzerComponent> ent, ref NinjaSelfAnalyzeEvent args)
    {
        args.Handled = true;

        if (!HasComp<MobStateComponent>(args.Performer))
            return;

        if (!TryComp<HealthAnalyzerComponent>(ent.Owner, out var analyzer))
            return;

        if (!_ui.HasUi(ent.Owner, HealthAnalyzerUiKey.Key))
            return;

        if (!analyzer.Silent)
            _audio.PlayPvs(analyzer.ScanningEndSound, ent.Owner);

        analyzer.ScannedEntity = args.Performer;
        _ui.OpenUi(ent.Owner, HealthAnalyzerUiKey.Key, args.Performer);
    }

    private void OnUnequipped(Entity<NinjaSelfHealthAnalyzerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!TryComp<HealthAnalyzerComponent>(ent.Owner, out var analyzer))
            return;

        analyzer.ScannedEntity = null;

        if (_ui.IsUiOpen(ent.Owner, HealthAnalyzerUiKey.Key, args.Wearer))
            _ui.CloseUi(ent.Owner, HealthAnalyzerUiKey.Key, args.Wearer);
    }
}
