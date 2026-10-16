#!/bin/sh
# While in Play mode: moves the main camera to (x, y) with the given half height and saves a capture.
# Usage: Tools/capture_at.sh <name> <x> <y> <halfHeight> [showLanes]
cd "$(dirname "$0")/.." || exit 1
lanes=${5:-false}
unity command eval --code "var c = UnityEngine.Camera.main; c.transform.position = new UnityEngine.Vector3($2f, $3f, -100f); c.orthographicSize = $4f; var d = UnityEngine.Object.FindAnyObjectByType<TrafficGame.Views.Debugging.LaneGraphDebugView>(); if (d != null) d.GetComponent<UnityEngine.MeshRenderer>().enabled = $lanes; return \"ok\";" --result-only > /dev/null
sleep 0.5
unity command capture_game_view --max_resolution 700 --source camera --result-only | python3 Tools/save_capture.py "Captures/$1.png"
