#!/usr/bin/env python3
"""Find PASSING tests whose only assertions are weak (isNotNull/isNotEmpty) —
the LlmCache false-green shape: green whether the behaviour works or not.
Usage: python3 -I weakonly.py <testdir> <run_output>
"""
import json, glob, os, re, sys
testdir, runfile = sys.argv[1], sys.argv[2]
os.chdir(testdir)
status = {}
for m in re.finditer(r'\[(Pass|Fail|Stale|Skipped)\]\s+(\S+\.test\.goal)', open(runfile).read()):
    status[m.group(2).lower()] = m.group(1)

WEAK = {'isNotNull', 'isNotEmpty'}
STRONG_ASSERT = {'equals','notEquals','isTrue','isFalse','isNull','contains',
                 'notContains','greaterThan','lessThan'}
for pr in sorted(glob.glob('**/.build/*.test.pr', recursive=True)):
    try: d = json.load(open(pr))
    except: continue
    key = '/' + pr.replace('.build/', '').replace('.pr', '.goal')
    # normalize to the /path form the run uses
    runkey = '/' + os.path.dirname(pr).replace('/.build','') + '/' + os.path.basename(pr).replace('.pr','.goal')
    st = status.get(runkey.lower())
    if st != 'Pass':
        continue
    asserts = []
    conds = 0
    for s in d.get('step', []):
        for c in s.get('code', []):
            if c.get('module') == 'assert':
                asserts.append(c.get('name'))
            if c.get('module') == 'condition':
                conds += 1
    if not asserts:
        continue
    strong = [a for a in asserts if a in STRONG_ASSERT]
    weak = [a for a in asserts if a in WEAK]
    if weak and not strong and conds == 0:
        print(f"WEAK-ONLY  {runkey}")
        print(f"    asserts: {asserts}")
