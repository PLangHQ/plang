"""Decider v6 alone on the golden goals, scored like decider_eval (v5): picks per step against the golden's
own actions, at the 0.5 and 0.9 cuts.

    1  per step one choice over the modules and the popular actions (structured criteria)
    2  per candidate (share ≥ harness.CANDIDATE) a score: how the step uses it (harness.USE_LEVELS)
    3  the action of a picked module option; else/elseif by name where condition.if is picked

A pick's score is P(level ≥ 2), the probability the step uses the action itself. The docs' normalized
level (score / 3) is kept beside it: it puts a right "inside a condition" pick at 0.67, so it can't be
the pick score. A candidate whose likeliest level is 1 is held (named to run later), not a pick.

    RUN=1 python3 decider_v6_eval.py
"""
import json, os, time, collections, concurrent.futures as cf
import harness as h
import child_eval as e
import decider_eval as d

RUN = os.environ.get('RUN', '1')
DUMP = f'/shared/coder/2.0/rounds/decider-v6-run{RUN}'
OUT = os.path.join(h.OUT, f'decider_v6_run{RUN}_' + time.strftime('%Y%m%d_%H%M%S') + '.json')

def one(case, cat):
    goal = e.goal_of(case)
    folder = os.path.join(DUMP, case['id'])
    probs, uses, acts, stats = h.v6(goal, cat, dump=folder)
    for label in ('1.decider', '2.decider', '3.decider'):
        if os.path.exists(os.path.join(folder, f'{label}.request.json')): d.readable(folder, label)
    steps = []
    for s in goal['steps']:
        i = s['index']
        pick, held = {}, []
        for o, (level, p2, pr) in uses.get(i, {}).items():
            likeliest = max(pr, key=pr.get) if pr else None
            if likeliest == '1': held.append(o)
            if '.' in o:
                pick[o] = {'score': p2, 'from': 'score', 'level': level}
            elif (i, o) in acts and acts[(i, o)][0]:
                pick[f'{o}.{acts[(i, o)][0]}'] = {'score': p2, 'from': 'score + stage 3', 'level': level, 'confidence': acts[(i, o)][1]}
        for a in h.BRANCHES:
            if (i, a) in acts: pick[a] = {'score': acts[(i, a)][1], 'from': 'branch'}
        steps.append({'index': i, 'text': s['text'], 'expected': case['menu'][str(i)], 'held': held,
                      'options': {o: p for o, p in sorted(probs[i].items(), key=lambda kv: -(kv[1] or 0)) if (p or 0) >= 0.05},
                      'pick': pick})
    return {'goal': case['id'], 'steps': steps, 'stats': stats}

if __name__ == '__main__':
    cat = h.catalogue()
    with cf.ThreadPoolExecutor(5) as ex:
        results = list(ex.map(lambda c: one(c, cat), e.GOLDEN))
    json.dump(results, open(OUT, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    for cut in (0.9, 0.5):
        n = collections.Counter()
        for r in results:
            for s in r['steps']:
                hit, miss, extra = d.at(s, cut)
                n['hit'] += len(hit); n['miss'] += len(miss); n['extra'] += len(extra); n['exact'] += not miss and not extra
        print(f'cut {cut}: steps exact {n["exact"]}/58; actions hit {n["hit"]}, missed {n["miss"]}, extra {n["extra"]}')
    tot = collections.Counter()
    for r in results: tot.update(r['stats'])
    for st in ('1', '2', '3'):
        print(f'stage {st}: {tot[st + ".questions"]} questions, {tot[st + ".bytes"] / 1024:.0f} KB, {tot[st + ".secs"]:.1f}s, '
              f'tokens {tot[st + ".input_tokens"]:,} / {tot[st + ".output_tokens"]:,}')
    print('wrote', OUT)
