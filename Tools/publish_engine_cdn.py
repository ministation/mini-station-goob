#!/usr/bin/env python3
"""Publish Mini RobustToolbox client builds to cdn.ministation.ru/engine/.

Run on a machine that can SSH to the CDN host, or run the packaging steps
locally and pass --from-dir. Updates /var/robust-engine-builds/manifest.json
by overlaying this version onto a wizden-based manifest copy.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

ENGINE_BASE = os.environ.get("MINI_ENGINE_CDN", "https://cdn.ministation.ru/engine").rstrip("/")
WIZDEN_MANIFEST = "https://robust-builds.cdn.spacestation14.com/manifest.json"
WIZDEN_MODULES = "https://robust-builds.cdn.spacestation14.com/modules.json"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True, help="Engine version string, e.g. 288.1.0")
    p.add_argument("--from-dir", type=Path, help="Directory with Robust.Client_*.zip already built")
    p.add_argument("--ssh-host", default=os.environ.get("MINI_CDN_SSH", "root@138.124.14.77"))
    p.add_argument("--ssh-port", default=os.environ.get("MINI_CDN_SSH_PORT", "2210"))
    p.add_argument("--remote-dir", default="/var/robust-engine-builds")
    args = p.parse_args()

    release = args.from_dir
    if release is None:
        print("--from-dir is required (run Tools/package_client_build.py first)", file=sys.stderr)
        sys.exit(2)

    zips = sorted(release.glob("Robust.Client_*.zip"))
    if not zips:
        print(f"No Robust.Client_*.zip in {release}", file=sys.stderr)
        sys.exit(1)

    platforms = {}
    for z in zips:
        rid = z.name[len("Robust.Client_") : -len(".zip")]
        platforms[rid] = {
            "url": f"{ENGINE_BASE}/builds/{args.version}/{z.name}",
            "sha256": sha256_file(z),
        }
        print(f"{rid}: {platforms[rid]['sha256'][:16]}… {z.stat().st_size}")

    entry = {
        "date": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "insecure": True,
        "platforms": platforms,
    }

    with tempfile.TemporaryDirectory() as td:
        td_path = Path(td)
        # Upload zips
        remote_build = f"{args.remote_dir}/builds/{args.version}"
        subprocess.check_call(
            [
                "ssh",
                "-p",
                args.ssh_port,
                args.ssh_host,
                f"mkdir -p {remote_build}",
            ]
        )
        for z in zips:
            subprocess.check_call(
                [
                    "scp",
                    "-P",
                    args.ssh_port,
                    str(z),
                    f"{args.ssh_host}:{remote_build}/{z.name}",
                ]
            )

        # Fetch remote manifest (or wizden), overlay, push back
        patch = td_path / "patch.json"
        patch.write_text(json.dumps({args.version: entry}))
        subprocess.check_call(
            [
                "scp",
                "-P",
                args.ssh_port,
                str(patch),
                f"{args.ssh_host}:/tmp/engine_version_patch.json",
            ]
        )
        remote_py = f"""
import json, urllib.request
from pathlib import Path
dst = Path({args.remote_dir!r})
manifest_path = dst / "manifest.json"
if manifest_path.exists():
    manifest = json.loads(manifest_path.read_text())
else:
    with urllib.request.urlopen({WIZDEN_MANIFEST!r}, timeout=180) as r:
        manifest = json.load(r)
patch = json.loads(Path("/tmp/engine_version_patch.json").read_text())
manifest.update(patch)
manifest_path.write_text(json.dumps(manifest, separators=(",", ":")))
modules_path = dst / "modules.json"
if not modules_path.exists():
    with urllib.request.urlopen({WIZDEN_MODULES!r}, timeout=180) as r:
        modules_path.write_bytes(r.read())
print("ok", sorted(patch))
"""
        subprocess.check_call(["ssh", "-p", args.ssh_port, args.ssh_host, f"python3 - <<'PY'\n{remote_py}\nPY"])

    print(f"Published engine {args.version} → {ENGINE_BASE}/builds/{args.version}/")
    print(f"Manifest: {ENGINE_BASE}/manifest.json")


if __name__ == "__main__":
    main()
