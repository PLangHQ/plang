"""Prompt C vs prompt B on the golden goals — stage 3 with the real decider's picks (v5), not hand menus.

Per round: the decider runs once per goal; its picks feed both prompts, on each model.

    B  frozen/PropertiesB.llm + the schema; propertiesUserB's shape with the menu = the picks ≥ 0.5 (names only);
       the answer in JSON; checked by build_pr.match (steps line up, the chain rule, fold)
    C  Properties.llm; prompt_c.user_message_c (the picks with scores, the certain ones pre-filled in
       formal); the answer in formal, parsed by formal.parse_answer; checked by prompt_c.check (fold, the
       match, and the agreement with the decider)

A refused answer is told why and answered again, once (FixProperties' conversation). C is judged per
step: a refused step is asked again alone and the rest of the answer stands; only a whole-answer
problem (a step missing, extra, renumbered) asks the whole answer again. Per step:
    first attempt  right / fixed (right once fold drops a copied body) / caught (wrong, refused) / silent (wrong, passed)
    final          right / silent (wrong, built) / failed (still refused after the retry: the goal fails loudly)
C also counts the disagreements its check flagged and the warnings it would put on the steps.

    ROUND=1 python3 c_eval.py            MODELS=gpt-5.4-nano PROMPTS=C ROUND=2 python3 c_eval.py
"""
import json, os, re, sys, time, copy, collections, urllib.request, urllib.error, concurrent.futures as cf
import harness as h
import build_pr as b
import child_eval as e
import decider_eval as d
import prompt_c as c
import formal as f
import formal_check as fc
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROUND = os.environ.get('ROUND', '1')
# C + nano by default; B or mini only when asked (MODELS=… PROMPTS=…)
MODELS = os.environ.get('MODELS', 'gpt-5.4-nano').split(',')
PROMPTS = os.environ.get('PROMPTS', 'C').split(',')
SHARED = '/shared/coder/2.0'   # the only place for rendered requests (Ingi); older rounds stay in /shared/coder/llm/plang/builder-formal/
OUT = os.path.join(HERE, 'runs', f'c_eval_round{ROUND}_' + time.strftime('%Y%m%d_%H%M%S'))

# ---------------------------------------------------------------- the calls
def chat(model, messages):
    body = json.dumps({'model': model, 'temperature': 0.0, 'max_completion_tokens': 16000,
                       'messages': messages}).encode()
    for attempt in range(4):
        req = urllib.request.Request('https://api.openai.com/v1/chat/completions', data=body,
                                     headers={'Authorization': f'Bearer {b.OPENAI_KEY}', 'Content-Type': 'application/json'})
        try:
            t0 = time.time()
            with urllib.request.urlopen(req, timeout=300) as r:
                raw = json.loads(r.read())
            return raw, time.time() - t0
        except urllib.error.HTTPError as ex:
            if ex.code in (408, 429, 500, 502, 503, 529) and attempt < 3:
                time.sleep(2 ** attempt); continue
            raise

def content(raw):
    text = raw['choices'][0]['message']['content']
    m = re.fullmatch(r'\s*```\w*\s*(.*?)\s*```\s*', text, re.S)
    return m.group(1) if m else text

# ---------------------------------------------------------------- one prompt, one model, one goal
def request(prompt, case, picks):
    goal = e.goal_of(case)
    if prompt == 'B':
        menu = {i: [a for a, _ in c.listed(p, goal['steps'][i]['text'])] for i, p in picks.items()}
        return b.SYSTEM_B_SENT, b.user_message_b(goal, menu)
    return c.SYSTEM_C, c.user_message_c(goal, picks)

def judge(prompt, case, picks, text):
    """B: (answer as .pr-shaped JSON for scoring, refusals, warnings, disagreements, answer before fold)."""
    goal = e.goal_of(case)
    if prompt == 'B':
        try:
            answer = json.loads(text)
        except ValueError as ex:
            return None, [f'the answer is not JSON: {ex}'], [], [], None
        refused = b.match(goal, answer)
        before = copy.deepcopy(answer)
        return (b.normalize(goal, answer) if not refused else answer), refused, [], [], before
    raise ValueError('C is judged per step: judge_c')

def judge_c(case, picks, parsed, errors, whole):
    """C, per step: (answer for scoring, whole-answer refusals, {step: refusals}, warnings, agreement).
    A line that doesn't parse refuses its own step, naming the line; the rest of the answer stands."""
    goal = e.goal_of(case)
    w, per_step, warnings = c.check(goal, picks, parsed)
    whole = whole + [x for x in w if not any(x.startswith(f'step {i} ') for i in errors)]
    # a line that doesn't parse still reports every problem it shows: its parse error, and each action it
    # names that isn't one of the step's
    texts = {s['index']: s['text'] for s in goal['steps']}
    for i, ex in errors.items():
        per_step.setdefault(i, []).append(f'step {i} does not parse: {ex.reason} — in: {ex.text.strip()}')
        per_step[i] += c.unlisted(i, c.named(ex.text), picks.get(i, {}), texts.get(i, ''))
    agreement = [r for p in per_step.values() for r in p if 'decider' in r or "isn't one of step" in r]
    return {'step': [{'index': i, 'action': parsed[i]} for i in sorted(parsed)]}, whole, per_step, warnings, agreement

def own(case, read):
    """A parsed answer with each step written in formal read from its own text instead (step.IsFormal):
    its line in the answer, if any, is set aside."""
    parsed, errors, whole = read
    lines = '\n'.join(f'[{i}] {s["text"]}' for i, s in enumerate(case['steps']) if h.is_formal(s['text']))
    if not lines: return parsed, errors, whole
    mine, my_errors, _ = f.parse_steps(lines)
    parsed = {i: r for i, r in parsed.items() if i not in mine and i not in my_errors} | mine
    errors = {i: x for i, x in errors.items() if i not in mine and i not in my_errors} | my_errors
    return parsed, errors, whole

def one_c(model, case, picks):
    """C with the per-step retry: a refused step is asked again alone; a whole-answer refusal (a step
    missing, extra, renumbered) asks the whole answer again."""
    system, user = request('C', case, picks)
    messages = [{'role': 'system', 'content': system}, {'role': 'user', 'content': user}]
    folder = os.path.join(OUT, 'C', model, case['id'])
    os.makedirs(folder, exist_ok=True)
    calls = []
    raw, secs = chat(model, messages)
    calls.append({'seconds': secs, 'usage': raw.get('usage') or {}})
    text1 = content(raw)
    open(os.path.join(folder, '1.answer.txt'), 'w', encoding='utf-8').write(text1)
    parsed, errors, whole = own(case, f.parse_steps(text1))
    before = {'step': [{'index': i, 'action': copy.deepcopy(parsed[i])} for i in sorted(parsed)]}
    first, whole1, steps1, warn1, agree1 = judge_c(case, picks, parsed, errors, whole)
    caught = set(range(len(case['steps']))) if whole1 else set(steps1)
    final, whole2, steps2, warn2, agree2 = first, whole1, steps1, warn1, agree1
    if whole1 or steps1:
        if whole1:
            ask = RETRY['C'].format(why='; '.join(whole1 + [r for p in steps1.values() for r in p]))
        else:
            ask = (f'Steps {", ".join(str(i) for i in sorted(steps1))} were refused: '
                   + '; '.join(r for i in sorted(steps1) for r in steps1[i])
                   + '\n\nAnswer again only these steps, one line each, starting with its [i], in formal.')
        messages += [{'role': 'assistant', 'content': text1}, {'role': 'user', 'content': ask}]
        raw2, secs2 = chat(model, messages)
        calls.append({'seconds': secs2, 'usage': raw2.get('usage') or {}})
        text2 = content(raw2)
        open(os.path.join(folder, '2.answer.txt'), 'w', encoding='utf-8').write(text2)
        parsed2, errors2, whole2 = own(case, f.parse_steps(text2))
        if not whole1:   # only the refused steps were asked: they replace theirs; the rest stands
            merged = {i: r for i, r in parsed.items() if i not in steps1}
            merged.update({i: r for i, r in parsed2.items() if i in steps1})
            errors2 = {i: x for i, x in errors2.items() if i in steps1}
            whole2 = [x for x in whole2 if not x.endswith('answered twice')]
            parsed2 = merged
        final, whole2, steps2, warn2, agree2 = judge_c(case, picks, parsed2, errors2, whole2)
        if agree1 and not agree2: warn2 = warn2 + [f'settled on retry: {x}' for x in agree1]
    goal = e.goal_of(case)
    unsure = [s['index'] for s in goal['steps'] if c.is_unsure(picks.get(s['index'], {}), s['text'])]
    popular = [w for w in warn2 if 'popular-action choice' in w]
    return {'prompt': 'C', 'model': model, 'case': case['id'], 'first': first, 'before': before,
            'unsure': unsure, 'popular_used': popular,
            'refused1': whole1 + [r for p in steps1.values() for r in p], 'caught': sorted(caught),
            'final': final, 'refused2': (whole2 + [r for p in steps2.values() for r in p]) if (whole1 or steps1) else [],
            'warnings': warn2, 'disagreements': agree1, 'calls': calls}

RETRY = {'B': 'Your answer was rejected by the validator: {why}\n\nReturn the corrected answer in the same shape.',
         'C': 'Your answer was refused: {why}\n\nAnswer again: the whole goal, one line per step, in formal.'}

def one(prompt, model, case, picks):
    if prompt == 'C': return one_c(model, case, picks)
    system, user = request(prompt, case, picks)
    messages = [{'role': 'system', 'content': system}, {'role': 'user', 'content': user}]
    folder = os.path.join(OUT, prompt, model, case['id'])
    os.makedirs(folder, exist_ok=True)
    calls = []
    raw, secs = chat(model, messages)
    calls.append({'seconds': secs, 'usage': raw.get('usage') or {}})
    text1 = content(raw)
    open(os.path.join(folder, '1.answer.txt'), 'w', encoding='utf-8').write(text1)
    first, refused1, warn1, agree1, before1 = judge(prompt, case, picks, text1)
    final, refused2, warn2, agree2, text2 = first, refused1, warn1, agree1, None
    if refused1:
        messages += [{'role': 'assistant', 'content': text1},
                     {'role': 'user', 'content': RETRY[prompt].format(why='; '.join(refused1))}]
        raw2, secs2 = chat(model, messages)
        calls.append({'seconds': secs2, 'usage': raw2.get('usage') or {}})
        text2 = content(raw2)
        open(os.path.join(folder, '2.answer.txt'), 'w', encoding='utf-8').write(text2)
        final, refused2, warn2, agree2, _ = judge(prompt, case, picks, text2)
        # a disagreement the retry settled is built — with a warning on its step
        if agree1 and not agree2: warn2 = warn2 + [f'settled on retry: {x}' for x in agree1]
    return {'prompt': prompt, 'model': model, 'case': case['id'], 'first': first, 'before': before1,
            'refused1': refused1, 'final': final, 'refused2': refused2 if refused1 else [],
            'warnings': warn2, 'disagreements': agree1, 'calls': calls}

# ---------------------------------------------------------------- scoring
def outcomes(rec, case):
    """Per step: (first-attempt outcome, final outcome, misses)."""
    e.CHILD_TEXT = rec['prompt'] == 'B'
    try:
        first = e.score(case, rec['before']) if rec['before'] else {int(k): (['no answer'], []) for k in case['expect']}
        folded = e.score(case, rec['first']) if rec['first'] else first
        final = e.score(case, rec['final']) if rec['final'] else {int(k): (['no answer'], []) for k in case['expect']}
    finally:
        e.CHILD_TEXT = True
    out = {}
    for k in case['expect']:
        i = int(k)
        fm = first.get(i, (['missing'], []))[0]
        # C is judged per step: a step counts caught when ITS line was refused (or the whole answer was)
        refused = (i in rec['caught']) if 'caught' in rec else bool(rec['refused1'])
        o1 = 'right' if not fm else 'caught' if refused else \
             'fixed' if not folded.get(i, (['x'], []))[0] else 'silent'
        lm = final.get(i, (['missing'], []))[0]
        o2 = 'failed' if rec['refused2'] else 'right' if not lm else 'silent'
        out[i] = (o1, o2, fm if fm else lm)
    return out

def readme(which, run_dir):
    """/shared/coder/2.0/README.md — kept true: which round is in the folders, and what each file is."""
    open(os.path.join(SHARED, 'README.md'), 'w', encoding='utf-8').write(f'''# Prompt C and the decider — the whole request chain per goal (branch `builder-formal`)

The folders hold **{which}** (C + {MODELS[0]}); raw results in `tools/decider/{os.path.relpath(run_dir, HERE)}/`.
This is the only place for rendered requests (Ingi). Each run is also kept under `rounds/round<N>-run<M>/<goal>/`;
rounds before 6 are in `/shared/coder/llm/plang/builder-formal/` as history.

Per goal folder (`build`, `checkout`, `decide`, `start`, `weekly_report`), in the order the builder sends them:

**1. The decider (typesafe), stages 1 and 2.** It has no system/user: one context text (the *state*) and a list of
questions, each answered with a score.
- `decider/1.decider.state.txt`: stage 1's context — how plang is structured, the goal's steps (numbered from 0),
  the modules with their descriptions and example steps, the 6 common actions with theirs. Every example line is
  checked to come from the entry it is printed under.
- `decider/1.decider.readable.txt`: stage 1's questions with their criteria and answers — per step one choice
  ("which module does the main work?") and a yes/no for each common action.
- `decider/2.decider.state.txt`, `decider/2.decider.readable.txt`: stage 2 — which action of the main module, the
  runner-up / main-module yes/no, else/elseif by name, and on an unsure step (best score < 0.8) which popular action.
- `decider/*.questions.json` / `*.answers.json`: the same, raw.

**2. The LLM, stage 3, prompt C.**
- `system.txt`: how a formal answer is written, the common steps, then the rules.
- `user.txt`: the goal as written, one line per step — the step `=> decider:` its picks with scores
  `=> formal:` the pre-filled formal line; the types; each action once.

**3. What came back, and what should have.**
- `answer.txt`: the model's answer; `answer.retry.txt` when a step or the answer was refused and asked again.
- `expected.answer.txt`: the expected answer as the model writes it (no types).
- `expected.typed.txt`: the expected result as the `.pr` holds it, types written.

The notation and decisions: `.bot/builder-formal/architect/vision.md` §4 / §4b and the decisions log in
`.bot/builder-formal/architect/summary.md`. The measurements: `.bot/builder-formal/coder/c-eval.md`.
''')

PRICES = e.PRICES
def cost(model, usage): return e.cost(model, usage)

if __name__ == '__main__':
    cat = h.catalogue()
    # Rendered requests go to /shared/coder/2.0/ only (Ingi): the current round in <goal>/, each run
    # copied to rounds/round<N>-run<M>/<goal>/ afterwards.
    run = os.environ.get('RUN', '') or '1'
    # REPLAY=<a run folder>: its recorded picks (decider.json) instead of asking the decider again — a
    # failed case replayed through a changed prompt. CASES=a,b: only those goals. A replay writes its
    # requests and answers to rounds/round<N>-run<M>/ only: the current round's pages stay as they are.
    replay = os.environ.get('REPLAY', '')
    wanted = [x for x in os.environ.get('CASES', '').split(',') if x]
    GOLDEN = [case for case in e.GOLDEN if not wanted or case['id'] in wanted]
    home = (lambda cid: os.path.join(SHARED, 'rounds', f'round{ROUND}-run{run}', cid)) if replay \
        else (lambda cid: os.path.join(SHARED, cid))
    os.makedirs(OUT, exist_ok=True)
    if replay:
        print(f'round {ROUND} run {run}: replaying the picks of {replay} on {len(GOLDEN)} goals', flush=True)
        recorded = json.load(open(os.path.join(replay, 'decider.json')))
        picks = {cid: {int(i): p for i, p in v.items()} for cid, v in recorded['picks'].items()}
        json.dump({'picks': recorded['picks'], 'replay': replay}, open(os.path.join(OUT, 'decider.json'), 'w'), indent=1)
    else:
        print(f'round {ROUND} run {run}: decider v5 on {len(GOLDEN)} goals', flush=True)
        with cf.ThreadPoolExecutor(5) as ex:
            decided = dict(zip([c_['id'] for c_ in GOLDEN],
                               ex.map(lambda case: d.one(case, cat, os.path.join(SHARED, case['id'], 'decider')), GOLDEN)))
        # a step's picks; an unsure step also carries '@popular': the top 3 of the decider's popular-action choice
        picks = {cid: {s['index']: {**{a: v['score'] for a, v in s['pick'].items()},
                                    **({'@popular': s['popular']} if s.get('popular') else {})} for s in r['steps']}
                 for cid, r in decided.items()}
        json.dump({'picks': {k: {str(i): p for i, p in v.items()} for k, v in picks.items()},
                   'decider': {cid: {'stage1': r['stage1'], 'stage2': r['stage2']} for cid, r in decided.items()}},
                  open(os.path.join(OUT, 'decider.json'), 'w'), indent=1)
    # the rendered prompt-C requests and the expected results, for reading
    for case in GOLDEN:
        folder = home(case['id'])
        os.makedirs(folder, exist_ok=True)
        system, user = request('C', case, picks[case['id']])
        open(os.path.join(folder, 'system.txt'), 'w', encoding='utf-8').write(system)
        open(os.path.join(folder, 'user.txt'), 'w', encoding='utf-8').write(user)
        expected = {int(k): [fc.rows_of(a) for a in acts] for k, acts in case['expect'].items()}
        open(os.path.join(folder, 'expected.typed.txt'), 'w', encoding='utf-8').write(f.write_answer(expected) + '\n')
        open(os.path.join(folder, 'expected.answer.txt'), 'w', encoding='utf-8').write(f.write_answer(expected, types=False) + '\n')
    jobs = [(p, m, case) for p in PROMPTS for m in MODELS for case in GOLDEN]
    with cf.ThreadPoolExecutor(10) as ex:
        records = list(ex.map(lambda j: one(j[0], j[1], j[2], picks[j[2]['id']]), jobs))
    by_id = {case['id']: case for case in GOLDEN}
    summary = []
    for rec in records:
        o = outcomes(rec, by_id[rec['case']])
        rec['steps'] = {str(i): {'first': a, 'final': z, 'misses': m} for i, (a, z, m) in o.items()}
        rec['cost'] = sum(cost(rec['model'], x['usage']) for x in rec['calls'])
        summary.append(rec)
    json.dump(summary, open(os.path.join(OUT, 'summary.json'), 'w'), indent=1, ensure_ascii=False)
    for prompt in PROMPTS:
        for model in MODELS:
            rows = [r for r in summary if r['prompt'] == prompt and r['model'] == model]
            n1 = collections.Counter(s['first'] for r in rows for s in r['steps'].values())
            n2 = collections.Counter(s['final'] for r in rows for s in r['steps'].values())
            tin = sum(x['usage'].get('prompt_tokens', 0) for r in rows for x in r['calls'])
            tout = sum(x['usage'].get('completion_tokens', 0) for r in rows for x in r['calls'])
            secs = [sum(x['seconds'] for x in r['calls']) for r in rows]
            print(f'{prompt} {model:<13} first: right {n1["right"]}, fixed {n1["fixed"]}, caught {n1["caught"]}, silent {n1["silent"]}'
                  f' | final: right {n2["right"]}, silent {n2["silent"]}, failed {n2["failed"]}'
                  f' | retries {sum(len(r["calls"]) > 1 for r in rows)}, disagreements {sum(len(r["disagreements"]) for r in rows)}, warnings {sum(len(r["warnings"]) for r in rows)}'
                  f' | {sum(secs):.1f}s (max goal {max(secs):.1f}), tokens {tin:,}/{tout:,}, ${sum(r["cost"] for r in rows):.4f}')
    # what came back (C + the first model): beside the request, and the whole run into rounds/ as history
    for case in GOLDEN:
        src = os.path.join(OUT, 'C', MODELS[0], case['id'])
        folder = home(case['id'])
        for name, dest in (('1.answer.txt', 'answer.txt'), ('2.answer.txt', 'answer.retry.txt')):
            if os.path.exists(os.path.join(src, name)): shutil.copy(os.path.join(src, name), os.path.join(folder, dest))
            elif os.path.exists(os.path.join(folder, dest)): os.remove(os.path.join(folder, dest))
        if not replay:
            shutil.copytree(folder, os.path.join(SHARED, 'rounds', f'round{ROUND}-run{run}', case['id']), dirs_exist_ok=True)
    if not replay: readme(f'round {ROUND}, run {run}', OUT)
    print('wrote', OUT)
