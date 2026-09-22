"""Stage 3, second shape. One request per goal, but nothing to count and nothing to cross-reference.

What changed from the first attempt:
  - the answer format and a worked example come FIRST, not after the rules
  - each action's properties are listed INLINE under its own step, as blanks to fill
  - the answer is an object keyed by the action number, not an array of {name,value} pairs
  - class teaching (notes, examples) rides ONCE at the end, as reference, not per step
"""
import json, os, time, urllib.request
import harness as h, params as pp

KEY = os.environ['OPENAI_API_KEY']
URL = 'https://api.openai.com/v1/chat/completions'
MODEL = os.environ.get('STAGE3_MODEL', 'gpt-5.4-nano')

SYSTEM = '''You fill in the properties of PLang actions.

Each step of a goal is written in plain language. Under each step are its actions, already chosen,
each with a number and its properties listed as blanks. Fill the blanks from the step's own words.

Answer with JSON only — one key per action number, one entry per property you fill:

  {"1": {"Name": "%path%", "Value": "/"},
   "2": {"GoalName": {"name": "SendMail", "parameter": [{"name": "to", "value": "%email%"}]}},
   "3": {}}

Worked example. Given:

  step 1: read 'notes.txt', write to %content%
     1. file.read
        Path (path, required)
        ResolveVariables (bool, optional, default false)
     2. variable.set
        Name (variable, required)
        Value (item, required)

the answer is:

  {"1": {"Path": "notes.txt"},
   "2": {"Name": "%content%", "Value": "%!data%"}}

`ResolveVariables` is left out because the step does not ask for it. `Value` is `%!data%` because
that is the result of the action before it in the same step.

Filling a blank:
- A value the step writes goes in as written — a quoted text as that text, a %variable% with its
  % signs, a number as a number.
- A required blank the step does not write is yours to work out from what the step means.
- Leave an optional blank out unless the step asks for it. Never put in a placeholder and never
  repeat a default.
- A `variable` blank is a variable name with its % signs.
- Give a `type` only when the step forces one (`as text`, `(json)`); otherwise the value speaks for
  itself. Type names are PLang's: text, number, bool, list, dict, path, date, duration.'''


def render(goal, plan, cat):
    order = []
    lines = []
    for s in goal['steps']:
        acts = plan.get(s['index'], [])
        if not acts: continue
        lines.append(f'step {h.step_no(s)}: ' + '    ' * s.get('indent', 0) + s['text'])
        for m, a in acts:
            order.append((s['index'], len(order), m, a))
            n = len(order)
            lines.append(f'   {n}. {m}.{a}')
            for pname, pinfo in (pp.parameters(m, a) or {}).items():
                bits = [pinfo['type'], 'required' if not pinfo['optional'] else 'optional']
                if pinfo.get('default'): bits.append(f'default {pinfo["default"]}')
                opts = pp.options(pinfo['type'])
                if opts: bits.append('one of: ' + ', '.join(list(opts)[:20]))
                sh = pp.shape(pinfo['type'])
                if sh: bits.append(f'an object {sh}')
                lines.append(f'      {pname} ({", ".join(bits)})')
    # class teaching once, at the end, for the classes actually used
    ref = []
    for m, a in sorted({(m, a) for _, _, m, a in order}):
        d = cat.get(m, {}).get('actions', {}).get(a, {})
        txt = '\n'.join(x for x in (d.get('description'), d.get('notes'), d.get('examples')) if x)
        if txt: ref.append(f'--- {m}.{a}\n{txt}')
    user = (f'Goal `{goal["name"]}`.\n\n' + '\n'.join(lines)
            + ('\n\nReference — what these action classes are for:\n\n' + '\n\n'.join(ref) if ref else ''))
    return user, order


def ask(goal, plan, cat):
    user, order = render(goal, plan, cat)
    body = json.dumps({'model': MODEL,
                       'messages': [{'role': 'system', 'content': SYSTEM},
                                    {'role': 'user', 'content': user}],
                       'response_format': {'type': 'json_object'}}).encode()
    req = urllib.request.Request(URL, data=body, method='POST', headers={
        'Authorization': f'Bearer {KEY}', 'Content-Type': 'application/json'})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=300) as r:
        resp = json.loads(r.read())
    return json.loads(resp['choices'][0]['message']['content']), time.time() - t0, resp.get('usage', {}), (user, order)


def flatten(answer, order):
    out = {}
    for k, props in answer.items():
        try: i = int(k)
        except (TypeError, ValueError): continue
        if not (1 <= i <= len(order)): continue
        si, _, _, _ = order[i - 1]
        n = sum(1 for j in range(i - 1) if order[j][0] == si)      # position within its step
        for pname, v in (props or {}).items():
            out[(si, n, pname)] = v
    return out
