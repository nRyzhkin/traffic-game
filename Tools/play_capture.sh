#!/bin/sh
# Enters Play mode, prints console errors/warnings and saves a downscaled Game view capture to Captures/<name>.png.
cd "$(dirname "$0")/.." || exit 1
name=${1:-capture}
mkdir -p Captures
unity command clear_console > /dev/null
unity command editor_play > /dev/null
sleep "${2:-5}"
unity command console --tail 30 --result-only | python3 -c "
import json, sys
for e in json.load(sys.stdin).get('entries', []):
    if e['level'] in ('error', 'warning'):
        print(e['level'], '|', e['message'][:300].replace('\n', ' '), '|', e.get('stackTrace', '')[:300].replace('\n', ' '))
"
unity command capture_game_view --max_resolution 900 --source camera --result-only | python3 Tools/save_capture.py "Captures/$name.png"
