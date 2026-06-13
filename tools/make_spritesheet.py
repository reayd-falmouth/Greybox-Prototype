"""
make_spritesheet.py
Resizes numbered poker chip PNGs to NxN and concatenates them into a
single horizontal sprite sheet.

Usage:
    python tools/make_spritesheet.py                  # all known currencies
    python tools/make_spritesheet.py --currency GBP   # one currency
    python tools/make_spritesheet.py --size 128        # 128x128 tiles
    python tools/make_spritesheet.py --dry-run         # preview without writing

Requires: pip install Pillow
"""

import argparse
from pathlib import Path
from PIL import Image

REPO_ROOT   = Path(__file__).resolve().parent.parent
SPRITES_BASE = REPO_ROOT / "Unity/Assets/Art/Sprites"

CHIP_SUFFIXES = [
    "white",
    "blue",
    "red",
    "green",
    "purple",
    "silver",
    "gold",
    "pure_gold",
    "diamond",
]


def detect_prefix(sprites_dir: Path) -> str:
    """Return the currency prefix used in filenames (may differ from dir name, e.g. KUW/KWD)."""
    matches = list(sprites_dir.glob("1_*_white.png"))
    if not matches:
        return sprites_dir.name
    return matches[0].stem.split("_")[1]


def chip_names(prefix: str):
    return [f"{i+1}_{prefix}_{s}.png" for i, s in enumerate(CHIP_SUFFIXES)]


def build_sheet(currency: str, tile: int, dry_run: bool):
    sprites_dir = SPRITES_BASE / currency
    prefix = detect_prefix(sprites_dir)
    output_path = sprites_dir / f"{prefix}_spritesheet.png"
    names = chip_names(prefix)
    paths = [sprites_dir / n for n in names]

    missing = [p for p in paths if not p.exists()]
    if missing:
        print(f"ERROR [{currency}] — missing files:")
        for p in missing:
            print(f"  {p}")
        return False

    print(f"[{currency}] tile={tile}x{tile}  sheet={tile * len(paths)}x{tile}  -> {output_path.name}")
    for i, p in enumerate(paths):
        img = Image.open(p)
        print(f"  [{i+1:02d}] {p.name:32s}  original={img.size}")

    if dry_run:
        print("  (dry run — not written)\n")
        return True

    sheet = Image.new("RGBA", (tile * len(paths), tile), (0, 0, 0, 0))
    for i, p in enumerate(paths):
        img = Image.open(p).convert("RGBA").resize((tile, tile), Image.LANCZOS)
        sheet.paste(img, (i * tile, 0))

    sheet.save(output_path, "PNG")
    print(f"  Saved: {output_path}\n")
    return True


def main():
    # Auto-detect available currency directories
    available = sorted(
        d.name for d in SPRITES_BASE.iterdir()
        if d.is_dir() and any(d.glob("1_*_white.png"))
    )

    parser = argparse.ArgumentParser(description="Build poker chip sprite sheets.")
    parser.add_argument("--currency", choices=available, help=f"One of: {available} (default: all)")
    parser.add_argument("--size", type=int, default=100, help="Tile size in pixels (default: 100)")
    parser.add_argument("--dry-run", action="store_true", help="Preview order without writing")
    args = parser.parse_args()

    targets = [args.currency] if args.currency else available

    if not targets:
        print("No chip directories found under", SPRITES_BASE)
        raise SystemExit(1)

    print(f"Currencies: {targets}\n")
    ok = all(build_sheet(c, args.size, args.dry_run) for c in targets)
    raise SystemExit(0 if ok else 1)


if __name__ == "__main__":
    main()
