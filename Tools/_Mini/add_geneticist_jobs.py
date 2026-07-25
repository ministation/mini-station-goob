#!/usr/bin/env python3
"""Insert Geneticist into availableJobs next to MedicalDoctor in gameMap prototypes."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"c:\ss14\mini-station-goob\Resources\Prototypes")

# Match MedicalDoctor job line and insert Geneticist after it if missing nearby.
DOC_LINE = re.compile(
    r"^([ \t]*)MedicalDoctor:\s*(\[[^\]]+\])\s*$",
    re.MULTILINE,
)


def process_file(path: Path) -> bool:
    text = path.read_text(encoding="utf-8")
    if "MedicalDoctor:" not in text:
        return False

    changed = False

    def repl(m: re.Match[str]) -> str:
        nonlocal changed
        indent, slots = m.group(1), m.group(2)
        # Look ahead in original: if next non-empty after this match already Geneticist, skip
        # We'll check context after building - simpler: if Geneticist already in file near this station, still OK to add per MedicalDoctor occurrence
        after_start = m.end()
        # Peek next few lines
        rest = text[after_start:after_start + 80]
        if re.match(r"\s*Geneticist:", rest):
            return m.group(0)
        changed = True
        # Preserve spacing style from slots (with/without spaces inside brackets)
        return f"{indent}MedicalDoctor: {slots}\n{indent}Geneticist: [ 1, 1 ]"

    new_text = DOC_LINE.sub(repl, text)
    if not changed:
        return False

    # Don't touch commented-out MedicalDoctor lines
    # DOC_LINE only matches uncommented lines (^ without #)
    path.write_text(new_text, encoding="utf-8", newline="\n")
    return True


def main() -> None:
    updated = []
    for path in sorted(ROOT.rglob("*.yml")):
        # Focus on map/station job configs
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        if "/Maps/" not in f"/{rel}" and "Maps" not in path.parts:
            # Also allow Stations under Maps
            if "Maps" not in path.parts:
                continue
        if process_file(path):
            updated.append(rel)
            print(f"updated: {rel}")
    print(f"done, {len(updated)} files")


if __name__ == "__main__":
    main()
