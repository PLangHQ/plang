"""Decider harness — measures the two-stage typesafe pipeline against Tests/**/.pr as labels.

stage 1  noul per (step, module)           -> module set per step      vs .pr modules
stage 2  choice per (step, module-in-set)  -> action per (step,module) vs .pr action

Raw answers are written to runs/<name>.jsonl so threshold sweeps and re-analysis need no API.
"""
import json, glob, os, sys, time, urllib.request, urllib.error, collections, concurrent.futures as cf

ROOT = '/workspace/plang'
URL = 'https://api.typesafe.ai/v1/systemone'
MODEL = 'jev-latest'
KEY = open('/shared/hopkaup/secrets/typesafe.txt').read().strip()
OUT = os.path.join(os.path.dirname(__file__), 'runs')
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- catalogue
def first_line(p):
    for line in open(p, encoding='utf-8'):
        s = line.strip()
        if s: return s
    return ''

def catalogue():
    mods = {}
    for d in sorted(glob.glob(f'{ROOT}/os/system/modules/*/')):
        name = os.path.basename(d.rstrip('/'))
        mdesc = os.path.join(d, 'module.description.md')
        if not os.path.exists(mdesc): continue
        acts = {}
        for a in sorted(glob.glob(d + '*.description.md')):
            an = os.path.basename(a)[:-len('.description.md')]
            if an == 'module': continue
            acts[an] = first_line(a)
        mods[name] = {'description': first_line(mdesc), 'actions': acts}
    return mods

# ---------------------------------------------------------------- dataset
def goals(limit=None, seed=0):
    out = []
    files = sorted(glob.glob(f'{ROOT}/Tests/**/.build/*.pr', recursive=True))
    for f in files:
        try: d = json.load(open(f, encoding='utf-8'))
        except Exception: continue
        sts = d.get('step') or d.get('steps') or []
        steps = []
        for st in sts:
            acts = st.get('action') or st.get('actions') or []
            label = [(a.get('module'), a.get('action') or a.get('name')) for a in acts if a.get('module')]
            if not label or not st.get('text'): continue
            steps.append({'index': len(steps), 'text': st['text'], 'label': label})
        if steps:
            out.append({'file': os.path.relpath(f, ROOT), 'name': d.get('name', ''), 'steps': steps})
    if seed:
        import random; random.Random(seed).shuffle(out)
    return out[:limit] if limit else out

# ---------------------------------------------------------------- api
def ask(state, questions, retries=4):
    body = json.dumps({'state': state, 'model': MODEL, 'questions': questions}).encode()
    for attempt in range(retries):
        if attempt: time.sleep(2 ** attempt)
        req = urllib.request.Request(URL, data=body, method='POST', headers={
            'Authorization': f'Bearer {KEY}', 'Content-Type': 'application/json', 'User-Agent': 'plang decider harness'})
        t0 = time.time()
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                resp = json.loads(r.read())
            return resp, time.time() - t0, len(body)
        except urllib.error.HTTPError as e:
            txt = e.read().decode(errors='replace')
            if e.code in (408, 429, 500, 502, 503, 504, 529): continue
            raise RuntimeError(f'{e.code}: {txt[:300]}')
    raise RuntimeError('retries exhausted')

# ---------------------------------------------------------------- stages
def state_for(goal, cat):
    return {
        'goal': goal['name'],
        'steps': [{'index': s['index'], 'text': s['text']} for s in goal['steps']],
        'modules': {m: v['description'] for m, v in cat.items()},
    }

def stage1(goal, cat):
    qs = {}
    for s in goal['steps']:
        for m in cat:
            qs[f's{s["index"]}_{m}'] = {
                'type': 'noul',
                'instructions': f'Does `steps[{s["index"]}].text` use the plang module `{m}` (described at `modules.{m}`) to do what it says?'}
    resp, secs, nbytes = ask(state_for(goal, cat), qs)
    probs = collections.defaultdict(dict)
    for k, a in resp['answers'].items():
        i, m = k[1:].split('_', 1)
        probs[int(i)][m] = a.get('noul')
    return probs, secs, nbytes, len(qs), resp.get('usage', {})

def stage2(goal, cat, chosen):   # chosen: {step_index: [modules]}
    qs = {}
    for s in goal['steps']:
        for m in chosen.get(s['index'], []):
            acts = cat[m]['actions']
            if len(acts) <= 1: continue
            qs[f's{s["index"]}_{m}'] = {
                'type': 'choice',
                'instructions': f'`steps[{s["index"]}].text` uses the plang module `{m}`. Which action of `{m}` does it call?',
                'criteria': acts}
    if not qs: return {}, 0.0, 0, 0, {}
    resp, secs, nbytes = ask(state_for(goal, cat), qs)
    out = {}
    for k, a in resp['answers'].items():
        i, m = k[1:].split('_', 1)
        out[(int(i), m)] = (a.get('choice'), a.get('confidence'))
    for s in goal['steps']:                       # single-action modules need no question
        for m in chosen.get(s['index'], []):
            if len(cat[m]['actions']) == 1: out[(s['index'], m)] = (next(iter(cat[m]['actions'])), 1.0)
    return out, secs, nbytes, len(qs), resp.get('usage', {})

# ---------------------------------------------------------------- run
def run(name, limit, seed, threshold=0.5, workers=4):
    cat = catalogue()
    data = goals(limit, seed)
    path = os.path.join(OUT, f'{name}.jsonl')
    print(f'{len(data)} goals, {sum(len(g["steps"]) for g in data)} steps, {len(cat)} modules -> {path}', flush=True)

    def one(goal):
        rec = {'goal': goal['name'], 'file': goal['file'], 'steps': goal['steps']}
        try:
            probs, s1, b1, q1, u1 = stage1(goal, cat)
            chosen = {i: [m for m, p in probs[i].items() if p is not None and p >= threshold] for i in probs}
            acts, s2, b2, q2, u2 = stage2(goal, cat, chosen)
            rec.update(stage1={'probs': {str(i): probs[i] for i in probs}, 'secs': s1, 'bytes': b1, 'questions': q1, 'usage': u1},
                       stage2={'choice': {f'{i}|{m}': v for (i, m), v in acts.items()}, 'secs': s2, 'bytes': b2, 'questions': q2, 'usage': u2})
        except Exception as e:
            rec['error'] = str(e)
        return rec

    with open(path, 'w') as f, cf.ThreadPoolExecutor(workers) as ex:
        for n, rec in enumerate(ex.map(one, data), 1):
            f.write(json.dumps(rec) + '\n'); f.flush()
            if n % 10 == 0 or n == len(data): print(f'  {n}/{len(data)}', flush=True)
    return path

if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else 'sample'
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else 20
    seed = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    run(name, limit, seed)
