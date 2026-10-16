#!/bin/sh
# Recompiles the open Editor and prints only the summary and the first errors/warnings.
cd "$(dirname "$0")/.." || exit 1
# Compiling or running EditMode tests during Play mode breaks the Test Runner; leave Play mode first.
unity command eval --code 'if (UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying = false; return "ok";' --result-only > /dev/null 2>&1
unity recompile --project-path . --json 2>&1 | python3 -c "
import json, sys
raw = sys.stdin.read()
try:
    d = json.loads(raw)['data']
except Exception:
    print(raw[:800]); sys.exit(1)
print(d['status'], d['summary'])
for e in (d['errors'] + d['warnings'])[:15]:
    print(e.get('file', '') + ':' + str(e.get('line', '')) + ' ' + e.get('message', ''))
"
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
  [ "$(unity status 2>&1 | awk 'NR==2{print $2}')" = "ready" ] && exit 0
  sleep 2
done
echo "Editor not ready yet"
