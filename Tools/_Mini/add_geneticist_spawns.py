#!/usr/bin/env python3
"""Add SpawnPointGeneticist (+ scanner/console if missing nearby setup) next to MedicalDoctor on ALL maps."""
from __future__ import annotations

import re
from pathlib import Path

MAPS_ROOT = Path(r"c:\ss14\mini-station-goob\Resources\Maps")

DOCTOR_BLOCK = re.compile(
    r"- proto: SpawnPointMedicalDoctor\n"
    r"  entities:\n"
    r"((?:  - uid: \d+\n"
    r"    components:\n"
    r"    - type: Transform\n"
    r"(?:      parent: .+\n)?"
    r"      pos: [^\n]+\n"
    r"(?:      parent: .+\n)?)*)",
    re.MULTILINE,
)

POS_RE = re.compile(r"pos:\s*([-\d.]+),\s*([-\d.]+)")
PARENT_RE = re.compile(r"parent:\s*(.+)")
UID_RE = re.compile(r"- uid:\s*(\d+)")


def find_max_uid(text: str) -> int:
    return max((int(m.group(1)) for m in UID_RE.finditer(text)), default=100000)


def parse_first_doctor(block: str) -> tuple[str, str, str] | None:
    first = re.search(
        r"- uid: \d+\n"
        r"    components:\n"
        r"    - type: Transform\n"
        r"((?:      .+\n)+)",
        block,
    )
    if not first:
        return None
    transform = first.group(1)
    pos = POS_RE.search(transform)
    if not pos:
        return None
    parent = PARENT_RE.search(transform)
    parent_line = f"      parent: {parent.group(1).strip()}\n" if parent else ""
    return pos.group(1), pos.group(2), parent_line


def make_proto_block(proto: str, uid: int, x: float, y: float, parent_line: str) -> str:
    return (
        f"- proto: {proto}\n"
        f"  entities:\n"
        f"  - uid: {uid}\n"
        f"    components:\n"
        f"    - type: Transform\n"
        f"{parent_line}"
        f"      pos: {x},{y}\n"
    )


def process_map(path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    if "SpawnPointMedicalDoctor" not in text:
        return "skip-no-doctor"

    if "SpawnPointGeneticist" in text:
        # Already has geneticist spawn; still ensure scanner+console if both missing near genetics?
        return "skip-has-geneticist"

    match = DOCTOR_BLOCK.search(text)
    if not match:
        return "fail-parse"

    parsed = parse_first_doctor(match.group(0))
    if not parsed:
        return "fail-pos"

    x_s, y_s, parent_line = parsed
    x, y = float(x_s), float(y_s)
    next_uid = find_max_uid(text) + 1

    blocks = [
        make_proto_block("SpawnPointGeneticist", next_uid, x + 1.0, y, parent_line),
        make_proto_block("MedicalScanner", next_uid + 1, x + 2.0, y, parent_line),
        make_proto_block("DnaModifierConsole", next_uid + 2, x + 3.0, y, parent_line),
    ]

    insert_at = match.end()
    insertion = "".join(blocks)
    path.write_text(text[:insert_at] + insertion + text[insert_at:], encoding="utf-8", newline="\n")
    return f"ok uids={next_uid}-{next_uid+2} @({x+1},{y})"


def main() -> None:
    counts: dict[str, int] = {}
    for path in sorted(MAPS_ROOT.rglob("*.yml")):
        # Skip shuttles/centcomm/dungeons unless they have doctors (script already checks)
        result = process_map(path)
        key = result.split()[0]
        counts[key] = counts.get(key, 0) + 1
        if result.startswith("ok") or result.startswith("fail"):
            print(f"{path.relative_to(MAPS_ROOT)}: {result}")
    print("---")
    for k, v in sorted(counts.items()):
        print(f"{k}: {v}")


if __name__ == "__main__":
    main()
