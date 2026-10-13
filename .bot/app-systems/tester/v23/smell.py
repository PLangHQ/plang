#!/usr/bin/env python3
"""Flag plang test steps whose saved .pr action may not check what the words say.
Read-only. Heuristic: emits candidates for human review, not verdicts.
Usage: python3 -I smell.py <testdir>
"""
import json, glob, os, re, sys

root = sys.argv[1] if len(sys.argv) > 1 else '.'
TYPEWORDS = {'file','dict','list','image','video','audio','number','text','bool',
             'guid','date','datetime','path','object','csv','json'}

def props(code):
    return {p.get('name'): p for p in (code.get('property') or [])}

def pval(code, name):
    p = props(code).get(name)
    return p.get('value') if p else None

def flag(prpath, idx, text, msg):
    print(f"[FLAG] {prpath}  step {idx}\n   text:  {text!r}\n   why:   {msg}\n")

for prpath in sorted(glob.glob(os.path.join(root, '**/.build/*.test.pr'), recursive=True)):
    try:
        d = json.load(open(prpath))
    except Exception as e:
        print(f"!! {prpath}: {e}"); continue
    rel = prpath
    for s in d.get('step', []):
        text = s.get('text') or ''
        low = text.lower()
        codes = s.get('code') or []
        acts = [f"{c.get('module')}.{c.get('name')}" for c in codes]
        joined = " ".join(acts)

        # --- Smell A: 'is [not] <TYPE>' in text saved as a truth/null check ---
        m = re.search(r'\bis\s+(not\s+)?(?:a\s+|an\s+)?([a-z]+)\b', low)
        if m and m.group(2) in TYPEWORDS:
            tw = m.group(2)
            # expected: a condition.if/assert with the type word as Right, operator is/isnot
            saved_has_typeword = False
            for c in codes:
                for p in (c.get('property') or []):
                    v = p.get('value')
                    if isinstance(v, str) and v.lower() == tw:
                        saved_has_typeword = True
                    if p.get('name') in ('Operator',) and isinstance(v,str) and v in ('is','isnot'):
                        pass
            truthish = any(n in ('assert.isTrue','assert.isFalse','assert.isNull','assert.isNotNull') for n in acts)
            if truthish and not saved_has_typeword:
                flag(rel, s.get('index'), text,
                     f"text asserts type '{tw}' but saved {joined} with no type operand (type word dropped?)")
            # value-compare instead of type-compare
            for c in codes:
                if f"{c.get('module')}.{c.get('name')}" == 'condition.if':
                    op = pval(c, 'Operator')
                    if op in ('==','!=','equals','notequals') and (pval(c,'Right') or '').__str__().lower()==tw:
                        flag(rel, s.get('index'), text,
                             f"'is {tw}' saved as value-compare Operator={op} Right={tw} (should be is/isnot type check)")

        # --- Smell B: 'if %x% is <0/literal>' condition with no Right ---
        if low.startswith('if ') or ' if ' in low:
            for c in codes:
                if f"{c.get('module')}.{c.get('name')}" == 'condition.if':
                    pr = props(c)
                    if 'Left' in pr and 'Operator' in pr and 'Right' not in pr:
                        flag(rel, s.get('index'), text,
                             f"condition.if has Left+Operator but no Right (dropped operand?)")

        # --- Smell C: 'write ... to <name>' saved without a channel/target ---
        m2 = re.search(r'\bwrite\b.*\bto\s+([a-z_][\w-]*)\b', low)
        if m2 and 'output.write' in joined:
            tgt = m2.group(1)
            chan = None
            for c in codes:
                if f"{c.get('module')}.{c.get('name')}" == 'output.write':
                    for p in (c.get('property') or []):
                        if p.get('name','').lower() in ('channel','target','to'):
                            chan = p.get('value')
            if chan is None:
                flag(rel, s.get('index'), text,
                     f"'write ... to {tgt}' saved as output.write with no channel param (channel dropped?)")
            elif isinstance(chan,str) and chan.lower() != tgt.lower():
                flag(rel, s.get('index'), text,
                     f"'write ... to {tgt}' but saved channel={chan!r} (channel name changed?)")

        # --- Smell D: 'on error <X>' step whose on.error code does nothing ---
        if 'on error' in low:
            for c in codes:
                if c.get('module') == 'on' and c.get('name') == 'error':
                    keys = set(props(c).keys())
                    if not (keys & {'Recovery','Retry','RetryCount','Ignore','GoalToCall',
                                    'Call','Handler','GoalName','Goal'}):
                        flag(rel, s.get('index'), text,
                             f"on.error code has no Recovery/Retry/Ignore/Call ({sorted(keys)}) — no-op handler?")
