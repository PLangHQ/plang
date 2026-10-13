#!/usr/bin/env python3
"""For each 'uncovered' action, check if it really appears as module+name in any
built test .pr. Reports which are genuinely absent vs. falsely marked uncovered.
Usage: python3 -I coverage.py <testdir> <run_output_file>
"""
import json, glob, os, re, sys
testdir = sys.argv[1]
runfile = sys.argv[2]
os.chdir(testdir)

# the [ ] uncovered actions from the run's coverage section
uncovered = []
for line in open(runfile):
    m = re.match(r'\s+\[ \] ([a-z]+)\.([A-Za-z]+)\s*$', line)
    if m:
        uncovered.append((m.group(1), m.group(2)))

# index every module+name pair that appears in any built test .pr
present = {}  # (module,name) -> count of test prs
for pr in glob.glob('**/.build/*.test.pr', recursive=True):
    try:
        d = json.load(open(pr))
    except Exception:
        continue
    pairs = set()
    def scan(codes):
        for c in codes or []:
            if c.get('module') and c.get('name'):
                pairs.add((c['module'], c['name']))
            # nested Recovery actions are strings; skip
    for s in d.get('step', []):
        scan(s.get('code'))
    for mn in pairs:
        present[mn] = present.get(mn, 0) + 1

OWNED = {  # next-branch.md known gaps (brief says don't re-list)
 ('crypto','encrypt'),('crypto','decrypt'),('module','add'),('module','remove'),
 ('on','cancel'),('on','unbind'),('on','input'),('identity','list'),
 ('identity','unarchive'),('code','list'),('code','remove'),('code','setDefault'),
 ('snapshot','resume'),
}
BYDESIGN_MODULES = {'browser','screen','window','terminal','build'}  # os/ui + builder-internal
BYDESIGN_PAIRS = {('llm','decider')}

print("== uncovered actions that ARE actually exercised (coverage report under-credits) ==")
for m,n in uncovered:
    if (m,n) in present:
        print(f"  {m}.{n}  (in {present[(m,n)]} test pr)")

print("\n== genuinely absent, but OWNED (next-branch) — not re-raised ==")
for m,n in uncovered:
    if (m,n) not in present and (m,n) in OWNED:
        print(f"  {m}.{n}")

print("\n== genuinely absent, BY-DESIGN (os/ui/builder-internal) ==")
for m,n in uncovered:
    if (m,n) not in present and (m,n) not in OWNED and (m in BYDESIGN_MODULES or (m,n) in BYDESIGN_PAIRS):
        print(f"  {m}.{n}")

print("\n== GENUINE GAPS to report (absent, not owned, not by-design) ==")
for m,n in uncovered:
    if (m,n) not in present and (m,n) not in OWNED and m not in BYDESIGN_MODULES and (m,n) not in BYDESIGN_PAIRS:
        print(f"  {m}.{n}")
