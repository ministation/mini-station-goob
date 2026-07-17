#!/usr/bin/env python3
"""Add Typan AccessReader overrides to Typan vending machines.

Looks up each Typan vendor's parent NT prototype, maps its NT access groups to
Typan access tags, and inserts/updates the AccessReader in the Mini file.
"""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MINI = ROOT / "Resources" / "Prototypes" / "_Mini" / "Entities" / "Structures" / "Machines" / "vending_machines.yml"
NT_SOURCES = [
    ROOT / "Resources" / "Prototypes" / "Entities" / "Structures" / "Machines" / "vending_machines.yml",
    ROOT / "Resources" / "Prototypes" / "_Goobstation" / "Entities" / "Structures" / "Machines" / "vending_machines.yml",
    ROOT / "Resources" / "Prototypes" / "_DeltaV" / "Entities" / "Structures" / "Machines" / "vending_machines.yml",
]

NT_TO_TYPAN = {
    "Medical": "TypanMedical",
    "Chemistry": "TypanMedical",
    "Engineering": "TypanEngineering",
    "Atmospherics": "TypanAtmospherics",
    "Security": "TypanProtection",
    "Armory": "TypanArmory",
    "Research": "TypanScience",
    "Cargo": "TypanCargo",
    "Salvage": "TypanCargo",
    "Hydroponics": "TypanService",
    "Kitchen": "TypanService",
    "Bar": "TypanService",
    "Service": "TypanService",
    "Janitor": "TypanService",
    "Theatre": "TypanService",
    "Chapel": "TypanService",
    "Lawyer": "TypanService",
    "Detective": "TypanService",
    "HeadOfPersonnel": "TypanCommand",
    "CentralCommand": "TypanCommand",
    "Command": "TypanCommand",
}


def parse_entities(text: str) -> dict[str, str]:
    """Return id -> block text for each '- type: entity' block."""
    blocks = re.split(r"(?=^- type: entity$)", text, flags=re.M)
    out = {}
    for block in blocks:
        m = re.search(r"^\s{2}id:\s*(\S+)", block, re.M)
        if m:
            out[m.group(1)] = block
    return out


def main() -> int:
    nt_entities: dict[str, str] = {}
    for src in NT_SOURCES:
        if src.exists():
            nt_entities.update(parse_entities(src.read_text(encoding="utf-8")))

    text = MINI.read_text(encoding="utf-8")
    parts = re.split(r"(?=^- type: entity$)", text, flags=re.M)
    changed = 0
    skipped: list[str] = []

    for i, block in enumerate(parts):
        m_id = re.search(r"^\s{2}id:\s*(\S+)", block, re.M)
        m_parent = re.search(r"^\s{2}parent:\s*(\S+)", block, re.M)
        if not m_id or not m_parent:
            continue
        ent_id = m_id.group(1)
        parent = m_parent.group(1)

        if "- type: AccessReader" in block:
            continue  # already has explicit Typan access

        parent_block = nt_entities.get(parent)
        if not parent_block:
            continue
        m_access = re.search(
            r"- type: AccessReader\n\s+access:\s*(\[\[.*?\]\])", parent_block
        )
        if not m_access:
            continue

        groups = re.findall(r'"(\w+)"', m_access.group(1))
        typan_tags: list[str] = []
        unmapped = False
        for g in groups:
            if g in ("SyndicateAgent", "NuclearOperative"):
                # already fits Typan crew IDs
                typan_tags = []
                unmapped = True
                break
            mapped = NT_TO_TYPAN.get(g)
            if mapped is None:
                unmapped = True
                skipped.append(f"{ent_id}: unmapped access {g}")
                break
            if mapped not in typan_tags:
                typan_tags.append(mapped)
        if unmapped or not typan_tags:
            continue

        access_yaml = ", ".join(f'["{t}"]' for t in typan_tags)
        insert = f"  - type: AccessReader\n    access: [{access_yaml}]\n"
        # Insert right after the Sprite component block (before PointLight if present).
        m_pl = re.search(r"^  - type: PointLight\n", block, re.M)
        if m_pl:
            pos = m_pl.start()
            block = block[:pos] + insert + block[pos:]
        else:
            block = block.rstrip("\n") + "\n" + insert
        parts[i] = block
        changed += 1
        print(f"{ent_id}: access [{access_yaml}] (parent {parent})")

    MINI.write_text("".join(parts), encoding="utf-8")
    print(f"\nUpdated {changed} vendors")
    for s in skipped:
        print("SKIP", s)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
