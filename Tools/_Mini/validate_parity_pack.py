import re
from pathlib import Path

recipe_ids = set()
abstract_ids = set()
PROTO = Path("Resources/Prototypes")
for path in PROTO.rglob("*.yml"):
    text = path.read_text(encoding="utf-8", errors="ignore")
    if "latheRecipe" not in text:
        continue
    parts = re.split(r"(?m)^(?=- type:)", text)
    for doc in parts:
        if not doc.startswith("- type:"):
            continue
        lines = []
        for line in doc.splitlines():
            if "#" in line:
                q = False
                out = []
                for ch in line:
                    if ch in "\"'":
                        q = not q
                    if ch == "#" and not q:
                        break
                    out.append(ch)
                line = "".join(out)
            lines.append(line)
        doc = "\n".join(lines)
        m = re.match(r"- type:\s*(\S+)", doc)
        if not m or m.group(1) != "latheRecipe":
            continue
        im = re.search(r"(?m)^\s*id:\s*(\S+)", doc)
        if not im:
            continue
        rid = im.group(1)
        recipe_ids.add(rid)
        if re.search(r"(?m)^\s*abstract:\s*true\s*$", doc):
            abstract_ids.add(rid)

pack = Path("Resources/Prototypes/_Mini/Recipes/Lathes/Packs/research_parity.yml").read_text(
    encoding="utf-8"
)
needed = re.findall(r"(?m)^  - (\S+)", pack)
print("latheRecipes", len(recipe_ids), "abstract", len(abstract_ids))
missing = [r for r in needed if r not in recipe_ids]
abstract = [r for r in needed if r in abstract_ids]
print("pack", len(needed), "missing", len(missing), "abstract_in_pack", len(abstract))
for r in missing[:50]:
    print(" MISSING", r)
for r in abstract[:20]:
    print(" ABSTRACT", r)
