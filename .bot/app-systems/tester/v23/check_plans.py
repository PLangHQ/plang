#!/usr/bin/env python3
import re, os, glob, sys
testdir = sys.argv[1]
runfile = sys.argv[2]
os.chdir(testdir)
run = open(runfile).read()
status = {}
for m in re.finditer(r'\[(Pass|Fail|Stale|Skipped)\]\s+(\S+\.test\.goal)', run):
    status[m.group(2)] = m.group(1)

missing, unbuilt, notrun = [], [], []
ok = []
for sg in sorted(glob.glob('plan/*/start.goal')):
    txt = open(sg).read()
    for m in re.finditer(r'-\s*call\s+(?:goal\s+)?(/\S+)', txt):
        ref = m.group(1)
        rel = ref.lstrip('/')
        cand = rel + '.test.goal'
        if not os.path.exists(cand):
            if os.path.exists(rel + '.goal'):
                continue  # plain helper goal
            missing.append((sg, ref)); continue
        d = os.path.dirname(cand)
        base = os.path.basename(cand).lower().replace('.goal', '.pr')
        prglob = glob.glob(os.path.join(d, '.build', base))
        if not prglob:
            unbuilt.append((sg, cand))
        key = '/' + cand
        st = status.get(key)
        if st is None:
            notrun.append((sg, key))
        else:
            ok.append((key, st))

print(f"indexed refs that exist, built, and ran: {len(ok)}")
from collections import Counter
print("  by status:", dict(Counter(s for _, s in ok)))
print(f"\nMISSING (.test.goal absent): {len(missing)}")
for s, r in missing: print("  ", s, "->", r)
print(f"\nUNBUILT (.pr absent): {len(unbuilt)}")
for s, c in unbuilt: print("  ", s, "->", c)
print(f"\nNOT FOUND IN RUN OUTPUT: {len(notrun)}")
for s, k in notrun: print("  ", s, "->", k)
