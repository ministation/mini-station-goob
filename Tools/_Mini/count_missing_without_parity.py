import re
from pathlib import Path

PROTO = Path("Resources/Prototypes")


def strip_comments(text: str) -> str:
    out = []
    for line in text.splitlines():
        if "#" in line:
            in_q = False
            buf = []
            for ch in line:
                if ch in ("'", '"'):
                    in_q = not in_q
                if ch == "#" and not in_q:
                    break
                buf.append(ch)
            line = "".join(buf)
        out.append(line)
    return "\n".join(out)


def split_docs(text: str):
    text = strip_comments(text)
    parts = re.split(r"(?m)^(?=- type:)", text)
    return [p.strip() for p in parts if p.strip().startswith("- type:")]


def get_field(doc: str, field: str):
    if field == "type":
        m = re.search(r"(?m)^- type:\s*(.+)$", doc)
    else:
        m = re.search(rf"(?m)^\s*{re.escape(field)}:\s*(.+)$", doc)
    return None if not m else m.group(1).strip().strip("\"'")


def get_list_field(doc: str, field: str):
    m = re.search(rf"(?m)^\s*{re.escape(field)}:\s*$", doc)
    if not m:
        return []
    rest = doc[m.end() :]
    items = []
    item_indent = None
    for line in rest.splitlines():
        if not line.strip():
            continue
        lm = re.match(r"^([ \t]*)-\s+(.+)$", line)
        if not lm:
            break
        indent = len(lm.group(1).expandtabs(2))
        if item_indent is None:
            item_indent = indent
        elif indent < item_indent:
            break
        elif indent > item_indent:
            continue
        val = lm.group(2).strip().split("#", 1)[0].strip()
        if val and not val.startswith("type:"):
            items.append(val)
    return items


packs = {}
techs = {}
dyn = set()
static = set()
for path in PROTO.rglob("*.yml"):
    text = path.read_text(encoding="utf-8", errors="ignore")
    if "- type:" not in text:
        continue
    for doc in split_docs(text):
        t = get_field(doc, "type")
        if t == "latheRecipePack":
            pid = get_field(doc, "id")
            if pid:
                packs[pid] = get_list_field(doc, "recipes")
        elif t == "technology":
            tid = get_field(doc, "id")
            if tid:
                techs[tid] = get_list_field(doc, "recipeUnlocks")
        elif t == "entity":
            for f in ("dynamicPacks", "emagDynamicPacks"):
                dyn.update(get_list_field(doc, f))
            for f in ("staticPacks", "emagStaticPacks"):
                static.update(get_list_field(doc, f))

dyn.discard("MiniResearchParity")
techs.pop("MiniLatheParity", None)
packs.pop("MiniResearchParity", None)

printable = set()
for p in dyn | static:
    printable.update(packs.get(p, []))
unlocked = set()
for r in techs.values():
    unlocked.update(r)
missing = sorted(unlocked - printable)
print("still missing without MiniResearchParity:", len(missing))
for r in missing:
    print(r)
