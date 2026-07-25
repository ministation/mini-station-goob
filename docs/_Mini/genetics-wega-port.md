# Genetics port: Wega (Paradise) → Mini

Port of Corvax-Wega genetics ([PR #73](https://github.com/wega-team/ss14-wega/pull/73)) into Mini.
Player guide reference: [SS220 Руководство по генетике](https://wiki.ss220.space/index.php/Руководство_по_генетике).

## License

Source code and content under `Content.*/_Mini/Genetics` and `Resources/**/_Mini/Genetics` are derived from
**wega-team/ss14-wega** (`_Wega` / Corvax-Wega), licensed **GPL-3.0**. Authors include Zekins3366 / wega-team.
Keep attribution; Mini AGPL remains compatible for combined distribution.

## Paradise vs modern TG

| | Modern /tg/ | Paradise / SS220 / **this port** |
|---|---|---|
| Loop | ATCG sequencer + discovery aliases | Hex SE blocks 1–55, irradiate subblocks |
| Machine | DNA Scanner + Console | DNA Modifier console + MedicalScanner bay |
| Data | mutation sequences | SE / UI / UE hex (`EnzymesPrototypeInfo.HexCode`) |
| Thresholds | solve pairs | DAC / BEA / 802; block 55 = monkey |

## Layout (Wega → Mini)

| Wega | Mini |
|---|---|
| `Content.*/_Wega/Genetics` | `Content.*/_Mini/Genetics` |
| `namespace Content.Shared.Genetics` | unchanged (files under `_Mini`) |
| `Resources/Prototypes/_Wega/Genetics` | `Resources/Prototypes/_Mini/Genetics/Enzymes` |
| Machines / injectors / job | `Resources/Prototypes/_Mini/Genetics/*.yml` |
| Textures | `Resources/Textures/_Mini/Genetics/` |
| Locale | `Resources/Locale/ru-RU/_Mini/genetics/` |
| Guidebook | `Resources/ServerInfo/_Mini/Guidebook/Medical/Genetic.xml` |

## Core types

- `DnaModifierComponent` — SE/UI on organics; instability
- `DnaModifierConsole` + `DnaClient` — UI / buffers (needs `DnaServer` on R&D server)
- MedicalScanner — subject bay (device link `MedicalScannerSender`)
- `StructuralEnzymesPrototype` — gene catalog YAML
- `DnaModifierInjector` / Clean SE / Disk
- `DnaInstabilityComponent` — stages 1–3

## Adaptation checklist

- [x] Copy Shared/Server/Client genetics from Wega
- [x] Remap textures `_Wega` → `_Mini/Genetics`
- [x] Rename genetics mob `Hulk` → `GeneticsHulk` (avoid Wizard `HulkComponent`)
- [x] Add `DnaModifier` to organic species base
- [x] Add `DnaServer` to `ResearchAndDevelopmentServer`
- [x] Mutadon reagent + ChemMutateDna on UnstableMutagen
- [x] GeneEngineering unlocks `DnaModifierComputerCircuitboard`
- [x] Geneticist job + playtime tracker + Medical department
- [x] Guidebook wired into Medical guides
- [x] Compile Content.Shared / Server / Client (0 errors)

## Maps & loadout (Mini)

- **Maps:** `Tools/_Mini/remap_genetics_medbay.py` remaps `Maps/_Mini` only (not `_CorvaxGoob`).
  - `SpawnPointGeneticist` is always placed on a free tile **adjacent to** `SpawnPointMedicalDoctor` (first doctor spawn).
  - `MedicalScanner` + `DnaModifierConsole` go next to that doctor cluster (reuse a scanner only if it is ≤10 tiles from the primary doctor; otherwise place a new pair). Bind range ≤4.
  - Skips `Events` / `Shuttles` / `CentComm` / `Bitrun`.
- **Loadout:** Geneticist starting gear includes `BoxDnaInjector` (`BaseAmmoProvider`, 10× empty `DnaInjector`) — same pattern as sterile swab boxes.

## Known gaps / stubs

- UI cosmetics: seeded from `HumanoidAppearance` (Wega VisualBody not ported).
- Deep inventory clone / TTS / barks skipped on polymorphism clone.
- Matter Eater / cough / height adapted to Mini APIs.
- Strong genes (TK, mind communication) may still need playtests.
- Do not merge Wega `ChatSystem`/`ForensicsSystem` patches wholesale — Mini job icons / forensics DNA stay.
- Stock / `_Goobstation` maps are out of scope for the remapper (Mini + CorvaxGoob Stations only).
