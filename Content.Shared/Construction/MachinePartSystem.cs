// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Mini.Construction.Prototypes;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared.Construction
{
    /// <summary>
    /// Deals with machine parts and machine boards.
    /// </summary>
    public sealed class MachinePartSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly SharedLatheSystem _lathe = default!;
        [Dependency] private readonly SharedConstructionSystem _construction = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MachineBoardComponent, ExaminedEvent>(OnMachineBoardExamined);
        }

        private void OnMachineBoardExamined(EntityUid uid, MachineBoardComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            using (args.PushGroup(nameof(MachineBoardComponent)))
            {
                args.PushMarkup(Loc.GetString("machine-board-component-on-examine-label"));
                foreach (var (material, amount) in component.StackRequirements)
                {
                    var stack = _prototype.Index(material);
                    var name = _prototype.Index(stack.Spawn).Name;

                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", amount),
                        ("requiredElement", Loc.GetString(name))));
                }

                // Mini: Orion MachineParts
                foreach (var (partType, amount) in component.PartRequirements)
                {
                    string requiredElement;
                    if (_prototype.TryIndex(partType, out MachinePartPrototype? machinePart))
                        requiredElement = Loc.GetString(machinePart.Name);
                    else
                        requiredElement = partType;

                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", amount),
                        ("requiredElement", requiredElement)));
                }

                foreach (var (_, info) in component.ComponentRequirements)
                {
                    var examineName = _construction.GetExamineName(info);
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", info.Amount),
                        ("requiredElement", examineName)));
                }

                foreach (var (_, info) in component.TagRequirements)
                {
                    var examineName = _construction.GetExamineName(info);
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", info.Amount),
                        ("requiredElement", examineName)));
                }
            }
        }

        public bool TryGetMachineBoardMaterialCost(Entity<MachineBoardComponent> entity, out Dictionary<string, int> materials, int coefficient = 1)
        {
            var (_, comp) = entity;

            materials = new Dictionary<string, int>();

            foreach (var (stackId, amount) in comp.StackRequirements)
            {
                var stackProto = _prototype.Index(stackId);
                var defaultProto = _prototype.Index(stackProto.Spawn);

                if (defaultProto.TryGetComponent<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in physComp.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (_lathe.TryGetRecipesFromEntity(stackProto.Spawn, out var recipes))
                {
                    var partRecipe = recipes[0];
                    if (recipes.Count > 1)
                        partRecipe = recipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    return false;
                }
            }

            foreach (var (partType, amount) in comp.PartRequirements)
            {
                if (!_prototype.TryIndex(partType, out MachinePartPrototype? machinePart))
                    return false;

                if (!_prototype.Resolve(machinePart.StockPartPrototype, out var partProto))
                    return false;

                if (partProto.TryGetComponent<PhysicalCompositionComponent>(out var partPhys, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in partPhys.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (_lathe.TryGetRecipesFromEntity(machinePart.StockPartPrototype, out var partRecipes))
                {
                    var partRecipe = partRecipes[0];
                    if (partRecipes.Count > 1)
                        partRecipe = partRecipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    return false;
                }
            }

            // Mini: Orion MachineParts
            foreach (var (partType, amount) in comp.PartRequirements)
            {
                if (!_prototype.TryIndex(partType, out MachinePartPrototype? machinePart))
                    return false;

                if (!_prototype.Resolve(machinePart.StockPartPrototype, out var partProto))
                    return false;

                if (partProto.TryGetComponent<PhysicalCompositionComponent>(out var partPhys, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in partPhys.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (_lathe.TryGetRecipesFromEntity(machinePart.StockPartPrototype, out var partRecipes))
                {
                    var partRecipe = partRecipes[0];
                    if (partRecipes.Count > 1)
                        partRecipe = partRecipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    return false;
                }
            }

            var genericPartInfo = comp.ComponentRequirements.Values.Concat(comp.TagRequirements.Values);
            foreach (var info in genericPartInfo)
            {
                var amount = info.Amount;
                var defaultProtoId = info.DefaultPrototype;

                if (_lathe.TryGetRecipesFromEntity(defaultProtoId, out var recipes))
                {
                    var partRecipe = recipes[0];
                    if (recipes.Count > 1)
                        partRecipe = recipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (_prototype.Resolve(defaultProtoId, out var defaultProto) &&
                         defaultProto.TryGetComponent<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in physComp.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
