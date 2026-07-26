#!/usr/bin/env python3
"""Copy DS gamma shuttle and strip/replace entities missing in Mini."""
from __future__ import annotations

import re
from pathlib import Path

SRC = Path(r"c:\ss14\space-station-14-fobos\Resources\Maps\Shuttles\ERT\gamma.yml")
DST = Path(r"c:\ss14\mini-station-goob\Resources\Maps\_Mini\Shuttles\ERT\gamma.yml")
MINI_PROTOS = Path(r"c:\ss14\mini-station-goob\Resources\Prototypes")

# Direct replacements for fork-specific entities -> Mini equivalents / delete
REPLACEMENTS: dict[str, str | None] = {
    "ADTCombatHypo": "Hypospray",
    "CombatHypo": "Hypospray",
    "BackmenVendingMachineSnackTeal": "VendingMachineSnack",
    "DeployableBarrierCentcomm": "DeployableBarrier",
    "CelestinMedipen": "EmergencyMedipen",
    "FilledVirusMedkit": "MedkitFilled",
    "SuperVirusMedkit": "MedkitCombatFilled",
    "BluespaceVial": "BluespaceBeaker",
    "ClothingBackpackRIGEVA": "ClothingOuterHardsuitEVA",
    "ClothingOuterHardsuitEVAPrisoner": "ClothingOuterHardsuitEVA",
    "CrateMaterialArmory": "CrateMaterialGlass",
    "SignERTber": "SignSecure",
    "WeaponEnergyTurretCentralCommandControlPanel": "ComputerAlert",
    # Prefer Mini ERT cryo spawners when markers present
    "ErtSpawnPoint": "CryogenicSleepUnitSpawnerERT",
}

# Entities we always remove (fork-only clutter / branding)
ALWAYS_DELETE_PREFIXES = (
    "DeadSpace",
    "DS",
    "Fobos",
)


def collect_mini_ids() -> set[str]:
    ids: set[str] = set()
    for yml in MINI_PROTOS.rglob("*.yml"):
        try:
            text = yml.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for m in re.finditer(r"^  id: ([^\s#]+)", text, re.M):
            ids.add(m.group(1))
    return ids


def split_entities(text: str) -> tuple[str, list[str]]:
    """Return (header before entities, list of entity YAML blocks)."""
    # Map format: meta + grids + entities: then list of - proto blocks
    marker = "\nentities:\n"
    idx = text.find(marker)
    if idx < 0:
        raise SystemExit("entities: section not found")
    header = text[: idx + len(marker)]
    body = text[idx + len(marker) :]
    # Split on lines that start with "- proto:"
    parts = re.split(r"(?m)(?=^- proto:)", body)
    entities = [p for p in parts if p.strip()]
    return header, entities


def entity_proto(block: str) -> str | None:
    m = re.match(r"^- proto: (.*)$", block, re.M)
    if not m:
        return None
    return m.group(1).strip().strip('"')


def main() -> None:
    ids = collect_mini_ids()
    text = SRC.read_text(encoding="utf-8")
    header, entities = split_entities(text)

    kept: list[str] = []
    deleted: list[str] = []
    replaced: list[tuple[str, str]] = []
    missing_deleted: list[str] = []

    for block in entities:
        proto = entity_proto(block)
        if proto is None or proto == "":
            kept.append(block)
            continue

        if any(proto.startswith(p) for p in ALWAYS_DELETE_PREFIXES):
            deleted.append(proto)
            continue

        if proto in REPLACEMENTS:
            new = REPLACEMENTS[proto]
            if new is None:
                deleted.append(proto)
                continue
            block = re.sub(r"^- proto: .*$", f"- proto: {new}", block, count=1, flags=re.M)
            replaced.append((proto, new))
            proto = new

        if proto not in ids:
            missing_deleted.append(proto)
            continue

        kept.append(block)

    out = header + "".join(kept)
    # Soft-rename map title if present
    out = out.replace("Мёртвый", "Nanotrasen")
    out = out.replace("Dead Space", "Nanotrasen")
    out = out.replace("МК", "NT")

    DST.parent.mkdir(parents=True, exist_ok=True)
    DST.write_text(out, encoding="utf-8")

    print(f"Wrote {DST}")
    print(f"kept={len(kept)} deleted={len(deleted)} replaced={len(replaced)} missing_deleted={len(set(missing_deleted))}")
    print("--- replaced ---")
    for a, b in replaced:
        print(f"  {a} -> {b}")
    print("--- unique missing deleted ---")
    for p in sorted(set(missing_deleted)):
        print(f"  {p}")
    print("--- always deleted ---")
    for p in sorted(set(deleted)):
        print(f"  {p}")


if __name__ == "__main__":
    main()
