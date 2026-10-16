#!/bin/sh
# In Play mode: prints the screen position (pixels, origin bottom-left) of the centre of a named element of our UI.
cd "$(dirname "$0")/.." || exit 1
unity command eval --code "var r = UnityEngine.Object.FindAnyObjectByType<TrafficGame.Views.UI.GameUi>().GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement; var e = UnityEngine.UIElements.UQueryExtensions.Q(r, \"$1\"); var c = e.worldBound.center; var s = r.panel.scaledPixelsPerPoint; var h = UnityEngine.Camera.main.pixelHeight; return (c.x * s).ToString(\"0\") + \" \" + (h - c.y * s).ToString(\"0\") + \" display=\" + e.resolvedStyle.display + \" enabled=\" + e.enabledInHierarchy;" --result-only | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('result') or d)"
