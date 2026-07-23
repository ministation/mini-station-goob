#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# Capture a short high-pop load snapshot for before/after perf compares.
# Run on the game host while players report lag (ideally 40+ online).
#
# Usage:
#   chmod +x Tools/measure_highpop_load.sh
#   sudo Tools/measure_highpop_load.sh [output.txt]

set -euo pipefail

OUT="${1:-highpop-load-$(date +%F-%H%M%S).txt}"
ROBUST_PID="$(pgrep -n -f 'Robust.Server' || true)"

{
  echo "===== HOST ====="
  date
  hostnamectl 2>/dev/null | head -n 20 || hostname
  uptime
  echo
  echo "===== MEM ====="
  free -h
  echo
  echo "===== TOP CPU ====="
  ps -eo pid,user,%cpu,%mem,rss,cmd --sort=-%cpu | head -n 20
  echo
  if [[ -n "${ROBUST_PID}" ]]; then
    echo "===== Robust.Server pidstat (10s) pid=${ROBUST_PID} ====="
    if command -v pidstat >/dev/null 2>&1; then
      pidstat -u -p "${ROBUST_PID}" 1 10
    else
      echo "pidstat not installed (apt install sysstat)"
    fi
  else
    echo "===== Robust.Server not found ====="
  fi
  echo
  echo "===== NOTES ====="
  echo "Compare: loadavg vs nproc, Robust %CPU (~200% = ~2 cores),"
  echo "wsgi/TTS competing, available RAM, player complaints + client netgraph DOWN."
  echo "After deploy expect: same pop, lower tick hitch / complaints if presets applied."
} | tee "${OUT}"

echo "Wrote ${OUT}"
