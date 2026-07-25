#!/usr/bin/env python3
"""Place genetics next to medical doctors (Mini + CorvaxGoob stations).

Rules:
- SpawnPointGeneticist: free tile adjacent to SpawnPointMedicalDoctor (never GeneDrobe/fridge).
- MedicalScanner + DnaModifierConsole: reuse a nearby medbay scanner if present,
  otherwise place both on free floor next to the doctor cluster (bind range ≤4).
- Strip previous script genetics (geneticist, DNA console, doctor-offset / genetics scanners).
"""
from __future__ import annotations

import math
import re
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

MAP_ROOTS = [
    Path(r"c:\ss14\mini-station-goob\Resources\Maps\_Mini"),
]

POS_RE = re.compile(r"pos:\s*([-\d.]+),\s*([-\d.]+)")
PARENT_RE = re.compile(r"parent:\s*(\S+)")
UID_RE = re.compile(r"- uid:\s*(\d+)")

# Solid / dense — underfloor cables/pipes ignored
BLOCKING_PREFIXES = (
    "Wall",
    "Reinforced",
    "Window",
    "Windoor",
    "Airlock",
    "Door",
    "Firelock",
    "Grille",
    "Girder",
    "Railing",
    "Fence",
    "Table",
    "Rack",
    "Closet",
    "Locker",
    "Crate",
    "Machine",
    "Computer",
    "Console",
    "Vendor",
    "Vending",
    "ChemMaster",
    "ChemDispenser",
    "ReagentGrinder",
    "MedicalTechFab",
    "MedicalScanner",
    "CloningPod",
    "Cryo",
    "Sleeper",
    "Morgue",
    "Disposal",
    "SMES",
    "Substation",
    "Generator",
    "Thruster",
    "GravityGenerator",
    "Lathe",
    "Autolathe",
    "Protolathe",
    "Biogenerator",
    "OreProcessor",
    "SmartFridge",
    "Wardrobe",
    "Bed",
    "StasisBed",
    "OperatingTable",
    "DnaModifier",
)

WALL_PREFIXES = ("Wall", "ReinforcedWindow", "Window", "Grille")


@dataclass
class EntRef:
    proto: str
    uid: int
    x: float
    y: float
    parent: str
    entity_start: int
    entity_end: int


@dataclass
class ProtoBlock:
    proto: str
    entities: list[EntRef] = field(default_factory=list)


def tile_key(x: float, y: float) -> tuple[int, int]:
    return (int(math.floor(x)), int(math.floor(y)))


def is_blocking(proto: str) -> bool:
    return any(proto.startswith(p) for p in BLOCKING_PREFIXES)


def is_wall(proto: str) -> bool:
    return any(proto.startswith(p) for p in WALL_PREFIXES)


def neighbors4(t: tuple[int, int]) -> list[tuple[int, int]]:
    x, y = t
    return [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]


def parse_map(lines: list[str]) -> list[ProtoBlock]:
    blocks: list[ProtoBlock] = []
    i = 0
    n = len(lines)
    while i < n:
        m = re.match(r"- proto: (\S+)\s*$", lines[i])
        if not m:
            i += 1
            continue
        proto = m.group(1)
        i += 1
        if i >= n or not lines[i].startswith("  entities:"):
            continue
        i += 1
        ents: list[EntRef] = []
        while i < n and lines[i].startswith("  - uid:"):
            uid_m = UID_RE.search(lines[i])
            ent_start = i
            i += 1
            while i < n and not lines[i].startswith("  - uid:") and not lines[i].startswith("- proto:"):
                i += 1
            ent_end = i
            chunk = "".join(lines[ent_start:ent_end])
            pos_m = POS_RE.search(chunk)
            if not uid_m or not pos_m:
                continue
            parent_m = PARENT_RE.search(chunk)
            ents.append(
                EntRef(
                    proto=proto,
                    uid=int(uid_m.group(1)),
                    x=float(pos_m.group(1)),
                    y=float(pos_m.group(2)),
                    parent=parent_m.group(1) if parent_m else "2",
                    entity_start=ent_start,
                    entity_end=ent_end,
                )
            )
        blocks.append(ProtoBlock(proto=proto, entities=ents))
    return blocks


def build_occupancy(
    blocks: list[ProtoBlock],
) -> tuple[set[tuple[int, int]], set[tuple[int, int]], dict[str, list[EntRef]]]:
    occupied: set[tuple[int, int]] = set()
    walls: set[tuple[int, int]] = set()
    by_proto: dict[str, list[EntRef]] = defaultdict(list)
    for block in blocks:
        for ent in block.entities:
            by_proto[ent.proto].append(ent)
            t = tile_key(ent.x, ent.y)
            if is_wall(ent.proto):
                walls.add(t)
                occupied.add(t)
            elif is_blocking(ent.proto):
                occupied.add(t)
    return occupied, walls, by_proto


def make_block(proto: str, uid: int, x: float, y: float, parent: str) -> str:
    return (
        f"- proto: {proto}\n"
        f"  entities:\n"
        f"  - uid: {uid}\n"
        f"    components:\n"
        f"    - type: Transform\n"
        f"      parent: {parent}\n"
        f"      pos: {x},{y}\n"
    )


def remove_entities(lines: list[str], to_remove: list[EntRef]) -> list[str]:
    if not to_remove:
        return lines
    remove_ranges = sorted(
        {(e.entity_start, e.entity_end) for e in to_remove},
        key=lambda r: r[0],
        reverse=True,
    )
    out = list(lines)
    for estart, eend in remove_ranges:
        del out[estart:eend]
    text = "".join(out)
    text = re.sub(r"- proto: \S+\n  entities:\n(?!- proto: |  - uid:)", "", text)
    text = re.sub(r"- proto: \S+\n  entities:\n(?=- proto:)", "", text)
    return text.splitlines(keepends=True)


def free_near(
    occupied: set[tuple[int, int]],
    origin: tuple[float, float],
    radius: int = 8,
    prefer_wall: set[tuple[int, int]] | None = None,
) -> list[tuple[int, int]]:
    ox, oy = int(math.floor(origin[0])), int(math.floor(origin[1]))
    scored: list[tuple[float, tuple[int, int]]] = []
    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            t = (ox + dx, oy + dy)
            if t in occupied:
                continue
            dist = math.hypot(t[0] + 0.5 - origin[0], t[1] + 0.5 - origin[1])
            wall_bonus = -0.5 if prefer_wall and any(n in prefer_wall for n in neighbors4(t)) else 0.0
            scored.append((dist + wall_bonus, t))
    scored.sort(key=lambda x: x[0])
    return [t for _, t in scored]


def pick_adjacent_pair(tiles: list[tuple[int, int]]) -> tuple[tuple[int, int], tuple[int, int]] | None:
    free = set(tiles)
    for t in tiles:
        for n in neighbors4(t):
            if n in free:
                return t, n
    return None


def process_map(path: Path) -> str:
    raw = path.read_text(encoding="utf-8")
    lines = raw.splitlines(keepends=True)
    blocks = parse_map(lines)
    occupied, walls, by_proto = build_occupancy(blocks)

    docs = list(by_proto.get("SpawnPointMedicalDoctor", []))
    if not docs:
        return "skip-no-doctor"

    consoles = list(by_proto.get("DnaModifierConsole", []))
    gens = list(by_proto.get("SpawnPointGeneticist", []))
    scanners = list(by_proto.get("MedicalScanner", []))

    to_remove: list[EntRef] = []
    to_remove.extend(consoles)
    to_remove.extend(gens)

    # Strip genetics-script scanners. Never keep a scanner that only exists as part of
    # an old genetics cluster far from the primary doctor spawn.
    primary = docs[0]
    for scan in scanners:
        dist_primary = math.hypot(scan.x - primary.x, scan.y - primary.y)
        doctor_offset = any(
            abs(scan.x - (g.x + 1.0)) < 0.2 and abs(scan.y - g.y) < 0.2 for g in gens
        )
        near_gen = any(math.hypot(scan.x - g.x, scan.y - g.y) <= 3.5 for g in gens)
        near_cons = any(math.hypot(scan.x - c.x, scan.y - c.y) <= 2.5 for c in consoles)
        # Keep real medbay/cloning scanners close to the main doctor spawn.
        if dist_primary <= 10.0 and not doctor_offset and not (near_gen and near_cons):
            continue
        if doctor_offset or (near_gen and near_cons) or (near_cons and dist_primary > 10.0):
            to_remove.append(scan)

    seen: set[int] = set()
    uniq_remove: list[EntRef] = []
    for e in to_remove:
        if e.uid in seen:
            continue
        seen.add(e.uid)
        uniq_remove.append(e)

    for e in uniq_remove:
        occupied.discard(tile_key(e.x, e.y))

    new_lines = remove_entities(lines, uniq_remove)
    blocks2 = parse_map(new_lines)
    occupied2, walls2, by_proto2 = build_occupancy(blocks2)

    docs2 = list(by_proto2.get("SpawnPointMedicalDoctor", []))
    if not docs2:
        return f"fail-no-doctor-after-remove removed={len(uniq_remove)}"

    # Primary doctor = first listed (same as old spawn script).
    doctor = docs2[0]
    parent = doctor.parent
    doc_tile = tile_key(doctor.x, doctor.y)
    # Spawn points are not solid, but we must not stack machines on them.
    occupied2.add(doc_tile)
    for d in docs2:
        occupied2.add(tile_key(d.x, d.y))

    # --- Geneticist: adjacent free tile next to doctor (never on the doctor tile) ---
    gen_t = None
    for n in neighbors4(doc_tile):
        if n not in occupied2:
            gen_t = n
            break
    if gen_t is None:
        near = [
            t
            for t in free_near(occupied2, (doctor.x, doctor.y), radius=4)
            if t != doc_tile
        ]
        gen_t = near[0] if near else (doc_tile[0] + 1, doc_tile[1])
    occupied2.add(gen_t)

    # --- Scanner: reuse only if close to the primary doctor; else place new nearby ---
    remaining_scanners = list(by_proto2.get("MedicalScanner", []))
    reuse = None
    best_d = float("inf")
    for scan in remaining_scanners:
        d = math.hypot(scan.x - doctor.x, scan.y - doctor.y)
        if d <= 10.0 and d < best_d:
            best_d = d
            reuse = scan

    max_uid = max((int(m.group(1)) for line in new_lines if (m := UID_RE.search(line))), default=100000)
    u = max_uid

    if reuse is not None:
        scan_desc = f"reuse@{reuse.x},{reuse.y}"
        at = tile_key(reuse.x, reuse.y)
        cons_t = None
        for n in neighbors4(at):
            if n not in occupied2:
                cons_t = n
                break
        if cons_t is None:
            for t in free_near(occupied2, (reuse.x, reuse.y), radius=3, prefer_wall=walls2):
                if max(abs(t[0] - at[0]), abs(t[1] - at[1])) <= 3:
                    cons_t = t
                    break
        if cons_t is None:
            return f"fail-no-console-near-scanner@{reuse.x},{reuse.y}"
        occupied2.add(cons_t)
        u += 1
        gen_uid = u
        u += 1
        cons_uid = u
        insertion = (
            make_block("SpawnPointGeneticist", gen_uid, gen_t[0] + 0.5, gen_t[1] + 0.5, parent)
            + make_block("DnaModifierConsole", cons_uid, cons_t[0] + 0.5, cons_t[1] + 0.5, parent)
        )
        cons_desc = f"({cons_t[0] + 0.5},{cons_t[1] + 0.5})"
    else:
        # Place scanner+console as adjacent free tiles near doctor
        candidates = free_near(occupied2, (doctor.x, doctor.y), radius=8, prefer_wall=walls2)
        pair = pick_adjacent_pair(candidates)
        if pair is None and len(candidates) >= 2:
            pair = (candidates[0], candidates[1])
        if pair is None:
            # Force next to geneticist / doctor
            scan_t = (gen_t[0] + 1, gen_t[1])
            cons_t = (gen_t[0] + 2, gen_t[1])
        else:
            scan_t, cons_t = pair
        occupied2.add(scan_t)
        occupied2.add(cons_t)
        u += 1
        gen_uid = u
        u += 1
        scan_uid = u
        u += 1
        cons_uid = u
        insertion = (
            make_block("SpawnPointGeneticist", gen_uid, gen_t[0] + 0.5, gen_t[1] + 0.5, parent)
            + make_block("MedicalScanner", scan_uid, scan_t[0] + 0.5, scan_t[1] + 0.5, parent)
            + make_block("DnaModifierConsole", cons_uid, cons_t[0] + 0.5, cons_t[1] + 0.5, parent)
        )
        scan_desc = f"({scan_t[0] + 0.5},{scan_t[1] + 0.5})"
        cons_desc = f"({cons_t[0] + 0.5},{cons_t[1] + 0.5})"

    text = "".join(new_lines)
    doc_m = re.search(
        r"- proto: SpawnPointMedicalDoctor\n  entities:\n(?:  - uid:.*\n(?:    .*\n)*)*",
        text,
    )
    insert_at = doc_m.end() if doc_m else len(text)
    path.write_text(text[:insert_at] + insertion + text[insert_at:], encoding="utf-8", newline="\n")
    return (
        f"ok removed={len(uniq_remove)} doctor@{doctor.x},{doctor.y} "
        f"gen=({gen_t[0] + 0.5},{gen_t[1] + 0.5}) scan={scan_desc} cons={cons_desc}"
    )


def strip_mtb() -> None:
    path = Path(r"c:\ss14\mini-station-goob\Resources\Maps\_Mini\Events\MTB.yml")
    if not path.exists():
        return
    text = path.read_text(encoding="utf-8")
    for proto in ("SpawnPointGeneticist", "DnaModifierConsole"):
        text = re.sub(
            rf"- proto: {proto}\n  entities:\n(?:  - uid:.*\n(?:    .*\n)*)*",
            "",
            text,
        )
    path.write_text(text, encoding="utf-8", newline="\n")
    print("Events/MTB.yml: stripped genetics spawns/consoles")


def main() -> None:
    strip_mtb()
    ok = fail = skip = 0
    for root in MAP_ROOTS:
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.yml")):
            if any(p in path.parts for p in ("Shuttles", "CentComm", "Events", "Bitrun")):
                continue
            text0 = path.read_text(encoding="utf-8")
            if "SpawnPointMedicalDoctor" not in text0:
                continue
            try:
                result = process_map(path)
            except Exception as exc:  # noqa: BLE001
                result = f"fail-exception {exc}"
            rel = path.as_posix().split("/Maps/")[-1]
            print(f"{rel}: {result}")
            if result.startswith("ok"):
                ok += 1
            elif result.startswith("skip"):
                skip += 1
            else:
                fail += 1
    print(f"--- done ok={ok} fail={fail} skip={skip}")


if __name__ == "__main__":
    main()
