# Genetics: /tg/station parity gap analysis

Audit of Mini's genetics implementation vs current /tg/station, with a review of the
mutations added by this PR (Night Vision, Chemical Allergy, Venomous, Regeneration).

## Sources and method

* /tg/station master snapshot dumped locally at `C:\ss14\tg-genetics-dump`
  (`code/datums/mutations/*.dm`, `code/datums/dna`, `code/game/machinery/dna_infuser`,
  `code/game/objects/items/dna_*.dm`, `code/datums/status_effects/debuffs/genetic_damage.dm`,
  `code/__DEFINES/DNA.dm`, `code/_globalvars/genetics.dm`).
* Sparse `git clone` of tgstation at `C:\ss14\tgstation-ref`
  (`code/game/objects/items/chromosome.dm`, `dna_injector.dm`, `dna_probe.dm`, `code/__DEFINES/DNA.dm`).
* This fork: `Content.Shared/_Trauma/Genetics`, `Content.Server/_Trauma/Genetics`,
  `Resources/Prototypes/_Trauma/Genetics`, `Resources/Locale/{en-US,ru-RU}/_Trauma/genetics`.

Counts below were taken by parsing the prototypes and the .dm sources directly, not from memory.
tg counts = named mutation datums in `code/datums/mutations`, excluding the 3 code-only variants
(`chameleon/changeling`, `hulk/wizardly`, `hulk/superhuman`), which are never part of the gene pool.

## TL;DR

| | tg | Mini before PR | Mini after PR |
|---|---|---|---|
| Mutation prototypes (`Resources/Prototypes/_Trauma/Genetics/Mutations`) | 80 named | 79 | 83 |
| tg mutations covered | 80 | 72 (90%) | 76 (95%) |
| Mutations Mini has that tg does not | — | 7 | 7 |
| tg mutations still missing | — | 8 | 4 |
| Missing/incomplete non-mutation tg genetics systems | 6 | 6 | 6 |

Still missing after this PR: **Extremely Unsightly**, **Extremely Comely**,
**Biotech Compatibility**, **X Ray Vision**; plus **Unstable DNA** exists only as an empty stub.

Larger non-mutation gaps: **DNA Infuser** (machinery + organ sets), **mutation chromosomes**
(+ mutation coefficients), **DNA Vault station goal + DNA probe**. See section 4.

## 1. Mutation inventory

### 1.1 Covered by existing Mini prototypes (73)

Format: Mini prototype id - in-game name - tg source.

| Mini prototype | Name | tg datum (file) |
|---|---|---|
| MutationColdAdaptation | Cold Adaptation | adaptation/cold (adaptation.dm) |
| MutationHeatAdaptation | Heat Adaptation | adaptation/heat (adaptation.dm) |
| MutationThermalAdaptation | Thermal Adaptation | adaptation/thermal (adaptation.dm) |
| MutationPressureAdaptation | Pressure Adaptation | adaptation/pressure (adaptation.dm) |
| MutationAdrenalineRush | Adrenaline Rush | adrenaline_rush (active.dm) |
| MutationAntenna | Antenna | antenna (antenna.dm) |
| MutationMindReader | Mind Reader | mindreader (antenna.dm) |
| MutationAutotomy | Autotomy | self_amputation (autotomy.dm) |
| MutationEpilepsy | Epilepsy | epilepsy (body.dm) |
| MutationCough | Cough | cough (body.dm) |
| MutationParanoia | Paranoia | paranoia (body.dm) |
| MutationDwarfism | Dwarfism | dwarfism (body.dm) |
| MutationAcromegaly | Acromegaly | acromegaly (body.dm) |
| MutationGigantism | Gigantism | gigantism (body.dm) |
| MutationClumsiness | Clumsiness | clumsy (body.dm) |
| MutationTourettes | Tourette's Syndrome | tourettes (body.dm) |
| MutationDeafness | Deafness | deaf (body.dm) |
| MutationMonkified | Monkified | race (body.dm) |
| MutationGlowy | Glowy | glow (body.dm) |
| MutationAntiGlow | Anti-Glow | glow/anti (body.dm) |
| MutationStrength | Strength | strong (body.dm) |
| MutationStimmed | Stimmed | stimmed (body.dm) |
| MutationInsulated | Insulated | insulated (body.dm) |
| MutationFierySweat | Fiery Sweat | fire (body.dm) |
| MutationSpatialInstability | Spatial Instability | badblink (body.dm) |
| MutationAcidicFlesh | Acidic Flesh | acidflesh (body.dm) |
| MutationSpastic | Spastic | spastic (body.dm) |
| MutationTwoLeftFeet | Two Left Feet | extrastun (body.dm) |
| MutationInternalMartyrdom | Internal Martyrdom | martyrdom (body.dm) |
| MutationHeadless | H.A.R.S. | headless (body.dm) |
| MutationHypermetabolicBlood | Hypermetabolic Blood | bloodier (body.dm) |
| MutationRockEater | Rock Eater | rock_eater (body.dm) |
| MutationRockAbsorber | Rock Absorber | rock_absorber (body.dm) |
| MutationInexorable | Inexorable | inexorable (body.dm) |
| MutationUnstableDna | Unstable DNA | bad_dna (body.dm) - **entity exists, behaviour is a TODO stub** |
| MutationChameleon | Chameleon | chameleon (chameleon.dm) |
| MutationGeladikinesis | Geladikinesis | geladikinesis (cold.dm) |
| MutationCryokinesis | Cryokinesis | cryokinesis (cold.dm) |
| MutationFarsight | Farsight | farsight (farsight.dm) |
| MutationFireBreath | Fire Breath | firebreath (fire_breath.dm) |
| MutationCindikinesis | Cindikinesis | cindikinesis (hot.dm) |
| MutationPyrokinesis | Pyrokinesis | pyrokinesis (hot.dm) |
| MutationHulk | Hulk | hulk (hulk.dm) |
| MutationOrk | Ork | hulk/ork (hulk.dm) |
| MutationDogNose | Transcendent Olfaction | olfaction (olfaction.dm) |
| MutationClever | Clever | clever (passive.dm) |
| MutationRadioactivity | Radioactivity | radioactive (radioactive.dm) |
| MutationTelekinesis | Telekinesis | telekinesis (reach.dm) |
| MutationElasticArms | Elastic Arms | elastic_arms (reach.dm) |
| MutationNearSightedness | Near Sightedness | nearsight (sight.dm) |
| MutationBlindness | Blindness | blind (sight.dm) |
| MutationThermalVision | Thermal Vision | thermal (sight.dm) |
| MutationIlliterate | Illiterate | illiterate (sight.dm) |
| MutationLaserEyes | Laser Eyes | laser_eyes (sight.dm) |
| MutationNervousness | Nervousness | nervousness (speech.dm) |
| MutationWacky | Wacky | wacky (speech.dm) |
| MutationHeckaciousLarincks | Heckacious Larincks | heckacious (speech.dm) |
| MutationMute | Mute | mute (speech.dm) |
| MutationUnintelligible | Unintelligible | unintelligible (speech.dm) |
| MutationSwedish | Swedish | swedish (speech.dm) |
| MutationChav | Chav | chav (speech.dm) |
| MutationElvis | Elvis | elvis (speech.dm) |
| MutationStoner | Stoner | stoner (speech.dm) |
| MutationMedieval | Medieval | medieval (speech.dm) |
| MutationPigLatin | Pig Latin | piglatin (speech.dm) |
| MutationTongueSpike | Tongue Spike | tongue_spike (tongue_spike.dm) |
| MutationChemSpike | Chem Spike | tongue_spike/chem (tongue_spike.dm) |
| MutationVoidMagnet | Void Magnet | void (void_magnet.dm) |
| MutationWebbingProduction | Webbing Production | webbing (webbing.dm) |
| MutationShockTouch | Shock Touch | shock (touch.dm) |
| MutationMendingTouch | Mending Touch | lay_on_hands (touch.dm) |
| MutationTelepathy | Telepathy | telepathy (telepathy.dm) |
| MutationNightVision | Night Vision | night_vision (sight.dm) - **added by this PR** |

Deliberate semantic deviations already present in these ports (not bugs, just notes for future work):

* tg `bad_dna` (Unstable DNA) has real behaviour (random mutation on gain). Mini has a locked
  admeme prototype with `instability: -40` and a `# TODO`; it does nothing. Easy to finish.
* tg `fire` (Fiery Sweat) only halves burn physiology; Mini additionally lowers Heat/Cold/Shock
  coefficients and adds a Heat Adaptation conflict (commented in YAML as non-tg).
* tg has no Separate "Gigantism/Stimmed" instability (0); Mini matches (inherits 0).
* tg Hulk has `wizardly`/`superhuman` variants for spells/gamemodes; Mini only has the gene-pool
  Hulk + Ork. No action needed unless those systems ever want a hulk variant.

### 1.2 Added by this PR (4)

| Mini prototype | Name | tg datum | Instability (tg / Mini) | Notes |
|---|---|---|---|---|
| MutationNightVision | Night Vision | night_vision (sight.dm) | +35 / +35 | Passive `NightVision` component instead of a trait; no toggle action. |
| MutationChemicalAllergy | Chemical Allergy | chemical_allergy (body.dm) | -40 / -40 | tg reacts to medicine/drug reagents; tg also halves instability while exposed (DNA stability system is not mirrored - see 4.2). |
| MutationVenomousStrikes | Venomous | venomous_strikes (body.dm) | +25 / +25 | tg element injects toxin on unarmed hits and filters the same toxin from its own blood. |
| MutationRegeneration | Regeneration | limb_regeneration (body.dm) | +25 / +25 | tg regrows limbs/organs while unconscious + fed, consuming nutrition; `can_acquire` blocked for `TRAIT_NOHUNGER`. |

### 1.3 Still missing after this PR (4)

| tg datum | Name | Why it is missing | Feasibility in SS14 |
|---|---|---|---|
| aoe_moodlet | Extremely Unsightly | Needs the mood system (`mood_event` + personality checks) and face-covering checks. | **Not feasible cheaply.** SS14 fork has no mood system at all (`MoodComponent`/`MoodSystem` do not exist). Would require porting tg's mood subsystem first. Could be approximated with status effects, but that changes the mechanic. Recommend leaving out. |
| aoe_moodlet/positive | Extremely Comely | Same as above. | Same as above. |
| biotechcompat | Biotech Compatibility | Only adjusts `skillchip_complexity_modifier` for skillchips, which do not exist in SS14. | **Not feasible / pointless.** No skillchip system. Out of scope unless skillchips are ported. |
| xray | X Ray Vision | Adds `TRAIT_XRAY_VISION`: a real through-wall renderer. SS14 has no x-ray vision rendering path; wizard scrying orb only toggles its own behaviour. | **Hard.** Requires a new renderer / light trick or a per-viewer occlusion system (large, risky). Out of scope for a mutation parity PR. |

### 1.4 Mini mutations that tg does not have (7)

These are pre-existing Mini/Goobstation additions, not tg parity gaps. Keep them unless a
parity-only ruleset is wanted.

| Mini prototype | Name | Comment |
|---|---|---|
| MutationFelinized | Felinized | Mini-specific (cat-person transformation). |
| MutationMouse | Mouse | Admin-only polymorph (`BaseMutationAdmeme`). |
| MutationEnhancedPerception | Enhanced Perception | Viewcone bonus (`ViewconeModifier`). Not in tg. |
| MutationWeakPerception | Weak Perception | Viewcone malus. Not in tg. |
| MutationColdWeakness | Cold Weakness | Mini-specific "yin-yang" weakness pair (weakness.yml). |
| MutationHeatWeakness | Heat Weakness | Same. |
| MutationThermalWeakness | Homeostatic Shutdown | Same, craftable by combining the two above. |

### 1.5 Mutation combination recipes (tg `/datum/generecipe`)

tg defines 12 combination recipes in `code/datums/mutations/_combined.dm`. Mini has all 12 in
`Resources/Prototypes/_Trauma/Genetics/Recipes` with identical inputs, plus one Mini-only recipe
(`ThermalWeakness`, part of the Mini-specific weakness pair). Recipe parity is complete:

| Result | Inputs (same in tg) |
|---|---|
| Hulk | Strength + Radioactivity |
| Ork | Hulk + Clumsiness |
| Mind Reader | Antenna + Paranoia |
| Shock Touch | Insulated + Radioactivity |
| Cindikinesis | Geladikinesis + Fiery Sweat |
| Pyrokinesis | Cryokinesis + Fiery Sweat |
| Thermal Adaptation | Cold Adaptation + Heat Adaptation |
| Anti-Glow | Glowy + Void Magnet |
| Chem Spike | Tongue Spike + Stimmed |
| Internal Martyrdom | Strength + Stimmed |
| Heckacious Larincks | Wacky + Stoner |
| Rock Absorber | Rock Eater + Stoner |
| *(Mini only)* Homeostatic Shutdown | Cold Weakness + Heat Weakness |

Note: Mini's `UnlockedMutations` filters out anything that has a recipe, so all recipe results
are unrollable. This matches tg's `locked` flags for Hulk, Anti-Glow, Chem Spike, Internal
Martyrdom, Rock Absorber, Heckacious Larincks, Mind Reader, Shock Touch, Cindikinesis, Pyrokinesis
and Thermal Adaptation. The one difference: tg leaves **Ork** rollable (no `locked` flag) while
Mini can only obtain it by combining Hulk + Clumsiness. Harmless, but it is a parity deviation.

## 2. Mutation prototype conventions in Mini (for reviewers)

* Every concrete mutation prototype needs `- type: Mutation` with a required `instability` int,
  either directly or inherited from an abstract base (`BaseMutation`, `BaseFreeMutation`,
  `BaseTriggerMutation`, `BaseLightMutation`, `BaseMutationAdaptation`, `BaseMutationWeakness`,
  `BaseMutationAdmeme`). Abstract prototypes are not instantiated by Robust, so they may omit it.
* Optional `difficulty` (bases missing when sequencing), `rarity` (scanner flavour), `locked`
  (excluded from the random pool), `required`, `conflicts` (one-way - mirror them).
* Behaviour is attached with one of the shared mutation component systems:
  `ComponentMutation` (adds/removes components on the mutated mob), `EffectsMutation` (entity
  effects), `ActionMutation` (granted action), `StatusEffectsMutation`, or a custom component.
  Components added through `ComponentMutation` **must live in a shared assembly**, otherwise the
  prototype fails to load on the client.
* Localisation: `<MutationId>-mutated` is required (`Loc.GetString` throws on gain if missing),
  `<MutationId>-removed` is optional. New keys go into new .ftl files under
  `Resources/Locale/{en-US,ru-RU}/_Trauma/genetics/`.
* Difficulty/rarity are additions over tg (tg has `blocks`/`difficulty` defaults; Mini uses
  `difficulty`/4 base scrambles and its own rarity enum for the scanner). Values do not need to
  match tg exactly, only the gameplay behaviour.

## 3. Non-mutation tg genetics systems

| tg system | tg files | Mini status | Notes / feasibility |
|---|---|---|---|
| DNA console: scanner, sequencer, mutation storage, unique enzymes | `dna_console`, tgui `DnaConsole/*` | **Implemented, different UX** | `GeneticsConsole` + `GeneticsScanner` + `GeneticsConsoleEnzymes` + `GeneticsDiskSlot`; mob comes from linked `MedicalScanner` (tg has a dedicated DNA scanner machine). Sequencer UI is Mini's own (screen-based, not TGUI). |
| DNA injector (apply stored mutation) | `dna_injector.dm` | **Replaced by disks + printed injectors** | Mutations are stored on `GeneticsDisk`, printed from Biomass as mutator/activator/cleaner. Design divergence; documented in the guidebook. |
| Mutadone | reagent | **Replaced by cleaner** | No `Mutadone` reagent; print a cleaner from a disk. Fine. |
| Mutation sequencing "bases/blocks" model | `dna.dm`, `dna_block.dm` | **Implemented, simplified** | Mini uses `MutationData` with 32 bases + per-round numbering; base count fixed at 32 (tg 4 blocks x 4 pairs). Good enough. |
| DNA Infuser (organ infusion machines, infuser entries, organ sets, tier progression, infusion book) | `dna_infuser/*` (11 .dm files) | **Missing** | **Feature-sized (its own PR).** Needs: machine with occupant+infusion item, `infuser_entry` prototypes as YAML data, mutant organ prototypes (Mini has few/no animal mutant organs), organ-set bonuses as status effects, tier progression, error-report paper. tg tier 3 ("vatgrow") is intentionally unfinished upstream. Medium-high effort, low gameplay overlap with mutations. |
| Mutation chromosomes (stabilizer / synchronizer / power / energetic) + mutation coefficients | `chromosome.dm`, `_mutations.dm` (`stabilizer_coeff`, `synchronizer_coeff`, `power_coeff`, `energy_coeff`, `can_chromosome`) | **Missing** | Medium effort: add coefficient fields to `MutationComponent` (or a separate `MutationChromosomeComponent`), make behaviours/actions read them, generate chromosomes from random mutation rolls, apply via a console UI action. Would touch many mutation values; do as a dedicated PR. Not required for parity of the mutation list. |
| DNA Vault station goal + DNA probe | not in the local dump (goal machinery is elsewhere), `dna_probe.dm` | **Missing** | Medium effort: station goal prototype, vault machinery that accepts uploaded crew DNA, DNA probe item, goal completion reward. The fork has a generic `StationGoal` system (CorvaxGoob) to hook into. Separate content PR. |
| Genetic damage (separate damage pool; toxin damage above 500, gorillize simians at 2500) | `genetic_damage.dm` | **Partial / different model** | Mini models genetic damage as `Cellular` damage on `DamageableComponent` (`MutationSystem.GetGeneticDamage`), with console/scanner thresholds (`MaxGeneticDamage`) and total instability -> melting status effect. No gorillize, no toxin conversion. Functionally equivalent for the sequencer loop; note as semantic difference, not a blocker. |
| Temporary DNA transformation status effect (dna injector transformations, `trans_sting`) | `dna_transformation.dm` | **Missing** | Low priority. Mini has `PolymorphMutation` for transformations; the temporary DNA-copy variant is only used by tg's genetics injectors and changeling sting. |
| DNA scanning of other mobs via handheld probe | `dna_probe.dm` | **Partial** | Mini has `HandheldGeneticsScanner` that scans a clicked mob and links to a console. Good substitute for readout; no vault upload. |

## 4. Deliberately out of scope for this PR

* **Extremely Unsightly / Extremely Comely** - no mood system (see 1.3).
* **Biotech Compatibility** - no skillchips (see 1.3).
* **X Ray Vision** - no through-wall renderer (see 1.3).
* **DNA Infuser, chromosomes, DNA Vault** - feature-sized systems, not mutations; should be
  separate PRs (see section 3).
* **tg instability/DNA stability economy** (stability drives random mutation odds; chemical
  allergy halves instability while exposed) is not mirrored 1:1. Mini's `TotalInstability`
  is a per-mob accumulation used for the melting threshold, and `MutationSystem.AddInstability`
  is the only writer. Chemical Allergy therefore ports the symptom effects, not the stability
  half of the mechanic. If stability parity is wanted later, add a `Stability` field to
  `MutatableComponent` first.
* **lockered admin mutations**: Laser Eyes is admeme-only in Mini (matches tg `locked`),
  Locked mutations are excluded from random rolls but still existing prototypes.

## 5. QA review of the ports in this PR

Filled in while reviewing the files under `Content.Shared/_Trauma/Genetics/Abilities`,
`Content.Server/_Trauma/Genetics/Abilities` and the four new mutation YAMLs.

Reviewed the working tree state with all four ports present
(`night_vision.yml`, `chemical_allergy.yml`, `venomous.yml`, `regeneration.yml` +
their C# + en-US/ru-RU .ftl files). All four prototype ids are unique, all four have
`-mutated` (and `-removed`) locale keys, SPDX headers are present, and every component added
through `ComponentMutation` lives in `Content.Shared` (so prototypes load on the client).
Behaviour systems are server-side as intended. The API calls used were checked against this
codebase (signatures verified), so the compile-sensitive risk is low.

Legend: **Matches tg** = mechanics verified against the tg source; **Checked** = implementation
detail / API verified against this codebase; **DEV** = acceptable deviation from tg;
**LIMITATION** = known functional limitation.

### Night Vision

| | |
|---|---|
| Matches tg | instability +35, name/description, gain/lose messages, passive (no toggle action). |
| DEV | `rarity: Mythical` (tg has no rarity field; Mini's rarity is scanner flavour only). `difficulty` was left at the default 8, matching tg. |
| Known limitation | `ComponentMutation` removes the added `NightVision` component when the mutation is lost. If the same mob also has night vision from another source (ninja visor, changeling darkness adaption, species organ), losing the mutation strips that source's night vision too. Inherent to any `ComponentMutation`-added component; only a real problem for the (rare) double-source case. |
| Checked | `isActive: true` + `toggleAction: null` is correct for this component (`NightVisionComponent.ToggleAction` defaults to `ToggleNightVision`, so the YAML override is required to keep it passive). `IsEquipment` defaults to false, which selects the mob path in the client overlay system. `NightVision`/client system is in `Content.Goobstation.Shared`/`Client`, both loaded by the server and the client entrypoint. |

### Chemical Allergy

| | |
|---|---|
| Matches tg | instability -40, "allergic to medicine/drugs" concept, stutter/toxin/jitter-dizzy-bloodloss/confusion-losebreath symptom set, Mannitol exclusion, drug+medicine group targeting. |
| DEV | `difficulty: 20` vs tg default 8 (kept per the task brief; only affects sequencing effort); `rarity: Common` (scanner flavour). |
| Checked (rates) | `UpdateInterval = 1s` + `SymptomChance = 0.8` + independent 66/33/12/6 % rolls per second now matches tg's `SPT_PROB` cadence exactly. |
| DEV | Dizziness -> `StatusEffectWoozy`, confusion -> `StatusEffectDrunk`, `losebreath` -> 4 Asphyxiation damage (no losebreath equivalent). Reasonable stand-ins. |
| DEV / missing | tg also (a) adds a healthscanner readout ("Subject has Chemical Allergy...") via `COMSIG_LIVING_HEALTHSCAN` and (b) halves mutation instability while reacting (DNA stability system is not mirrored by Mini). Neither is required for the symptom port; note in the PR description. |
| DEV | Group filter `Medicine` + `Narcotics` is an approximation of tg's `typecacheof(/datum/reagent/medicine) + /datum/reagent/drug`: medicines/drugs that sit in another reagent group (Special/Biological/etc.) will not trigger it. Mutadone does not exist in the codebase; Mannitol does and is excluded. |
| Checked | Blood is only removed while the mob is above `BloodlossThreshold` (0.9), and the amount is scaled from tg's 12-24/560 to 6-12/300. Edge case: because the current code subtracts a flat 6-12 without clamping to the remaining margin, a mob just above the threshold can dip a couple of points below it (tiny, short-lived bloodloss). `metabolizing` proxy = reagent has `Metabolisms`, so reagents administered while in stasis still count as active (minor). |

### Venomous

| | |
|---|---|
| Matches tg | instability +25, 3 units of Toxin per unarmed hit, self-filters 0.99 units/s of the same reagent from its own bloodstream, works only via unarmed attacks. |
| DEV | `rarity: Rare` (scanner flavour only). `difficulty` was left at the default 8, matching tg. |
| Checked (important) | `MeleeHitEvent` is raised on the *melee weapon* entity; for unarmed attacks `SharedMeleeWeaponSystem.TryGetWeapon` falls back to `weaponUid = user`, so the mob's own component only sees unarmed/glove hits - this correctly matches tg (no venom with a held weapon). |
| DEV | Venom is injected on `MeleeHitEvent`, which is raised before the damage is applied; tg's element injects after the hit lands. Also dead targets and targets without an injectable solution are skipped (tg would still try to add reagent to any reagent holder). Cosmetic/edge cases. |
| DEV | tg swaps the gain message to "teeth and claws" for claw attacks; Mini has no claw attack-effect equivalent, so the static text is fine. |
| Note | The self-filter loop is a 1s `Update` over all mutated mobs (matches tg's on_life cadence). |

### Regeneration

| | |
|---|---|
| Matches tg | instability +25, regrows missing limbs/organs while unconscious (sleep or crit) and fed, consumes nutrition, 5 %/s attempt, organs/limbs prioritized over cosmetic organs, 50 % organ preference. |
| DEV | `difficulty: 20`, `rarity: Rare`. |
| LIMITATION (documented in code) | The head is in `IgnoredPartSlots`, so a decapitated mob never regrows its head; also brain/eyes/tongue are not reported missing when the head part is gone (the walk stops at the missing head slot). tg would `regenerate_limb(BODY_ZONE_HEAD)` + brain. Deliberate to avoid fighting H.A.R.S.; must be stated in the PR/guidebook if this mutation ships. |
| DEV | Cosmetic organ list is hardcoded `{eyes, tongue, butt}`. tg uses species `mutant_organ` list for "special" (cosmetic) organs and treats eyes/tongue as important. Consequence: eyes and tongue regrow last, and for non-human species the wrong organs are prioritized. Consider deriving cosmetic organs from the species body prototype/organs later. |
| Checked (parts) | A regrown limb's own organ slots are populated from the body prototype in the same pass (`PopulatePartOrgans`), so no extra nutrition pass is needed. For the human body prototype limbs have no organs anyway. |
| DEV | Hard crit: tg requires `stat < HARD_CRIT`, the port only skips dead, so a mob in hard crit can still regenerate. Minor. |
| DEV | Notification text differs slightly: tg only says "could use a meal" when unconscious+hungry and "could use a nap" when awake+fed; the port shows "nap" whenever awake regardless of hunger. Cosmetic. |
| Checked | `HungerComponent.Thresholds` access, `GetHunger`, `ModifyHunger`, body prototype walking, `TryCreatePartSlotAndAttach` / `TryCreateOrganSlot` / `InsertOrgan` / `GetPartSlotContainerId` / `GetOrganContainerId`, `MobStateSystem.IsCritical`, `SharedAudioSystem.PlayPvs` all match this codebase's signatures. `TryCreateOrganSlot` is safe when the organ slot already exists (returns true). |

### Cross-cutting

* ru-RU `regeneration.ftl` defines an extra unused `ent-MutationRegeneration` key (no en-US counterpart); the entity name comes from the YAML literal like every other mutation, so the key is dead text. Harmless, can be dropped.
* All four mutations are unlocked (rollable), matching tg (none of them are `locked` upstream).
* None of the ports adds a mutation recipe (Combine) - tg doesn't have recipes for these either, so nothing to do.
* Guidebook claim updated in this PR: `Resources/ServerInfo/_Trauma/Guidebook/Genetics.xml` no longer says
"Все мутации с tg здесь реализованы"; it now lists the three system-level exceptions (mood, skillchips, x-ray vision).

## 6. Suggested follow-up order

1. Finish `MutationUnstableDna` (random mutation on gain, locked admeme) - trivial, reuses
   `RandomNegativeMutation` / `AddMutation` effects that already exist.
2. Mutation chromosomes + coefficients - small content/UI surface, big depth for genetics.
3. Genetic damage status effect (toxin conversion + gorillize) - aligns with tg medical checks.
4. DNA Infuser + infuser entries + mutant organs - largest, needs art/organ content.
5. DNA Vault station goal + probe - depends on station goal content work.