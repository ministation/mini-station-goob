#!/usr/bin/env python3
from pathlib import Path

path = Path(r"c:/ss14/mini-station-goob/Resources/Maps/_Mini/Shuttles/ERT/blue.yml")
text = path.read_text(encoding="utf-8")

old_meta = """meta:
  format: 7
  category: Map
  engineVersion: 283.1.0
  forkId: \"\"
  forkVersion: \"\"
  time: 07/25/2026 21:58:33
  entityCount: 507
maps:
- 324
grids:
- 1
orphans: []
nullspace: []"""

# Fix: the above has escaped quotes in source that become " in string - good actually
# Wait in this write tool I need literal quotes in the yaml

old_meta = (
    "meta:\n"
    "  format: 7\n"
    "  category: Map\n"
    "  engineVersion: 283.1.0\n"
    '  forkId: ""\n'
    '  forkVersion: ""\n'
    "  time: 07/25/2026 21:58:33\n"
    "  entityCount: 507\n"
    "maps:\n"
    "- 324\n"
    "grids:\n"
    "- 1\n"
    "orphans: []\n"
    "nullspace: []"
)

new_meta = (
    "meta:\n"
    "  format: 7\n"
    "  category: Grid\n"
    "  engineVersion: 283.1.0\n"
    '  forkId: ""\n'
    '  forkVersion: ""\n'
    "  time: 07/26/2026 02:00:00\n"
    "  entityCount: 506\n"
    "maps: []\n"
    "grids:\n"
    "- 1\n"
    "orphans:\n"
    "- 1\n"
    "nullspace: []"
)

if old_meta not in text:
    raise SystemExit("meta header not found exactly; aborting")

text = text.replace(old_meta, new_meta, 1)
text = text.replace(
    "    - type: Transform\n      parent: 324\n",
    "    - type: Transform\n      parent: invalid\n",
    1,
)

map_entity = (
    "\n"
    "  - uid: 324\n"
    "    components:\n"
    "    - type: MetaData\n"
    "      name: Map Entity\n"
    "    - type: Transform\n"
    "    - type: Map\n"
    "      mapPaused: True\n"
    "    - type: GridTree\n"
    "    - type: Broadphase\n"
    "    - type: OccluderTree\n"
)

if map_entity not in text:
    raise SystemExit("map entity block not found; aborting")

text = text.replace(map_entity, "\n", 1)
path.write_text(text, encoding="utf-8")

print("ok")
print("parent: 324", text.count("parent: 324"))
print("uid: 324", text.count("uid: 324"))
print("category Grid", text.startswith("meta:\n  format: 7\n  category: Grid"))
