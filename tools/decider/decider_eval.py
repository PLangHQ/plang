"""The decider alone (stages 1 and 2, no stage-3 call) on the golden goals: which actions it picks per
step, with their scores, against the actions the golden expects (its menu: every action the step uses,
its conditions' bodies and modifiers included).

    stage 1  per step one choice (which module does the main work) + noul per common action
                                                                        harness.stage1
    stage 2  per step one choice (which action) for the main module and for a runner-up module at
             ≥ 0.2, each unless a near-certain common action of it already answers it; and, where
             condition.if was picked, noul for condition.elseif and condition.else
                                                                        harness.picks, harness.stage2

A pick's score: a common action's own noul (when stage 2 also names it, its module's probability and the
choice's confidence are kept beside it); the main module's action scores the module's probability, with
the choice's confidence. Scored at two cuts — 0.9 (pre-filled) and 0.5 (pre-filled + possible). Every request
and response is written under /shared/coder/llm/plang/builder-formal/decider/<goal>/.

    python3 decider_eval.py
"""
import json, os, time, collections, concurrent.futures as cf
import harness as h
import child_eval as e

DUMP = os.environ.get('DUMP', '/shared/coder/llm/plang/builder-formal/decider-v4')
OUT = os.path.join(h.OUT, 'decider_eval_' + time.strftime('%Y%m%d_%H%M%S') + '.json')

def dump_to(folder, label):
    os.makedirs(folder, exist_ok=True)
    h._local.dump = (folder, label)

def readable(folder, label):
    """Beside the raw request/response: the state as plain text, the questions and the answers as JSON,
    and readable.txt — each question with its criteria and its answer. A state with an example line
    under the wrong entry fails loudly (harness.misplaced_examples)."""
    req = json.load(open(os.path.join(folder, f'{label}.request.json'), encoding='utf-8'))
    if wrong := h.misplaced_examples(req['state']):
        raise RuntimeError(f'{folder}/{label}: example lines under the wrong entry: ' + '; '.join(wrong[:5]))
    open(os.path.join(folder, f'{label}.state.txt'), 'w', encoding='utf-8').write(req['state'])
    json.dump(req['questions'], open(os.path.join(folder, f'{label}.questions.json'), 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
    resp = os.path.join(folder, f'{label}.response.json')
    answers = json.load(open(resp, encoding='utf-8')).get('answers') if os.path.exists(resp) else {}
    if os.path.exists(resp):
        json.dump(answers, open(os.path.join(folder, f'{label}.answers.json'), 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
    stage = label.split('.')[0]
    out = [f'Stage {stage} — {len(req["questions"])} questions (asked against {label}.state.txt)', '']
    for key, q in req['questions'].items():
        kind = {'choice': 'choice', 'score': 'score'}.get(q['type'], 'yes/no')
        out.append(f'[{key}] {kind}: {q["instructions"]}')
        crit = q.get('criteria') or {}
        if q['type'] == 'score':
            out.append('    levels: ' + ' | '.join(f'{n} {c}' for n, c in enumerate(crit)))
        elif q['type'] == 'choice':
            named = [k for k, v in crit.items() if v is None]
            if len(named) == len(crit): out.append(f'    options: {len(named)} names')
            elif all(isinstance(v, dict) for v in crit.values()):
                out.append(f'    options: {len(crit)}, each with structured criteria (what / not_for / examples): ' + ', '.join(crit))
            else: out.append('    options: ' + '; '.join(f'{k} = {v}' for k, v in crit.items()))
        elif crit:
            out.append(f'    criteria: true = {crit.get("true")} | false = {crit.get("false")}')
        a = answers.get(key) or {}
        if q['type'] == 'score':
            pr = a.get('probabilities') or {}
            out.append(f'    answer:  level {a.get("score")} (confidence {a.get("confidence")}) — '
                       + ', '.join(f'{lv}: {p:.2f}' for lv, p in sorted(pr.items())))
        elif q['type'] == 'choice':
            top = sorted((a.get('probabilities') or {}).items(), key=lambda kv: -(kv[1] or 0))[:3]
            out.append(f'    answer:  {a.get("choice")} (confidence {a.get("confidence")})'
                       + (' — top: ' + ', '.join(f'{k} {v:.2f}' for k, v in top) if top else ''))
        else:
            out.append(f'    answer:  {a.get("noul")}')
        out.append('')
    open(os.path.join(folder, f'{label}.readable.txt'), 'w', encoding='utf-8').write('\n'.join(out))

def one(case, cat, folder=None):
    """folder: where this goal's decider requests and responses go (default DUMP/<goal>)."""
    goal = e.goal_of(case)
    folder = folder or os.path.join(DUMP, case['id'])
    dump_to(folder, '1.decider')
    probs, s1, b1, q1, u1 = h.stage1(goal, cat)
    readable(folder, '1.decider')
    split = {i: h.picks(probs[i], cat) for i in probs}
    chosen = {i: ask2 for i, (_, ask2, _) in split.items()}
    runners = {i: runner for i, (_, _, runner) in split.items() if runner}
    # A stage-2 question not asked: the step's main module has more than one action, but a near-certain
    # common action of that module already answered it.
    skipped = []
    for i in probs:
        main = h.main_module(probs[i], cat)
        if main and main not in split[i][1] and len(cat[main]['actions']) > 1: skipped.append((i, main))
    dump_to(folder, '2.decider')
    conditions = {i for i, (common, _, _) in split.items() if (common.get('condition.if') or 0) >= 0.5}
    unsure = {i for i in probs if h.unsure(probs[i], cat)}
    acts, s2, b2, q2, u2 = h.stage2(goal, cat, chosen, conditions, runners, unsure)
    if q2: readable(folder, '2.decider')
    h._local.dump = None
    steps = []
    for s in goal['steps']:
        i = s['index']
        pick = {a: {'score': p, 'from': 'stage 1'} for a, p in split.get(i, ({}, [], []))[0].items()}
        for m in chosen.get(i, []):
            if (i, m) not in acts: continue
            action, confidence = acts[(i, m)]
            name = f'{m}.{action}'
            # The main module's action scores the module's probability; the runner-up's scores its own
            # "does the step also use it" answer.
            score = acts.get((i, f'@also.{m}'), (None, None))[1] if m in runners.get(i, []) else probs[i][m]
            # A common action keeps its own stage-1 score — the one the 0.9 rule reads; stage 2
            # naming it too is recorded beside it.
            if name in pick: pick[name].update(also='stage 2', module=score, confidence=confidence); continue
            pick[name] = {'score': score, 'from': 'stage 2' + (' yes/no' if m in runners.get(i, []) else ''), 'confidence': confidence}
        for a in h.BRANCHES:   # asked by name when condition.if was picked
            if (i, a) in acts: pick[a] = {'score': acts[(i, a)][1], 'from': 'branch'}
        # an unsure step: which one of the popular actions — the top 3 of that choice, with their probabilities
        popular = sorted((acts.get((i, '@popular'), (None, {}))[1] or {}).items(), key=lambda ap: -(ap[1] or 0))[:3]
        steps.append({'index': i, 'text': s['text'], 'expected': case['menu'][str(i)],
                      'popular': dict(popular) if (i, '@popular') in acts else None,
                      'main': h.main_module(probs[i], cat),
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
        print(f'{r["goal"]:<14} stage 1: {r["stage1"]["questions"]} questions {r["stage1"]["bytes"] / 1024:.0f} KB {r["stage1"]["secs"]:.1f}s {r["stage1"]["usage"]} | '
              f'stage 2: {r["stage2"]["questions"]} questions {r["stage2"]["bytes"] / 1024:.0f} KB {r["stage2"]["secs"]:.1f}s {r["stage2"]["usage"]} | skipped {len(r["skipped"])}')
    print('wrote', OUT)
