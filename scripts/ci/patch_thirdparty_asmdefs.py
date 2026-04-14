#!/usr/bin/env python3
"""
After S3 third-party assets are extracted into Unity/Assets/3rdParty, vendor asmdefs
often omit package references (e.g. Cinemachine). Unity batchmode then fails with CS0246.

This script:
  1) Adds com.unity.cinemachine@2.x Runtime asmdef GUID to every .asmdef under 3rdParty.
  2) Locates EngineCore (Backgammon AI) by asmdef name or EngineCore.asmdef.meta and adds
     its GUID to RMC.MyProject.Runtime.asmdef if missing.

Safe no-op when 3rdParty is absent (local dev without vendor tree).
"""
from __future__ import annotations

import json
import os
import sys

# Runtime/com.unity.cinemachine.asmdef.meta from com.unity.cinemachine@2.10.5
CINEMACHINE_RUNTIME_GUID = "4307f53044263cf4b835bd812fc161a4"

ASSETS = os.path.join("Unity", "Assets")
THIRDPARTY = os.path.join(ASSETS, "3rdParty")
RUNTIME_ASMDEF = os.path.join(ASSETS, "Scripts", "Runtime", "RMC", "RMC.MyProject.Runtime.asmdef")


def read_guid_from_meta(meta_path: str) -> str | None:
    if not os.path.isfile(meta_path):
        return None
    with open(meta_path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None


def load_json(path: str) -> dict:
    # Vendor asmdefs may be saved with a UTF-8 BOM
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def save_json(path: str, data: dict) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=4)
        f.write("\n")


def normalize_guid_ref(g: str) -> str:
    """Unity asmdef references use GUID:xxxxxxxx (32 hex)."""
    g = g.strip()
    if g.startswith("GUID:"):
        return g
    return f"GUID:{g}"


def merge_refs(data: dict, guids: list[str]) -> bool:
    refs = list(data.get("references") or [])
    # Normalize for duplicate detection (raw vs GUID: prefix)
    seen = set()
    for r in refs:
        r = r.strip()
        if r.startswith("GUID:"):
            seen.add(r)
            seen.add(r[5:])
        else:
            seen.add(r)
            seen.add(normalize_guid_ref(r))

    changed = False
    for g in guids:
        g = g.strip()
        canon = normalize_guid_ref(g)
        if g in seen or canon in seen or (g[5:] if g.startswith("GUID:") else g) in seen:
            continue
        refs.append(canon)
        seen.add(canon)
        seen.add(canon[5:])
        changed = True
    if changed:
        data["references"] = refs
    return changed


def patch_asmdef_file(path: str, guids: list[str]) -> bool:
    try:
        data = load_json(path)
    except (OSError, json.JSONDecodeError) as e:
        print(f"skip (unreadable): {path}: {e}", file=sys.stderr)
        return False
    if not merge_refs(data, guids):
        return False
    save_json(path, data)
    print(f"patched: {path}")
    return True


def iter_asmdefs(root: str):
    if not os.path.isdir(root):
        return
    for dirpath, _, files in os.walk(root):
        for fn in files:
            if fn.endswith(".asmdef"):
                yield os.path.join(dirpath, fn)


def find_engine_core_guid(root: str) -> str | None:
    for path in iter_asmdefs(root):
        try:
            data = load_json(path)
        except (OSError, json.JSONDecodeError):
            continue
        name = data.get("name") or ""
        root_ns = data.get("rootNamespace") or ""
        if (
            name == "EngineCore"
            or "EngineCore" in name
            or root_ns == "EngineCore"
        ):
            g = read_guid_from_meta(path + ".meta")
            if g:
                return g
    for dirpath, _, files in os.walk(root):
        if "EngineCore.asmdef.meta" in files:
            g = read_guid_from_meta(os.path.join(dirpath, "EngineCore.asmdef.meta"))
            if g:
                return g
    return None


def main() -> int:
    cwd = os.getcwd()
    print(f"patch_thirdparty_asmdefs: cwd={cwd}")

    n = 0
    if os.path.isdir(THIRDPARTY):
        for path in iter_asmdefs(THIRDPARTY):
            if patch_asmdef_file(path, [CINEMACHINE_RUNTIME_GUID]):
                n += 1
        print(f"Added Cinemachine ref to {n} asmdef(s) under {THIRDPARTY}")
    else:
        print(f"No {THIRDPARTY}; skipping Cinemachine patches.")

    ec = find_engine_core_guid(THIRDPARTY) if os.path.isdir(THIRDPARTY) else None
    if ec:
        print(f"Found EngineCore assembly guid: {ec}")
        if os.path.isfile(RUNTIME_ASMDEF):
            if patch_asmdef_file(RUNTIME_ASMDEF, [ec]):
                print("Updated RMC.MyProject.Runtime with EngineCore reference.")
        else:
            print(f"Missing {RUNTIME_ASMDEF}", file=sys.stderr)
            return 1
    else:
        print("EngineCore asmdef not found under 3rdParty (ok if package not in zip).")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
