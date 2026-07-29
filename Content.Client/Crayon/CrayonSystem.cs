// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Crayon;

public sealed class CrayonSystem : SharedCrayonSystem
{
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<CrayonComponent>(ent => new StatusControl(ent, _charges, EntityManager));
    }

    private sealed class StatusControl : Control
    {
        private readonly Entity<CrayonComponent> _crayon;
        private readonly SharedChargesSystem _charges;
        private readonly RichTextLabel _label;
        private readonly int? _capacity;

        public StatusControl(Entity<CrayonComponent> crayon, SharedChargesSystem charges, EntityManager entityManager)
        {
            _crayon = crayon;
            _charges = charges;
            if (entityManager.TryGetComponent(_crayon.Owner, out LimitedChargesComponent? limited))
                _capacity = limited.MaxCharges;
            _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            // Unlimited crayons (e.g. CrayonBlood) have no LimitedChargesComponent.
            if (_capacity is not { } capacity)
            {
                _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("crayon-drawing-label",
                    ("color", _crayon.Comp.Color),
                    ("state", _crayon.Comp.SelectedState),
                    ("infinite", true),
                    ("charges", -1),
                    ("capacity", -1)));
                return;
            }

            _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("crayon-drawing-label",
                ("color", _crayon.Comp.Color),
                ("state", _crayon.Comp.SelectedState),
                ("infinite", false),
                ("charges", _charges.GetCurrentCharges(_crayon.Owner)),
                ("capacity", capacity)));
        }
    }
}
