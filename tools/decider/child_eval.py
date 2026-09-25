"""Stage-3 eval: every golden goal (child_golden.json, one request per goal, scored per step) is sent
with each prompt under test to each model, RUNS times.

    A  the prompt before B, frozen in prompt_a/ (Properties.llm + the schema; the propertiesUser.template
       user message as build_pr.user_message rendered it)
    B  PropertiesB.llm + the schema, and build_pr.user_message_b (propertiesUserB.template)

The schema rides in the system message, as OpenAi.cs appends it. Raw answers land in
runs/child_eval_<stamp>/<prompt>/<model>/<goal>/.

    python3 child_eval.py                                  # A and B, nano and mini, 1 run each
    PROMPTS=B MODELS=gpt-5.4-nano RUNS=3 python3 child_eval.py
"""
import json, os, re, sys, time, copy, collections, urllib.request, urllib.error, concurrent.futures as cf
import build_pr as b

HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = json.load(open(os.path.join(HERE, 'child_golden.json'), encoding='utf-8'))['cases']
if os.environ.get('CASES'):
    GOLDEN = [c for c in GOLDEN if c['id'] in os.environ['CASES'].split(',')]
MODELS = os.environ.get('MODELS', 'gpt-5.4-nano,gpt-5.4-mini').split(',')
RUNS = int(os.environ.get('RUNS', 1))
OUT = os.path.join(HERE, 'runs', 'child_eval_' + time.strftime('%Y%m%d_%H%M%S'))

def goal_of(case):
    return {'name': case['goal'], 'comment': case.get('comment'),
            'steps': [{'index': i, 'text': s['text'], 'indent': s['indent'], 'comment': s.get('comment')}
                      for i, s in enumerate(case['steps'])]}

def menu_of(case):
    return {int(k): v for k, v in case['menu'].items()}

# ---------------------------------------------------------------- the prompts under test
# A is the prompt as it stood before B (run 5's): frozen in prompt_a/, because the notes it prints
# have since been rewritten for B. B renders live — PropertiesB.llm + propertiesUserB.template.
PROMPTS = os.environ.get('PROMPTS', 'A,B').split(',')
FROZEN_A = os.path.join(HERE, 'prompt_a')

def request(prompt, case):
    """(system, user) exactly as sent for this prompt and goal."""
    if prompt == 'A':
        return (open(os.path.join(FROZEN_A, 'system.txt'), encoding='utf-8').read(),
                open(os.path.join(FROZEN_A, f'{case["id"]}.user.txt'), encoding='utf-8').read())
    return b.SYSTEM_B_SENT, b.user_message_b(goal_of(case), menu_of(case))

# ---------------------------------------------------------------- the call (the request OpenAi.cs builds)
def ask(model, system, user):
    body = json.dumps({'model': model, 'temperature': 0.0, 'max_completion_tokens': 16000,
                       'messages': [{'role': 'system', 'content': system},
                                    {'role': 'user', 'content': user}]}).encode()
    for attempt in range(4):
        req = urllib.request.Request('https://api.openai.com/v1/chat/completions', data=body,
                                     headers={'Authorization': f'Bearer {b.OPENAI_KEY}',
                                              'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            if e.code in (408, 429, 500, 502, 503, 529) and attempt < 3:
                time.sleep(2 ** attempt); continue
            raise

def answer_of(raw):
    """The JSON the model wrote. A fenced block is unwrapped and RECORDED as a deviation."""
    content = raw['choices'][0]['message']['content']
    notes = []
    m = re.fullmatch(r'\s*```(?:json)?\s*(.*?)\s*```\s*', content, re.S)
    if m:
        content = m.group(1); notes.append('answer wrapped in a ``` fence')
    return json.loads(content), notes

# ---------------------------------------------------------------- scoring
def words(t):
    return re.sub(r'\s+', ' ', re.sub(r'["\'`]', '', str(t))).strip(' ,.;').lower()

def same_value(expected, got):
    if isinstance(expected, dict) and '$oneOf' in expected:
        return any(same_value(e, got) for e in expected['$oneOf'])
    if isinstance(expected, dict) and 'module' in expected:   # an action-typed property holds an action
        return isinstance(got, dict) and not compare_actions([expected], [got], '')
    if isinstance(expected, dict) and isinstance(got, dict):
        return expected.keys() == got.keys() and all(same_value(v, got[k]) for k, v in expected.items())
    if isinstance(expected, bool) or isinstance(got, bool):
        return expected is got
    if isinstance(expected, (int, float)) and isinstance(got, (int, float)):
        return float(expected) == float(got)
    if isinstance(expected, list) and isinstance(got, list):
        if expected and isinstance(expected[0], dict):   # argument rows: name + value
            return len(expected) == len(got) and all(
                isinstance(g, dict) and e['name'] == g.get('name') and same_value(e['value'], g.get('value'))
                for e, g in zip(expected, got))
        return len(expected) == len(got) and all(same_value(e, g) for e, g in zip(expected, got))
    return expected == got

def rows(action):
    raw = action.get('property') or []
    if isinstance(raw, dict): return raw
    return {r.get('name'): r.get('value') for r in raw if isinstance(r, dict)}

def compare_actions(expected, got, where, loose_split=False):
    """Every difference between an expected action list and the answered one."""
    misses = []
    if not isinstance(got, list):
        return [f'{where}: action is not a list']
    if len(expected) != len(got):
        misses.append(f'{where}: {len(got)} actions, expected {len(expected)} '
                      f'({", ".join(short(a) for a in got)})')
    for i, (e, g) in enumerate(zip(expected, got)):
        at = f'{where}[{i}]'
        if (e['module'], e['name']) != (g.get('module'), g.get('name')):
            misses.append(f'{at}: {g.get("module")}.{g.get("name")}, expected {e["module"]}.{e["name"]}')
            continue
        got_rows = rows(g)
        for name, value in (e.get('property') or {}).items():
            if name not in got_rows:
                misses.append(f'{at} {e["module"]}.{e["name"]}: {name} missing (expected {value!r})')
            elif not same_value(value, got_rows[name]):
                misses.append(f'{at} {e["module"]}.{e["name"]}: {name} = {got_rows[name]!r}, expected {value!r}')
        for name in got_rows:
            if name not in (e.get('property') or {}):
                misses.append(f'{at} {e["module"]}.{e["name"]}: extra {name} = {got_rows[name]!r}')
        misses += compare_child(e.get('child'), g.get('child'), f'{at} child', e.get('note', '').startswith('one child step'))
        misses += compare_modifiers(e.get('modifier') or [], g.get('modifier') or [], f'{at} {e["module"]}.{e["name"]} modifier')
    return misses

def compare_modifiers(expected, got, where):
    """A modifier is an action that wraps the one it sits on; its recovery is a list of actions."""
    if not isinstance(got, list): return [f'{where}: not a list']
    misses = compare_actions(expected, got, where) if expected or got else []
    for i, (e, g) in enumerate(zip(expected, got)):
        if isinstance(g, dict) and (e.get('recovery') or g.get('recovery')):
            misses += compare_actions(e.get('recovery') or [], g.get('recovery') or [], f'{where}[{i}] recovery')
    return misses

# A child's text is compared with the golden's words — except for an answer in formal, where a child's
# text is its body's formal (formal has no place for the body's words), so the actions alone are scored.
CHILD_TEXT = True

def compare_child(expected, got, where, loose_split):
    if not expected:
        return [f'{where}: invented ({json.dumps(got)[:120]})'] if got else []
    if not got:
        return [f'{where}: missing — the body is not in child']
    misses = []
    if len(got) == len(expected):
        for i, (e, g) in enumerate(zip(expected, got)):
            if CHILD_TEXT and words(e['text']) != words(g.get('text', '')):
                misses.append(f'{where}[{i}] text {g.get("text")!r}, expected {e["text"]!r}')
            misses += compare_actions(e['action'], g.get('action') or [], f'{where}[{i}]')
    elif loose_split and len(expected) == 1:
        # One branch split into one child step per action: each text is part of the branch's words,
        # and the actions, flattened, are the branch's actions.
        for i, g in enumerate(got):
            if not words(g.get('text', '')) or words(g.get('text', '')) not in words(expected[0]['text']):
                misses.append(f'{where}[{i}] text {g.get("text")!r} is not part of {expected[0]["text"]!r}')
        flat = [a for g in got for a in (g.get('action') or [])]
        misses += compare_actions(expected[0]['action'], flat, f'{where}(flattened)')
    else:
        misses.append(f'{where}: {len(got)} child steps, expected {len(expected)}')
    return misses

def short(a):
    if not isinstance(a, dict): return repr(a)
    r = rows(a)
    props = ','.join(f'{k}={json.dumps(v, ensure_ascii=False)}' for k, v in r.items())
    s = f'{a.get("module")}.{a.get("name")}({props})'
    if a.get('child'):
        s += '{' + ' | '.join(f'"{c.get("text")}": ' + '; '.join(short(x) for x in c.get('action') or [])
                              for c in a['child']) + '}'
    return s

def score(case, answer):
    """(misses per step index, the answered actions per step)."""
    by_index = {}
    for e in answer.get('step', []) if isinstance(answer, dict) else []:
        by_index[e.get('index')] = e.get('action') or []
    result = {}
    for k, expected in case['expect'].items():
        i = int(k)
        if i not in by_index:
            result[i] = ([f'step {i}: not in the answer'], [])
            continue
        result[i] = (compare_actions(expected, by_index[i], f'step {i}'), by_index[i])
    return result

# ---------------------------------------------------------------- run
def one(prompt, model, case, run):
    system, user = request(prompt, case)
    folder = os.path.join(OUT, prompt, model, case['id'])
    os.makedirs(folder, exist_ok=True)
    started = time.time()
    try:
        raw = ask(model, system, user)
        seconds = time.time() - started   # wall-clock of the call, retries included
        json.dump(raw, open(os.path.join(folder, f'{run}.raw.json'), 'w'), indent=1, ensure_ascii=False)
        answer, notes = answer_of(raw)
    except Exception as ex:
        return prompt, model, case['id'], run, None, {-1: ([f'call/parse failed: {type(ex).__name__}: {ex}'], [])}, [], None, {}
    json.dump(answer, open(os.path.join(folder, f'{run}.answer.json'), 'w'), indent=1, ensure_ascii=False)
    return prompt, model, case['id'], run, answer, score(case, answer), notes, seconds, raw.get('usage') or {}

# USD per 1M tokens (input, cached input, output) — the table OpenAi.cs prices a call with.
PRICES = {'gpt-5.4-nano': (0.20, 0.02, 1.25), 'gpt-5.4-mini': (0.75, 0.075, 4.50), 'gpt-5.4': (2.50, 0.25, 15.00)}

def cost(model, usage):
    """One call's cost in USD, the way OpenAi.cs computes it: cached input at the cached rate."""
    price_in, price_cached, price_out = PRICES[model]
    prompt = usage.get('prompt_tokens', 0)
    cached = (usage.get('prompt_tokens_details') or {}).get('cached_tokens', 0)
    return ((prompt - cached) * price_in + cached * price_cached + usage.get('completion_tokens', 0) * price_out) / 1e6

def percentile(values, p):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(round(p * (len(ordered) - 1))))] if ordered else 0

if __name__ == '__main__':
    for prompt in PROMPTS:
        os.makedirs(os.path.join(OUT, prompt), exist_ok=True)
        for case in GOLDEN:
            system, user = request(prompt, case)
            open(os.path.join(OUT, prompt, 'system.txt'), 'w').write(system)
            open(os.path.join(OUT, prompt, f'user.{case["id"]}.txt'), 'w').write(user)
    jobs = [(p, m, c, r) for p in PROMPTS for m in MODELS for c in GOLDEN for r in range(1, RUNS + 1)]
    results = []
    with cf.ThreadPoolExecutor(int(os.environ.get('WORKERS', 12))) as ex:
        for res in ex.map(lambda j: one(*j), jobs):
            results.append(res)
            prompt, model, cid, run, _, sc, _, _, _ = res
            ok = all(not m for m, _ in sc.values())
            print(f'{prompt} {model:<14} {cid:<16} run {run}: {"YES" if ok else "NO"}', flush=True)
    # What the builder's checks would do with each answer: build.match + the action list's chain rule
    # (build_pr.match mirrors both). A wrong answer they refuse goes to the retry; one they pass is a
    # SILENT miss — it would reach the .pr wrong.
    # A wrong answer the builder's normalization repairs (build.fold drops a child over an indented
    # body) is FIXED — the .pr comes out right with no retry.
    by_id = {c['id']: c for c in GOLDEN}
    summary = []
    for p, m, c, r, a, sc, n, seconds, usage in results:
        caught = b.match(goal_of(by_id[c]), a) if a is not None else ['no answer']
        normalized = score(by_id[c], b.normalize(goal_of(by_id[c]), copy.deepcopy(a))) if a is not None and not caught else {}
        steps = {}
        for i, (ms, got) in sc.items():
            outcome = 'right' if not ms else 'caught' if caught else \
                      'fixed' if i in normalized and not normalized[i][0] else 'silent'
            steps[str(i)] = {'misses': ms, 'got': [short(x) for x in got], 'outcome': outcome}
        summary.append({'prompt': p, 'model': m, 'case': c, 'run': r, 'notes': n, 'caught': caught, 'steps': steps,
                        'seconds': seconds, 'usage': usage, 'cost': cost(m, usage) if usage else None})
    json.dump(summary, open(os.path.join(OUT, 'summary.json'), 'w'), indent=1, ensure_ascii=False)
    for prompt, model in [(p, m) for p in PROMPTS for m in MODELS]:
        rows = [s for s in summary if s['model'] == model and s['prompt'] == prompt]
        count = collections.Counter(v['outcome'] for s in rows for v in s['steps'].values())
        total = sum(count.values())
        seconds = [s['seconds'] for s in rows if s['seconds'] is not None]
        prompt = [s['usage'].get('prompt_tokens', 0) for s in rows if s['usage']]
        answer = [s['usage'].get('completion_tokens', 0) for s in rows if s['usage']]
        costs = [s['cost'] for s in rows if s['cost'] is not None]
        print(f'{prompt} {model}: first-attempt {count["right"]}/{total} steps; caught (→ retry) {count["caught"]}; '
              f'fixed by the builder {count["fixed"]}; SILENT {count["silent"]}')
        print(f'    latency s: median {percentile(seconds, .5):.1f}, p90 {percentile(seconds, .9):.1f}, max {max(seconds, default=0):.1f}'
              f' | tokens: prompt median {percentile(prompt, .5)}, answer median {percentile(answer, .5)} (max {max(answer, default=0)})'
              f' | cost per goal: median ${percentile(costs, .5):.5f}, total ${sum(costs):.4f}')
    print('wrote', OUT)
