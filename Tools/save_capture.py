import json, sys, base64, re
d = json.load(sys.stdin)
def find(o):
    if isinstance(o, dict):
        for v in o.values():
            if isinstance(v, str) and len(v) > 1000: return v
            r = find(v)
            if r: return r
b = re.sub('^data:image/png;base64,', '', find(d))
open(sys.argv[1], 'wb').write(base64.b64decode(b)); print('saved', sys.argv[1])
