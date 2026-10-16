#!/bin/sh
# In Play mode: captures the Game view including UI and restores its real aspect ratio. Usage: Tools/capture_ui.sh <name> [width]
cd "$(dirname "$0")/.." || exit 1
unity command editor_focus > /dev/null
sleep 1
size=$(unity command eval --code 'return UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;' --result-only | python3 -c "import json,sys; print(json.load(sys.stdin)['result'])")
unity command capture_game_view --max_resolution 1400 --source screen --result-only | python3 Tools/save_capture.py "Captures/$1.png" > /dev/null
python3 - "$size" "Captures/$1.png" "${2:-640}" <<'PY'
import subprocess, sys
w, h = (int(v) for v in sys.argv[1].split("x"))
target_w = int(sys.argv[3]); target_h = int(target_w * h / w)
subprocess.run(["sips", "-z", str(target_h), str(target_w), sys.argv[2]], capture_output=True)
print(f"saved {sys.argv[2]} ({w}x{h})")
PY
