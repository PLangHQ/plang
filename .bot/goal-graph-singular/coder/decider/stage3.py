"""Stage 3 — parameters, by LLM, one request per goal.

Stages 1 and 2 have already fixed each step's module and action. What is left is filling the
parameters, and a per-step request pays for every method signature again. Grouped, each action's
signature is written once however many steps call it.

The model is asked for the parameters ONLY — never the module or action, which are already
decided, and never a re-reading of the step's intent.
"""
import json, os, re, sys, time, urllib.request, urllib.error, collections
import harness as h, params as pp

KEY = os.environ['OPENAI_API_KEY']
URL = 'https://api.openai.com/v1/chat/completions'
MODEL = os.environ.get('STAGE3_MODEL', 'gpt-5.4-nano')

SYSTEM = """You are the PLang parameter filler.

Each step's module and action are already decided — do not change them, do not add actions, do not
drop any. Your only job is to fill each action's parameters from the step's own words.

Rules:
- Use the action's declared parameter names exactly.
- Omit any optional parameter the step does not give a value for. Never invent one, never fill one
  with a placeholder, and never copy a default in.
- A value the step states rides as written: a quoted text as that text, a %variable% verbatim, a
  number as a number.
- A value the step implies but does not write is yours to compose — "is not null" means comparing
  against null; a message the step does not give is omitted, not written for it.
- `%!data%` is the previous action's result in the same step; use it only to thread one action's
  output into the next when the step says so.
- Types are PLang type names (text, number, bool, list, dict, path, date, duration, object is NOT
  a plang type). Omit `type` when the value speaks for itself.

Answer with JSON only: {"steps":[{"index":<n>,"actions":[{"module":"..","action":"..",
"parameters":[{"name":"..","value":..,"type":".."}]}]}]}"""


def signatures(plan, cat):
    """Each distinct action used in this goal, written once: its parameters and its teaching."""
    out = []
    for module, action in sorted({x for acts in plan.values() for x in acts}):
        decl = pp.parameters(module, action)
        if decl is None: continue
        lines = [f'{module}.{action}']
        d = cat.get(module, {}).get('actions', {}).get(action, {})
        if d.get('description'): lines.append(f'  {d["description"]}')
        for pname, pinfo in decl.items():
            opt = 'optional' if pinfo['optional'] else 'REQUIRED'
            dv = f', default {pinfo["default"]}' if pinfo.get('default') else ''
            opts = pp.options(pinfo['type'])
            allowed = f', one of: {", ".join(list(opts)[:20])}' if opts else ''
            lines.append(f'  - {pname}: {pinfo["type"]}, {opt}{dv}{allowed}')
        for facet in ('notes', 'examples'):
            if d.get(facet): lines.append(f'  {facet}: {d[facet]}')
        out.append('\n'.join(lines))
    return '\n\n'.join(out)


def ask(goal, plan, cat):
    steps = []
    for s in goal['steps']:
        acts = plan.get(s['index'], [])
        if not acts: continue
        steps.append(f'step {h.step_no(s)}: ' + '    ' * s.get('indent', 0) + s['text']
                     + '\n   actions, in order: ' + ', '.join(f'{m}.{a}' for m, a in acts))
    user = (f'Goal `{goal["name"]}`.\n\n' + '\n'.join(steps)
            + '\n\nThe actions used, and their parameters:\n\n' + signatures(plan, cat))
    body = json.dumps({'model': MODEL,
                       'messages': [{'role': 'system', 'content': SYSTEM},
                                    {'role': 'user', 'content': user}],
                       'response_format': {'type': 'json_object'}}).encode()
    req = urllib.request.Request(URL, data=body, method='POST', headers={
        'Authorization': f'Bearer {KEY}', 'Content-Type': 'application/json'})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=300) as r:
        resp = json.loads(r.read())
    txt = resp['choices'][0]['message']['content']
    return json.loads(txt), time.time() - t0, resp.get('usage', {}), user


def flatten(answer):
    """{(step_index, module, action, ParamName): value} from the model's reply."""
    out = {}
    for s in answer.get('steps', []):
        i = s.get('index')
        i = i - 1 if isinstance(i, int) else i          # the prompt numbers from 1
        for a in s.get('actions', []):
            for p in a.get('parameters') or []:
                out[(i, a.get('module'), a.get('action'), p.get('name'))] = p.get('value')
    return out
