"""The decider alone (stages 1 and 2, no stage-3 call) on the golden goals: which actions it picks per
step, with their scores, against the actions the golden expects (its menu: every action the step uses,
its conditions' bodies and modifiers included).

    stage 1  noul per (step, module) + noul per (step, common action)   harness.stage1
    stage 2  choice per (step, module), for a module stage 1 picked that no near-certain common
             action already answers                                    harness.picks, harness.stage2

A pick's score: a common action's own noul (when stage 2 also names it, its module noul and the choice's
confidence are kept beside it); any other action's module noul, with the choice's confidence. Scored at two cuts — 0.9 (pre-filled) and 0.5 (pre-filled + possible). Every request
and response is written under /shared/coder/llm/plang/builder-formal/decider/<goal>/.

    python3 decider_eval.py
"""
import json, os, time, collections, concurrent.futures as cf
import harness as h
import child_eval as e

DUMP = '/shared/coder/llm/plang/builder-formal/decider'
OUT = os.path.join(h.OUT, 'decider_eval_' + time.strftime('%Y%m%d_%H%M%S') + '.json')

def dump_to(folder, label):
    os.makedirs(folder, exist_ok=True)
    h._local.dump = (folder, label)

def readable(folder, label):
    """Beside the raw request/response: the state as plain text, the questions and the answers as JSON."""
    req = json.load(open(os.path.join(folder, f'{label}.request.json'), encoding='utf-8'))
    open(os.path.join(folder, f'{label}.state.txt'), 'w', encoding='utf-8').write(req['state'])
    json.dump(req['questions'], open(os.path.join(folder, f'{label}.questions.json'), 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
    resp = os.path.join(folder, f'{label}.response.json')
    if os.path.exists(resp):
        json.dump(json.load(open(resp, encoding='utf-8')).get('answers'),
                  open(os.path.join(folder, f'{label}.answers.json'), 'w', encoding='utf-8'), indent=2, ensure_ascii=False)

def one(case, cat):
    goal = e.goal_of(case)
    folder = os.path.join(DUMP, case['id'])
    dump_to(folder, '1.decider')
    probs, s1, b1, q1, u1 = h.stage1(goal, cat)
    readable(folder, '1.decider')
    split = {i: h.picks(probs[i], cat) for i in probs}
    chosen = {i: ask2 for i, (_, ask2) in split.items()}
    # A stage-2 question not asked: a module stage 1 picked (≥ 0.5) with more than one action, settled
    # by a near-certain common action instead.
    skipped = []
    for i in probs:
        settled = {a.split('.', 1)[0] for a, p in split[i][0].items() if p >= h.NEAR_CERTAIN}
        skipped += [(i, m) for m in settled if (probs[i].get(m) or 0) >= 0.5 and len(cat[m]['actions']) > 1]
    dump_to(folder, '2.decider')
    acts, s2, b2, q2, u2 = h.stage2(goal, cat, chosen)
    if q2: readable(folder, '2.decider')
    h._local.dump = None
    steps = []
    for s in goal['steps']:
        i = s['index']
        pick = {a: {'score': p, 'from': 'stage 1'} for a, p in split.get(i, ({}, []))[0].items()}
        for m in chosen.get(i, []):
            if (i, m) not in acts: continue
            action, confidence = acts[(i, m)]
            name = f'{m}.{action}'
            # A common action keeps its own stage-1 score — the one the 0.9 rule reads; stage 2
            # naming it too is recorded beside it.
            if name in pick: pick[name].update(also='stage 2', module=probs[i][m], confidence=confidence); continue
            pick[name] = {'score': probs[i][m], 'from': 'stage 2', 'confidence': confidence}
        steps.append({'index': i, 'text': s['text'], 'expected': case['menu'][str(i)],
                      'modules': {m: p for m, p in probs[i].items() if m in cat}, 'pick': pick})
    return {'goal': case['id'], 'steps': steps, 'skipped': skipped,
            'stage1': {'secs': s1, 'bytes': b1, 'questions': q1, 'usage': u1},
            'stage2': {'secs': s2, 'bytes': b2, 'questions': q2, 'usage': u2}}

def at(step, cut):
    """(hits, misses, extras) of one step's picks at a cut."""
    got = {a for a, v in step['pick'].items() if v['score'] is not None and v['score'] >= cut}
    want = set(step['expected'])
    return sorted(got & want), sorted(want - got), sorted(got - want)

if __name__ == '__main__':
    cat = h.catalogue()
    with cf.ThreadPoolExecutor(5) as ex:
        results = list(ex.map(lambda c: one(c, cat), e.GOLDEN))
    json.dump(results, open(OUT, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    for cut in (0.9, 0.5):
        n = collections.Counter()
        for r in results:
            for s in r['steps']:
                hit, miss, extra = at(s, cut)
                n['hit'] += len(hit); n['miss'] += len(miss); n['extra'] += len(extra)
                n['exact'] += not miss and not extra; n['steps'] += 1
        print(f'cut {cut}: steps exact {n["exact"]}/{n["steps"]}; actions hit {n["hit"]}, missed {n["miss"]}, extra {n["extra"]}')
    for r in results:
        print(f'{r["goal"]:<14} stage 1: {r["stage1"]["questions"]} questions {r["stage1"]["secs"]:.1f}s {r["stage1"]["usage"]} | '
              f'stage 2: {r["stage2"]["questions"]} questions {r["stage2"]["secs"]:.1f}s {r["stage2"]["usage"]} | skipped {len(r["skipped"])}')
    print('wrote', OUT)
