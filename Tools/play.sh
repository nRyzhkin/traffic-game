#!/bin/sh
# Enters Play mode and prepares the Editor for simulated input (input reaches the Game view even when the Editor is unfocused).
cd "$(dirname "$0")/.." || exit 1
for i in $(seq 1 30); do
  [ "$(unity status 2>&1 | awk 'NR==2{print $2}')" = "ready" ] && break
  sleep 2
done
unity command clear_console > /dev/null 2>&1
unity command editor_play > /dev/null
sleep 4
unity command eval --code 'UnityEngine.Application.runInBackground = true; UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; return "ok";' --result-only > /dev/null
sleep 1
unity command console --tail 30 --result-only | python3 -c "
import json, sys
for e in json.load(sys.stdin).get('entries', []):
    if e['level'] in ('error', 'warning'):
        print(e['level'], '|', e['message'][:300].replace(chr(10), ' '), '|', e.get('stackTrace', '')[:300].replace(chr(10), ' '))
"
