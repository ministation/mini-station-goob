#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# Source this (or copy into the watchdog / systemd unit Environment=) before starting Robust.Server.
#
#   source Tools/highpop_server_env.sh
#   # or in systemd:
#   # Environment=DOTNET_gcServer=1
#
# Server GC reduces pause spikes under large heaps (typical for SS14 with 30+ players).

export DOTNET_gcServer=1

# Optional: keep non-game processes from stealing cores on the same host.
# Example for a systemd service running Discord auth / wsgi:
#   CPUQuota=50%
#   Nice=10
# Prefer moving wsgi/TTS off the game node entirely under peak load.
