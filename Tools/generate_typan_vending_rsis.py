#!/usr/bin/env python3
"""Generate black/red Typan vending RSI variants from NT VendingMachines sprites.

Copies RSI structure (meta.json, frame packing, directions, delays, alpha) and
recolors RGB with a luminance-preserving Typan palette transfer.
"""

from __future__ import annotations

import argparse
import json
import math
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC_DIR = ROOT / "Resources" / "Textures" / "Structures" / "Machines" / "VendingMachines"
DST_DIR = ROOT / "Resources" / "Textures" / "_Mini" / "Structures" / "Machines" / "VendingMachines"
MANIFEST_PATH = ROOT / "Tools" / "typan_vending_palette.json"

# Skip templates / already-syndie skins / non-machine placeholders.
DEFAULT_SKIP = {
    "empty.rsi",
    "syndiedrobe.rsi",
    "bruiseomat.rsi",
    "random.rsi",
}


def srgb_to_linear(c: float) -> float:
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c: float) -> int:
    c = max(0.0, min(1.0, c))
    if c <= 0.0031308:
        v = c * 12.92
    else:
        v = 1.055 * (c ** (1.0 / 2.4)) - 0.055
    return int(round(v * 255.0))


def rgb_to_oklab(r: int, g: int, b: int) -> tuple[float, float, float]:
    lr, lg, lb = srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b)
    l = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb
    m = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb
    s = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb
    l_ = l ** (1.0 / 3.0)
    m_ = m ** (1.0 / 3.0)
    s_ = s ** (1.0 / 3.0)
    L = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_
    a = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_
    b2 = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_
    return L, a, b2


def oklab_to_rgb(L: float, a: float, b: float) -> tuple[int, int, int]:
    l_ = L + 0.3963377774 * a + 0.2158037573 * b
    m_ = L - 0.1055613458 * a - 0.0638541728 * b
    s_ = L - 0.0894841775 * a - 1.2914855480 * b
    l = l_ * l_ * l_
    m = m_ * m_ * m_
    s = s_ * s_ * s_
    lr = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s
    lg = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s
    lb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s
    return linear_to_srgb(lr), linear_to_srgb(lg), linear_to_srgb(lb)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def clamp01(x: float) -> float:
    return max(0.0, min(1.0, x))


def typan_target(L: float, chroma: float, hue_deg: float, is_unshaded: bool) -> tuple[int, int, int]:
    """Map source OKLab into Typan black chassis + red accents (boozeomat-style).

    Shaded chassis collapses to near-black / dark gray regardless of source paint
    color. Only red-family source pixels and unshaded emissives become red.
    """
    # Unshaded overlays: keep readable emissives in the red family.
    if is_unshaded:
        if chroma < 0.02 and L < 0.2:
            return oklab_to_rgb(L * 0.7, 0.0, 0.0)
        if chroma < 0.02 and L > 0.85:
            # bright UI glyphs → pale red/white
            return (238, 200, 200)
        # Colored or lit pixels → red emissive
        target_c = 0.14 if chroma > 0.03 else 0.08
        L2 = clamp01(lerp(L, 0.55, 0.25) if L > 0.4 else L * 0.9)
        return oklab_to_rgb(L2, target_c * 0.95, target_c * 0.3)

    # Shaded body: syndicate black/gray chassis (boozeomat-style).
    def charcoal(lum: float) -> tuple[int, int, int]:
        if lum < 0.2:
            return (19, 21, 20)
        if lum < 0.4:
            return (34, 30, 30)
        if lum < 0.6:
            return (50, 46, 46)
        if lum < 0.8:
            return (72, 70, 70)
        return (127, 127, 127)

    if chroma < 0.04:
        return charcoal(L)

    # Red-family source pixels keep the dark-red trim treatment that made the
    # boozeomat look right.
    if hue_deg >= 315.0 or hue_deg <= 50.0:
        if L > 0.55:
            L2 = clamp01(L * 0.35)
            target_c = 0.05
        elif L > 0.35:
            L2 = clamp01(L * 0.45)
            target_c = 0.06
        else:
            L2 = clamp01(L * 0.55)
            target_c = 0.045
        return oklab_to_rgb(L2, target_c * 0.9, target_c * 0.25)

    # Any other chassis paint color (olive, blue, green, yellow bodies) becomes
    # plain charcoal, slightly darkened so panel lines keep contrast.
    return charcoal(L * 0.9)


def recolor_rgba(img: Image.Image, is_unshaded: bool) -> Image.Image:
    src = img.convert("RGBA")
    # Pillow 10+: get_flattened_data; older: getdata.
    raw = src.get_flattened_data() if hasattr(src, "get_flattened_data") else src.getdata()
    data = list(raw)
    cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    out = []
    for r, g, b, a in data:
        if a == 0:
            out.append((r, g, b, a))
            continue
        key = (r, g, b)
        mapped = cache.get(key)
        if mapped is None:
            L, oa, ob = rgb_to_oklab(r, g, b)
            hue_deg = math.degrees(math.atan2(ob, oa)) % 360.0
            mapped = typan_target(L, math.hypot(oa, ob), hue_deg, is_unshaded)
            cache[key] = mapped
        nr, ng, nb = mapped
        out.append((nr, ng, nb, a))
    src.putdata(out)
    return src


def load_manifest() -> dict:
    if MANIFEST_PATH.exists():
        return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return {
        "skip": sorted(DEFAULT_SKIP),
        "notes": "Auto-generated Typan vending recolor manifest",
        "overrides": {
            # Medical Typan historically pointed at medical.rsi; NT medical uses medivend.
            "medical.rsi": {"source": "medivend.rsi", "enabled": False},
        },
    }


def save_manifest(manifest: dict) -> None:
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def validate_rsi(src: Path, dst: Path) -> list[str]:
    errors: list[str] = []
    src_meta = json.loads((src / "meta.json").read_text(encoding="utf-8"))
    dst_meta = json.loads((dst / "meta.json").read_text(encoding="utf-8"))
    # Structure must match except copyright note.
    src_states = src_meta.get("states", [])
    dst_states = dst_meta.get("states", [])
    if src_states != dst_states:
        errors.append(f"{dst.name}: states mismatch vs source")
    if src_meta.get("size") != dst_meta.get("size"):
        errors.append(f"{dst.name}: size mismatch")

    for png in src.glob("*.png"):
        out = dst / png.name
        if not out.exists():
            errors.append(f"{dst.name}: missing {png.name}")
            continue
        a = Image.open(png).convert("RGBA")
        b = Image.open(out).convert("RGBA")
        if a.size != b.size:
            errors.append(f"{dst.name}/{png.name}: size {a.size} -> {b.size}")
            continue
        # Alpha mask must be identical.
        aa = a.split()[3].tobytes()
        bb = b.split()[3].tobytes()
        if aa != bb:
            errors.append(f"{dst.name}/{png.name}: alpha mask changed")
    return errors


def process_rsi(name: str, source_name: str | None = None) -> list[str]:
    src_name = source_name or name
    src = SRC_DIR / src_name
    dst = DST_DIR / name
    if not src.is_dir():
        return [f"missing source RSI: {src_name}"]

    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True, exist_ok=True)

    meta = json.loads((src / "meta.json").read_text(encoding="utf-8"))
    copyright = meta.get("copyright", "")
    meta["copyright"] = (
        f"{copyright} Recolored to Typan black/red palette for Mini Station "
        f"(auto-generated by Tools/generate_typan_vending_rsis.py)."
    ).strip()
    (dst / "meta.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")

    for png in sorted(src.glob("*.png")):
        is_unshaded = "unshaded" in png.name.lower()
        img = Image.open(png)
        out = recolor_rgba(img, is_unshaded=is_unshaded)
        # Deterministic PNG write.
        out.save(dst / png.name, format="PNG", optimize=False, compress_level=6)

    return validate_rsi(src, dst)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="*", help="Process only these RSI names (with .rsi)")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    manifest = load_manifest()
    skip = set(manifest.get("skip", [])) | DEFAULT_SKIP
    overrides = manifest.get("overrides", {})

    rsi_names = sorted(p.name for p in SRC_DIR.iterdir() if p.is_dir() and p.name.endswith(".rsi"))
    if args.only:
        rsi_names = [n if n.endswith(".rsi") else f"{n}.rsi" for n in args.only]

    selected: list[tuple[str, str]] = []
    for name in rsi_names:
        if name in skip:
            continue
        ov = overrides.get(name, {})
        if ov.get("enabled") is False:
            continue
        source = ov.get("source", name)
        selected.append((name, source))

    # Ensure medical alias is generated as medivend output name used by prototypes.
    # Also always generate medivend.rsi if present.
    print(f"Will process {len(selected)} RSI into {DST_DIR}")
    if args.dry_run:
        for name, source in selected:
            print(f"  {name} <= {source}")
        save_manifest(manifest)
        return 0

    DST_DIR.mkdir(parents=True, exist_ok=True)
    all_errors: list[str] = []
    for name, source in selected:
        print(f"Recoloring {source} -> {name}")
        errs = process_rsi(name, source_name=source)
        if errs:
            print("  ERRORS:")
            for e in errs:
                print("   -", e)
            all_errors.extend(errs)
        else:
            print("  ok")

    # Keep legacy medical.rsi name as a copy of medivend for old refs.
    medivend_dst = DST_DIR / "medivend.rsi"
    medical_dst = DST_DIR / "medical.rsi"
    if medivend_dst.exists():
        if medical_dst.exists():
            shutil.rmtree(medical_dst)
        shutil.copytree(medivend_dst, medical_dst)
        print("Mirrored medivend.rsi -> medical.rsi for legacy Typan refs")

    save_manifest(manifest)
    if all_errors:
        print(f"\nCompleted with {len(all_errors)} validation error(s)", file=sys.stderr)
        return 1
    print("\nDone.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
