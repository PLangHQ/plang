"""The C# pick.list twin fixture — per golden goal: its steps, the decider's two answers as the service
gave them in a recorded round, and what python makes of them with TODAY's catalogue: the stage-2
questions stage 1's answer asks (harness.stage2_questions) and the picks both answers mean
(harness.picks + decider_eval.one's scoring). No service call: the answers are the recorded ones.

    python3 pick_fixture.py [round-folder]   → PLang.Tests/Wire/App/Decider/pick_golden.json
"""
import json, os, sys, collections
import harness as h
import child_eval as e
import prompt_c as c

HERE = os.path.dirname(os.path.abspath(__file__))
ROUND = sys.argv[1] if len(sys.argv) > 1 else '/shared/coder/2.0/rounds/round11-run1'
OUT = os.path.join(HERE, '..', '..', 'PLang.Tests', 'Wire', 'App', 'Decider', 'pick_golden.json')

def stage1_probs(answer1):
    """As harness.stage1 reads the service's answer."""
    probs = collections.defaultdict(dict)
    for k, a in answer1.items():
        i, m = k[1:].split('_', 1)
        if m == '@module': probs[int(i)].update(a.get('probabilities') or {a.get('choice'): a.get('confidence')})
        else: probs[int(i)][m] = a.get('noul')
    return probs

def stage2_acts(answer2):
    """As harness.stage2 reads the service's answer."""
    out = {}
    for k, a in answer2.items():
        i, m = k[1:].split('_', 1)
        if m == '@popular': out[(int(i), m)] = (a.get('choice'), a.get('probabilities') or {a.get('choice'): a.get('confidence')})
        elif m in h.BRANCHES or m.startswith('@also.'): out[(int(i), m)] = (None, a.get('noul'))
        else: out[(int(i), m)] = (a.get('choice'), a.get('confidence'))
    return out

cat = h.catalogue()
entries = []
for case in e.GOLDEN:
    folder = os.path.join(ROUND, case['id'], 'decider')
    def read(name):
        p = os.path.join(folder, name)
        return json.load(open(p, encoding='utf-8')) if os.path.exists(p) else {}
    goal = e.goal_of(case)
    # a round recorded before error.handle became on.error: its ids and options take today's name
    renamed = lambda d: json.loads(json.dumps(d).replace('error.handle', 'on.error'))
    answer1, answer2 = renamed(read('1.decider.answers.json')), renamed(read('2.decider.answers.json'))
    probs = stage1_probs(answer1)
    split = {i: h.picks(probs[i], cat) for i in probs}
    chosen = {i: ask2 for i, (_, ask2, _) in split.items()}
    runners = {i: runner for i, (_, _, runner) in split.items() if runner}
    conditions = {i for i, (common, _, _) in split.items() if (common.get('condition.if') or 0) >= 0.5}
    unsure = {i for i in probs if h.unsure(probs[i], cat)}
    questions = {}
    for s in goal['steps']: questions.update(h.stage2_questions(s, cat, chosen, conditions, runners, unsure))
    question1 = {}
    for s in goal['steps']: question1.update(h.stage1_questions(s, cat))
    # the states, as stage1 and stage2 build them
    state1 = h.state_for(goal, cat)
    used = {m for ms in chosen.values() for m in ms} | ({'condition'} if conditions else set())
    state2 = h.state_for(goal, cat, modules=used)
    if unsure:
        state2 += '\n\nThese actions are offered by name:\n' + '\n'.join(
            f'- {a}: {cat[a.split(".", 1)[0]]["actions"][a.split(".", 1)[1]]["description"]}' for a in h.POPULAR)
    # the picks, as decider_eval.one scores them — single-action modules answered by the module itself
    acts = stage2_acts(answer2)
    for i, ms in chosen.items():
        for m in ms:
            if len(cat[m]['actions']) == 1: acts[(i, m)] = (next(iter(cat[m]['actions'])), 1.0)
    picks = {}
    for s in goal['steps']:
        i = s['index']
        pick = {a: p for a, p in split.get(i, ({}, [], []))[0].items()}
        for m in chosen.get(i, []):
            if (i, m) not in acts: continue
            name = f'{m}.{acts[(i, m)][0]}'
            if name in pick: continue
            pick[name] = acts.get((i, f'@also.{m}'), (None, None))[1] if m in runners.get(i, []) else probs[i][m]
        for a in h.BRANCHES:
            if (i, a) in acts: pick[a] = acts[(i, a)][1]
        if (i, '@popular') in acts:
            pick['@popular'] = dict(sorted((acts[(i, '@popular')][1] or {}).items(), key=lambda ap: -(ap[1] or 0))[:3])
        picks[str(i)] = pick
    user = c.user_message_c(goal, {int(i): p for i, p in picks.items()})
    entries.append({'goal': case['id'], 'name': goal['name'], 'user': user,
                    'step': [{'index': s['index'], 'text': s['text'], 'indent': s.get('indent', 0), 'comment': s.get('comment')}
                             for s in goal['steps']],
                    'answer1': answer1, 'answer2': answer2, 'state1': state1, 'question1': question1,
                    'state2': state2, 'question2': questions, 'picks': picks})
os.makedirs(os.path.dirname(OUT), exist_ok=True)
json.dump(entries, open(OUT, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print(len(entries), 'goals ->', os.path.relpath(OUT, os.path.join(HERE, '..', '..')))
