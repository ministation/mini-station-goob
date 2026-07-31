# Converts MachineBoard Manipulator stackRequirements -> Servo partRequirements (Orion MachineParts port).
from pathlib import Path
import re

ROOT = Path("Resources/Prototypes")


def convert_board_block(block: str) -> tuple[str, int]:
    manips = list(re.finditer(r"^([ \t]+)Manipulator:\s*(\d+)[ \t]*(#.*)?$", block, re.M))
    if not manips:
        return block, 0

    changed = len(manips)
    total = sum(int(m.group(2)) for m in manips)
    child_indent = manips[0].group(1)
    parent_indent = child_indent[:-2] if len(child_indent) >= 2 else ""

    block2 = re.sub(r"^[ \t]+Manipulator:\s*\d+[ \t]*(#.*)?\n?", "", block, flags=re.M)

    # Remove empty stackRequirements keys
    block2 = re.sub(
        r"^([ \t]+)stackRequirements:[ \t]*(#.*)?\n(?=(?:[ \t]*#.*\n|[ \t]*\n)*(?:^[ \t]+\w|\Z))",
        lambda m: ""
        if not re.search(
            rf"^{re.escape(m.group(1))}  \w+:",
            block2[m.end() : m.end() + 200],
            re.M,
        )
        else m.group(0),
        block2,
        flags=re.M,
    )
    # Simpler empty-stack cleanup: stackRequirements followed only by comments/blank until next sibling key
    def strip_empty_stack(text: str) -> str:
        out_lines = []
        lines = text.splitlines(keepends=True)
        i = 0
        while i < len(lines):
            sm = re.match(r"^([ \t]+)stackRequirements:[ \t]*(#.*)?$", lines[i])
            if not sm:
                out_lines.append(lines[i])
                i += 1
                continue
            indent = sm.group(1)
            j = i + 1
            has_child = False
            while j < len(lines):
                if lines[j].strip() == "" or lines[j].strip().startswith("#"):
                    j += 1
                    continue
                cm = re.match(r"^([ \t]+)\S", lines[j])
                if cm and len(cm.group(1)) > len(indent):
                    has_child = True
                break
            if has_child:
                out_lines.append(lines[i])
                i += 1
            else:
                # drop empty stackRequirements
                i += 1
        return "".join(out_lines)

    block2 = strip_empty_stack(block2)

    insertion = (
        f"{parent_indent}partRequirements: # Mini: Orion MachineParts\n"
        f"{child_indent}Servo: {total} # Mini: Orion Manipulator->Servo\n"
    )

    if re.search(r"^[ \t]+partRequirements:", block2, re.M):
        if not re.search(r"^[ \t]+Servo:", block2, re.M):
            block2 = re.sub(
                r"^([ \t]+)partRequirements:[ \t]*(#.*)?$",
                rf"\1partRequirements: # Mini: Orion MachineParts\n{child_indent}Servo: {total} # Mini: Orion Manipulator->Servo",
                block2,
                count=1,
                flags=re.M,
            )
    elif re.search(r"^[ \t]+prototype:", block2, re.M):
        block2 = re.sub(
            r"^([ \t]+prototype:.*)$",
            rf"\1\n{insertion.rstrip()}",
            block2,
            count=1,
            flags=re.M,
        )
    else:
        block2 = re.sub(
            r"^([ \t]*- type: MachineBoard)\s*$",
            rf"\1\n{insertion.rstrip()}",
            block2,
            count=1,
            flags=re.M,
        )

    return block2, changed


def convert_file(path: Path) -> int:
    text = path.read_text(encoding="utf-8")
    if "Manipulator:" not in text or "MachineBoard" not in text:
        return 0

    pattern = re.compile(
        r"^([ \t]*)- type: MachineBoard\b.*?(?=^[ \t]*- type: |\Z)",
        re.M | re.S,
    )

    total_changed = 0

    def repl(match: re.Match) -> str:
        nonlocal total_changed
        new_block, c = convert_board_block(match.group(0))
        total_changed += c
        return new_block

    new_text = pattern.sub(repl, text)
    if total_changed:
        path.write_text(new_text, encoding="utf-8")
    return total_changed


def main() -> None:
    total = 0
    files_changed = []
    for f in ROOT.rglob("*.yml"):
        c = convert_file(f)
        if c:
            total += c
            files_changed.append((c, str(f)))

    print(f"Total Manipulator->Servo conversions: {total}")
    for c, p in sorted(files_changed, reverse=True):
        print(f"  {c}: {p}")

    rem = 0
    rem_files = []
    for f in ROOT.rglob("*.yml"):
        t = f.read_text(encoding="utf-8")
        if "MachineBoard" not in t:
            continue
        n = len(re.findall(r"Manipulator:\s*\d+", t))
        if n:
            rem += n
            rem_files.append((n, str(f)))
    print(f"Remaining Manipulator near MachineBoard files: {rem}")
    for n, p in rem_files:
        print(f"  leftover {n}: {p}")


if __name__ == "__main__":
    main()
