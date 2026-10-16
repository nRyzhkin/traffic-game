#!/bin/sh
# Queues a simulated input event in Play mode (the Editor must be focused).
# Usage: Tools/sim.sh key <KeyName[,KeyName]> down|up    |    Tools/sim.sh mouse <x> <y> [left|right|none]
cd "$(dirname "$0")/.." || exit 1
if [ "$1" = "key" ]; then
  keys=$(echo "$2" | sed 's/\([A-Za-z0-9]*\)/UnityEngine.InputSystem.Key.\1/g')
  if [ "$3" = "down" ]; then state="new UnityEngine.InputSystem.LowLevel.KeyboardState($keys)"; else state="new UnityEngine.InputSystem.LowLevel.KeyboardState()"; fi
  code="UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, $state); return \"ok\";"
else
  button=""
  case "$4" in
    left) button=".WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true)" ;;
    right) button=".WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, true)" ;;
  esac
  code="UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new UnityEngine.InputSystem.LowLevel.MouseState { position = new UnityEngine.Vector2($2f, $3f) }$button); return \"ok\";"
fi
unity command eval --code "$code" --result-only | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('result') or d.get('diagnostics'))"
sleep 0.6
