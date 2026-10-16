#!/bin/sh
# In Play mode: presses a named button of our UI (sends a submit event, as a click would).
cd "$(dirname "$0")/.." || exit 1
unity command eval --code "var r = UnityEngine.Object.FindAnyObjectByType<TrafficGame.Views.UI.GameUi>().GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement; var b = UnityEngine.UIElements.UQueryExtensions.Q(r, \"$1\"); using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); } return \"pressed\";" --result-only | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('result') or d)"
sleep 0.4
