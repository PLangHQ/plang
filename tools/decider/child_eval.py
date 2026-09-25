"""Child eval — stage 3 only: does the model put a condition's body in its `child`?

Every golden case (child_golden.json) is sent with the prompt the builder ships — the system message
build_pr.SYSTEM_SENT (Properties.llm + the Properties.schema instruction, as OpenAi.cs appends it) and
the user message build_pr.user_message() renders (the propertiesUser.template format) — to each model,
RUNS times. The prompt is frozen for the whole run. Raw answers land in runs/child_eval_<stamp>/.

    python3 child_eval.py                       # gpt-5.4-nano and gpt-5.4-mini, 3 runs each
    MODELS=gpt-5.4-nano RUNS=1 python3 child_eval.py
"""
import json, os, re, sys, time, urllib.request, urllib.error, concurrent.futures as cf
import build_pr as b

HERE = os.path.dirname(os.path.abspath(__file__))
GOLDEN = json.load(open(os.path.join(HERE, 'child_golden.json'), encoding='utf-8'))['cases']
if os.environ.get('CASES'):
    GOLDEN = [c for c in GOLDEN if c['id'] in os.environ['CASES'].split(',')]
MODELS = os.environ.get('MODELS', 'gpt-5.4-nano,gpt-5.4-mini').split(',')
RUNS = int(os.environ.get('RUNS', 3))
OUT = os.path.join(HERE, 'runs', 'child_eval_' + time.strftime('%Y%m%d_%H%M%S'))

def goal_of(case):
    return {'name': case['goal'], 'steps': [{'index': i, 'text': s['text'], 'indent': s['indent']}
                                             for i, s in enumerate(case['steps'])]}

def menu_of(case):
    return {int(k): v for k, v in case['menu'].items()}

# ---------------------------------------------------------------- the call (the request OpenAi.cs builds)
def ask(model, user):
    body = json.dumps({'model': model, 'temperature': 0.0, 'max_completion_tokens': 16000,
                       'messages': [{'role': 'system', 'content': b.SYSTEM_SENT},
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
    return misses

def compare_child(expected, got, where, loose_split):
    if not expected:
        return [f'{where}: invented ({json.dumps(got)[:120]})'] if got else []
    if not got:
        return [f'{where}: missing — the body is not in child']
    misses = []
    if len(got) == len(expected):
        for i, (e, g) in enumerate(zip(expected, got)):
            if words(e['text']) != words(g.get('text', '')):
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
def one(model, case, run):
    user = b.user_message(goal_of(case), menu_of(case))
    folder = os.path.join(OUT, model, case['id'])
    os.makedirs(folder, exist_ok=True)
    try:
        raw = ask(model, user)
        json.dump(raw, open(os.path.join(folder, f'{run}.raw.json'), 'w'), indent=1, ensure_ascii=False)
        answer, notes = answer_of(raw)
    except Exception as ex:
        return model, case['id'], run, None, {-1: ([f'call/parse failed: {type(ex).__name__}: {ex}'], [])}, []
    json.dump(answer, open(os.path.join(folder, f'{run}.answer.json'), 'w'), indent=1, ensure_ascii=False)
    return model, case['id'], run, answer, score(case, answer), notes

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    open(os.path.join(OUT, 'system.txt'), 'w').write(b.SYSTEM_SENT)
    for case in GOLDEN:
        open(os.path.join(OUT, f'user.{case["id"]}.txt'), 'w').write(b.user_message(goal_of(case), menu_of(case)))
    jobs = [(m, c, r) for m in MODELS for c in GOLDEN for r in range(1, RUNS + 1)]
    results = []
    with cf.ThreadPoolExecutor(int(os.environ.get('WORKERS', 12))) as ex:
        for res in ex.map(lambda j: one(*j), jobs):
            results.append(res)
            model, cid, run, _, sc, _ = res
            ok = all(not m for m, _ in sc.values())
            print(f'{model:<14} {cid:<22} run {run}: {"YES" if ok else "NO"}', flush=True)
    # What the builder's checks would do with each answer: build.match + the action list's chain rule
    # (build_pr.match mirrors both). A wrong answer they refuse goes to the retry; one they pass is a
    # SILENT miss — it would reach the .pr wrong.
    by_id = {c['id']: c for c in GOLDEN}
    summary = [{'model': m, 'case': c, 'run': r, 'notes': n,
                'caught': b.match(goal_of(by_id[c]), a) if a is not None else ['no answer'],
                'steps': {str(i): {'misses': ms, 'got': [short(x) for x in got]} for i, (ms, got) in sc.items()}}
               for m, c, r, a, sc, n in results]
    json.dump(summary, open(os.path.join(OUT, 'summary.json'), 'w'), indent=1, ensure_ascii=False)
    for model in MODELS:
        rows = [s for s in summary if s['model'] == model]
        steps = sum(len(s['steps']) for s in rows)
        right = sum(1 for s in rows for v in s['steps'].values() if not v['misses'])
        silent = sum(1 for s in rows if not s['caught'] for v in s['steps'].values() if v['misses'])
        caught = sum(1 for s in rows if s['caught'] for v in s['steps'].values() if v['misses'])
        print(f'{model}: first-attempt {right}/{steps} steps; misses caught (→ retry) {caught}; SILENT {silent}')
    print('wrote', OUT)
