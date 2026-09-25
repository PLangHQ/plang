"""Prompt C vs prompt B on the golden goals — stage 3 with the real decider's picks (v5), not hand menus.

Per round: the decider runs once per goal; its picks feed both prompts, on each model.

    B  PropertiesB.llm + the schema; propertiesUserB's shape with the menu = the picks ≥ 0.5 (names only);
       the answer in JSON; checked by build_pr.match (steps line up, the chain rule, fold)
    C  PropertiesC.llm; prompt_c.user_message_c (the picks with scores, the certain ones pre-filled in
       formal); the answer in formal, parsed by formal.parse_answer; checked by prompt_c.check (fold, the
       match, and the agreement with the decider)

A refused answer is told why and answered again, once (FixProperties' conversation). Per step:
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

HERE = os.path.dirname(os.path.abspath(__file__))
ROUND = os.environ.get('ROUND', '1')
MODELS = os.environ.get('MODELS', 'gpt-5.4-nano,gpt-5.4-mini').split(',')
PROMPTS = os.environ.get('PROMPTS', 'B,C').split(',')
SHARED = '/shared/coder/llm/plang/builder-formal'
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
    """(answer as .pr-shaped JSON for scoring, refusals, warnings, disagreements, answer before fold)."""
    goal = e.goal_of(case)
    if prompt == 'B':
        try:
            answer = json.loads(text)
        except ValueError as ex:
            return None, [f'the answer is not JSON: {ex}'], [], [], None
        refused = b.match(goal, answer)
        before = copy.deepcopy(answer)
        return (b.normalize(goal, answer) if not refused else answer), refused, [], [], before
    try:
        parsed = f.parse_answer(text)
    except f.FormalError as ex:
        line = text.split('\n')[ex.line - 1] if ex.line - 1 < len(text.split('\n')) else ''
        return None, [f'the formal does not parse, {ex} — in the line: {line.strip()}'], [], [], None
    before = {'step': [{'index': i, 'action': copy.deepcopy(parsed[i])} for i in sorted(parsed)]}
    refused, warnings = c.check(goal, picks, parsed)
    agreement = [r for r in refused if 'decider' in r]
    return {'step': [{'index': i, 'action': parsed[i]} for i in sorted(parsed)]}, refused, warnings, agreement, before

RETRY = {'B': 'Your answer was rejected by the validator: {why}\n\nReturn the corrected answer in the same shape.',
         'C': 'Your answer was refused: {why}\n\nAnswer again: the whole goal, one line per step, in formal.'}

def one(prompt, model, case, picks):
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
        o1 = 'right' if not fm else 'caught' if rec['refused1'] else \
             'fixed' if not folded.get(i, (['x'], []))[0] else 'silent'
        lm = final.get(i, (['missing'], []))[0]
        o2 = 'failed' if rec['refused2'] else 'right' if not lm else 'silent'
        out[i] = (o1, o2, fm if fm else lm)
    return out

PRICES = e.PRICES
def cost(model, usage): return e.cost(model, usage)

if __name__ == '__main__':
    cat = h.catalogue()
    d.DUMP = f'{SHARED}/decider-round{ROUND}'
    os.makedirs(OUT, exist_ok=True)
    print(f'round {ROUND}: decider v5 on {len(e.GOLDEN)} goals', flush=True)
    with cf.ThreadPoolExecutor(5) as ex:
        decided = dict(zip([c_['id'] for c_ in e.GOLDEN], ex.map(lambda case: d.one(case, cat), e.GOLDEN)))
    picks = {cid: {s['index']: {a: v['score'] for a, v in s['pick'].items()} for s in r['steps']} for cid, r in decided.items()}
    json.dump({'picks': {k: {str(i): p for i, p in v.items()} for k, v in picks.items()},
               'decider': {cid: {'stage1': r['stage1'], 'stage2': r['stage2']} for cid, r in decided.items()}},
              open(os.path.join(OUT, 'decider.json'), 'w'), indent=1)
    # the rendered requests, for reading
    for prompt in PROMPTS:
        for case in e.GOLDEN:
            system, user = request(prompt, case, picks[case['id']])
            folder = os.path.join(SHARED, f'prompt-{prompt.lower()}-round{ROUND}', case['id'])
            os.makedirs(folder, exist_ok=True)
            open(os.path.join(folder, 'system.txt'), 'w', encoding='utf-8').write(system)
            open(os.path.join(folder, 'user.txt'), 'w', encoding='utf-8').write(user)
    jobs = [(p, m, case) for p in PROMPTS for m in MODELS for case in e.GOLDEN]
    with cf.ThreadPoolExecutor(10) as ex:
        records = list(ex.map(lambda j: one(j[0], j[1], j[2], picks[j[2]['id']]), jobs))
    by_id = {case['id']: case for case in e.GOLDEN}
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
    print('wrote', OUT)
