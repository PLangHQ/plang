"""Runs stage 3 on a set and scores it against the .pr parameter values."""
import json, os, sys, collections
import harness as h, params as pp

def same(got, want):
    if got is None: return want is None
    if want is None: return got == pp.NONE
    if isinstance(want, (dict, list)): return None          # not a choice — expected to fall to the llm
    a, b = str(got).strip().strip('"\''), str(want).strip().strip('"\'')
    if a.lower() == b.lower(): return True
    if a == pp.NONE: return False
    try: return float(a) == float(b)
    except ValueError: return False

def run(setname):
    os.environ['SET'] = setname if setname != 'chosen' else ''
    cat = h.catalogue()
    goals = h.goals()
    ok = bad = structural = 0; misses = []; secs = 0.0; nq = 0
    for g in goals:
        plan = {s['index']: [(m, a) for m, a in s['label']] for s in g['steps']}
        answers, t, n = pp.ask_params(g, cat, plan)
        secs += t; nq += n
        for s in g['steps']:
            want = pp.label_params(s)
            for (m, a, pname), v in want.items():
                got = answers.get((s['index'], m, a, pname))
                if got is None: continue                      # not asked (no candidates)
                decl = (pp.parameters(m, a) or {}).get(pname) or {}
                d = decl.get('default')
                # A value equal to the declared default is what happens when nobody answers —
                # the .pr materialises it, the step never states it. Not a question.
                if d is not None and str(v).strip().lower() == str(d).strip().lower(): continue
                verdict = same(got[0], v)
                if verdict is None: structural += 1
                elif verdict: ok += 1
                else:
                    bad += 1
                    misses.append((s['text'][:58], f'{m}.{a}.{pname}', v, got[0], round(got[1] or 0, 2)))
    tot = ok + bad
    print(f'{setname}: {len(goals)} goals, {nq} questions, {secs:.1f}s')
    print(f'  answerable params: {tot}   correct {ok}  wrong {bad}   -> {ok/tot:.1%}' if tot else '  none')
    print(f'  structural (dict/list value, expected to fall to the llm): {structural}')
    if misses:
        print('  misses (step | param | want | got | conf):')
        for x in misses: print('   ', ' | '.join(str(y)[:46] for y in x))

if __name__ == '__main__':
    run(sys.argv[1] if len(sys.argv) > 1 else 'chosen')
