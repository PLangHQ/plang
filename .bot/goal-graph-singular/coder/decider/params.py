"""Stage 3 — parameters. Reads each action's declared parameters off the C# handler, offers the
candidate values taken from the step, and asks the decider one choice question per parameter.

Labels are the parameter values in the .pr. A value the step does not contain (a dict, a list of
objects, a computed expression) is not a choice at all and is expected to fall to the llm — those
are counted separately, never as a miss.
"""
import json, os, re, glob, sys, collections
import harness as h

ROOT = h.ROOT

# ---------------------------------------------------------------- the declared parameters
PROP = re.compile(r'public\s+partial\s+data\.@this(?:<(?P<type>[^>]*(?:<[^>]*>)?[^>]*)>)?(?P<opt>\?)?\s+(?P<name>\w+)\s*\{\s*get;\s*init;\s*\}')
DEFAULT = re.compile(r'\[Default\(([^\]]*)\)\]\s*$')

def parameters(module, action):
    """Every parameter an action declares, with its plang-ish type and whether it is optional."""
    for path in (f'{ROOT}/PLang/app/module/action/{module}/{action}.cs',
                 f'{ROOT}/PLang/app/module/action/{module}/{action.lower()}.cs'):
        if os.path.exists(path): break
    else:
        hits = [p for p in glob.glob(f'{ROOT}/PLang/app/module/action/{module}/*.cs')
                if re.search(rf'\[Action\("{re.escape(action)}"', open(p, encoding='utf-8').read(), re.I)]
        if not hits: return None
        path = hits[0]
    src = open(path, encoding='utf-8').read()
    out = {}
    lines = src.split('\n')
    for i, line in enumerate(lines):
        m = PROP.search(line)
        if not m: continue
        has_default = any(DEFAULT.search(l) for l in lines[max(0, i - 3):i])
        t = (m.group('type') or 'item').split('.')[-1].replace('@this', '').strip('<> ') or 'item'
        out[m.group('name')] = {'type': t, 'optional': bool(m.group('opt')) or has_default}
    return out

# ---------------------------------------------------------------- candidates from the step
QUOTED = re.compile(r"'([^']*)'|\"([^\"]*)\"")
VARIABLE = re.compile(r'%[^%\s]+%')
NUMBER = re.compile(r'(?<![\w%.])-?\d+(?:\.\d+)?(?![\w%])')

def candidates(text):
    """Every literal the step contains, offered as-is. No parsing of meaning — these are the
    pieces of the sentence, and the decider decides which piece is which parameter."""
    out = {}
    for m in QUOTED.finditer(text):
        v = m.group(1) if m.group(1) is not None else m.group(2)
        out[v] = f'the text "{v}" written in the step'
    for v in VARIABLE.findall(text):
        out[v] = f'the variable {v}'
    for v in NUMBER.findall(text):
        out.setdefault(v, f'the number {v} written in the step')
    for w in ('true', 'false'):
        if re.search(rf'\b{w}\b', text, re.I): out[w] = f'the value {w} written in the step'
    return out

# ---------------------------------------------------------------- ask
NONE = '__none__'

def ask_params(goal, cat, plan):
    """plan: {step_index: [(module, action)]} — what stages 1+2 decided."""
    state = h.state_for(goal, cat, modules={m for acts in plan.values() for m, _ in acts})
    qs = {}
    meta = {}
    for s in goal['steps']:
        cands = candidates(s['text'])
        if not cands: continue
        for module, action in plan.get(s['index'], []):
            decl = parameters(module, action)
            if not decl: continue
            for pname, pinfo in decl.items():
                crit = dict(cands)
                crit[NONE] = ('none of these — the value is not written in this step'
                              if not pinfo['optional'] else
                              'none of these — this parameter is not given in this step')
                key = f's{s["index"]}_{module}.{action}_{pname}'
                qs[key] = {'type': 'choice',
                           'instructions': (f'Step {h.step_no(s)} of this goal is `{s["text"].strip()}`. It calls '
                                            f'`{module}.{action}`. Parameter `{pname}` is {"optional" if pinfo["optional"] else "required"}, '
                                            f'of type {pinfo["type"]}. Which of these is the value of `{pname}` in step {h.step_no(s)}?'),
                           'criteria': crit}
                meta[key] = (s['index'], module, action, pname)
    if not qs: return {}, 0.0, 0
    out = {}; secs = 0.0; n = 0
    keys = list(qs)
    for i in range(0, len(keys), 120):          # keep a request from getting silly
        chunk = {k: qs[k] for k in keys[i:i + 120]}
        resp, t, _ = h.ask(state, chunk)
        secs += t; n += len(chunk)
        for k, a in resp['answers'].items():
            out[meta[k]] = (a.get('choice'), a.get('confidence'))
    return out, secs, n

# ---------------------------------------------------------------- labels
def label_params(step):
    """{(module, action, ParamName): value} straight off the .pr."""
    out = {}
    def walk(a):
        m, an = a.get('module'), a.get('action') or a.get('name')
        for p in a.get('parameter') or a.get('parameters') or []:
            out[(m, an, p.get('name'))] = p.get('value')
        for mod in a.get('modifier') or a.get('modifiers') or []: walk(mod)
        for c in a.get('child') or []:
            for ca in c.get('action') or c.get('actions') or []: walk(ca)
    for a in step.get('_raw', []): walk(a)
    return out
