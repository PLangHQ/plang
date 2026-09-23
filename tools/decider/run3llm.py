"""Stage 3 by LLM, scored against the .pr parameter values."""
import json, os, sys, collections
import harness as h, params as pp, stage3 as s3

def norm(v):
    if v is None: return None
    if isinstance(v, bool): return str(v).lower()
    if isinstance(v, (int, float)): return str(v)
    if isinstance(v, (dict, list)): return json.dumps(v, sort_keys=True, separators=(',', ':'))
    return str(v).strip().strip('"\'')

def same(got, want):
    a, b = norm(got), norm(want)
    if a == b: return True
    if a is None or b is None: return False
    if a.lower() == b.lower(): return True
    try: return float(a) == float(b)
    except ValueError: return False

def run(setname, show=30):
    os.environ['SET'] = '' if setname == 'chosen' else setname
    cat = h.catalogue(); goals = h.goals()
    ok = bad = extra = missing = 0; misses = []; secs = 0.0
    usage = collections.Counter()
    for g in goals:
        plan = {s['index']: [(m, a) for m, a in s['label']] for s in g['steps']}
        try:
            ans, t, u, (_, order) = s3.ask(g, plan, cat)
        except Exception as e:
            print(f'  {g["name"]}: ERROR {type(e).__name__} {str(e)[:120]}'); continue
        secs += t; usage.update({k: v for k, v in u.items() if isinstance(v, int)})
        got = s3.flatten(ans, order)
        for s in g['steps']:
            want = pp.label_params(s)
            declared_default = {}
            for (n, m, a, pname), v in want.items():
                d = (pp.parameters(m, a) or {}).get(pname, {}).get('default')
                # a materialised default is not something the step stated
                if d is not None and norm(v) == norm(d): continue
                key = (s['index'], n, pname)
                if key not in got:
                    missing += 1
                    misses.append(('MISSING', s['text'][:52], f'{m}.{a}.{pname}', norm(v), '-'))
                elif same(got[key], v): ok += 1
                else:
                    bad += 1
                    misses.append(('WRONG', s['text'][:52], f'{m}.{a}.{pname}', norm(v), norm(got[key])))
        # parameters the model produced that the .pr does not have
        wanted = {(s['index'], n, p) for s in g['steps'] for (n, m, a, p) in pp.label_params(s)}
        for k in got:
            if k not in wanted:
                extra += 1
                misses.append(('EXTRA', '', f'step{k[0]+1} action{k[1]}.{k[2]}', '-', norm(got[k])))
    tot = ok + bad + missing
    print(f'{setname}: {len(goals)} goals, {secs:.1f}s, {dict(usage)}')
    print(f'  params in .pr: {tot}   matched {ok}  wrong {bad}  missing {missing}   -> {ok/tot:.1%}' if tot else '  none')
    print(f'  produced but not in .pr: {extra}')
    for x in misses[:show]: print('   ', ' | '.join(str(y)[:44] for y in x))

if __name__ == '__main__':
    run(sys.argv[1] if len(sys.argv) > 1 else 'builder', int(sys.argv[2]) if len(sys.argv) > 2 else 30)
