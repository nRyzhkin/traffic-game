#!/bin/sh
# Runs EditMode tests in the open Editor and prints the summary plus failures only.
cd "$(dirname "$0")/.." || exit 1
# Compiling or running EditMode tests during Play mode breaks the Test Runner; leave Play mode first.
unity command eval --code 'if (UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying = false; return "ok";' --result-only > /dev/null 2>&1
for i in $(seq 1 30); do
  [ "$(unity status 2>&1 | awk 'NR==2{print $2}')" = "ready" ] && break
  sleep 2
done
# Right after a domain reload the command can return without a summary; retry a few times.
results="${TMPDIR:-/tmp}/traffic-game-tests.json"
for attempt in 1 2 3; do
  unity command run_tests --mode EditMode --filter "${1:-TrafficGame}" --timeout 900 --result-only > "$results" 2>&1
  grep -q '"Summary"' "$results" && break
  sleep 5
done
python3 - "$results" <<'PY'
import json, sys
try:
    d = json.load(open(sys.argv[1]))
except Exception:
    print(open(sys.argv[1]).read()[:1500]); sys.exit(1)
s = d.get('Summary', {})
print('Total', s.get('Total'), 'Passed', s.get('Passed'), 'Failed', s.get('Failed'))
for r in d.get('Results', []):
    if r.get('Status') != 'Passed':
        print('FAIL', r.get('FullName'), '|', str(r.get('Message'))[:500])
PY
