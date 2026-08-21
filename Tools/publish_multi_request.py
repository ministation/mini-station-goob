#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import argparse
import os
import re
import requests
import subprocess
from typing import Iterable

PUBLISH_TOKEN = os.environ["PUBLISH_TOKEN"]
VERSION = os.environ["GITHUB_SHA"]
FORK_ID = os.environ['FORK_ID']

RELEASE_DIR = "release"

#
# CONFIGURATION PARAMETERS
# Forks should change these to publish to their own infrastructure.
#
_default_cdn = os.environ.get("ROBUST_CDN_URL", "https://cdn.ministation.ru/").strip()
ROBUST_CDN_URL = _default_cdn if _default_cdn.endswith("/") else _default_cdn + "/"

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", default=FORK_ID)

    args = parser.parse_args()
    fork_id = args.fork_id

    session = requests.Session()
    session.headers = {
        "Authorization": f"Bearer {PUBLISH_TOKEN}",
    }

    print(f"Starting publish on Robust.Cdn for version {VERSION}")

    data = {
        "version": VERSION,
        "engineVersion": get_engine_version(),
    }
    headers = {
        "Content-Type": "application/json"
    }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers)
    resp.raise_for_status()
    print("Publish successfully started, adding files...")

    for file in get_files_to_publish():
        print(f"Publishing {file}")
        with open(file, "rb") as f:
            headers = {
                "Content-Type": "application/octet-stream",
                "Robust-Cdn-Publish-File": os.path.basename(file),
                "Robust-Cdn-Publish-Version": VERSION
            }
            resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file", data=f, headers=headers)

        resp.raise_for_status()

    print("Successfully pushed files, finishing publish...")

    data = {
        "version": VERSION
    }
    headers = {
        "Content-Type": "application/json"
    }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers)
    resp.raise_for_status()

    print("SUCCESS!")


def get_files_to_publish() -> Iterable[str]:
    for file in os.listdir(RELEASE_DIR):
        yield os.path.join(RELEASE_DIR, file)


def get_engine_version() -> str:
    props = os.path.join("RobustToolbox", "MSBuild", "Robust.Engine.Version.props")
    if os.path.isfile(props):
        with open(props, encoding="UTF-8") as f:
            text = f.read()
        m = re.search(r"<Version>([^<]+)</Version>", text)
        if m:
            return m.group(1).strip()
    proc = subprocess.run(
        ["git", "describe", "--tags", "--abbrev=0"],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        cwd="RobustToolbox",
        encoding="UTF-8",
    )
    if proc.returncode != 0:
        raise RuntimeError(
            "Engine version: no Robust.Engine.Version.props and git describe failed (need tags or props file)."
        )
    tag = proc.stdout.strip()
    if not tag.startswith("v"):
        raise RuntimeError(f"Unexpected engine tag (expected v-prefix): {tag!r}")
    return tag[1:]


if __name__ == '__main__':
    main()
