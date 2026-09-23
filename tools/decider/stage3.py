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

SYSTEM = """You are the PLang property filler.

Every action in this goal is numbered and its class is already decided. Fill in each action's
properties from the step's own words.

Rules:
- Use the property names exactly as declared on the action's class.
- Omit any optional property the step does not give a value for. Never invent one, never fill one
  with a placeholder, and never copy a default in.
- A value the step states rides as written: a quoted text as that text, a %variable% verbatim, a
  number as a number.
- A value the step implies but does not write is yours to compose — "is not null" means comparing
  against null. A message the step does not give is omitted, not written for it.
- `%!data%` is the previous action's result in the same step; use it only to thread one action's
  output into the next when the step says so.
- A type is a PLang type name: text, number, bool, list, dict, path, date, duration. Omit `type`
  when the value speaks for itself.

Answer with JSON only, one entry per numbered action:
{"actions":[{"index":<the action's number>,"properties":[{"name":"..","value":..,"type":".."}]}]}
Return one entry for EVERY number, in order, with no gaps — an action with nothing to fill gets an
empty list. The count of entries must equal the count of numbered actions."""


def signatures(plan, cat):
    """Each distinct action class used in this goal, written once: its properties and its teaching."""
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
            sh = pp.shape(pinfo['type'])
            allowed += f', an object shaped {sh}' if sh and not opts else ''
            lines.append(f'  - {pname}: {pinfo["type"]}, {opt}{dv}{allowed}')
        for facet in ('notes', 'examples'):
            if d.get(facet): lines.append(f'  {facet}: {d[facet]}')
        out.append('\n'.join(lines))
    return '\n\n'.join(out)


def numbering(goal, plan):
    """Every action in the goal, numbered once. The number is the only thing the answer carries
    back, so the module and the action are never restated and can never be changed."""
    order = []
    for s in goal['steps']:
        for n, (m, a) in enumerate(plan.get(s['index'], [])):
            order.append((s['index'], n, m, a))
    return order


def ask(goal, plan, cat):
    order = numbering(goal, plan)
    at = {(si, n): i + 1 for i, (si, n, _, _) in enumerate(order)}
    lines = []
    for s in goal['steps']:
        acts = plan.get(s['index'], [])
        if not acts: continue
        lines.append('    ' * s.get('indent', 0) + s['text'])
        for n, (m, a) in enumerate(acts):
            lines.append(f'   {at[(s["index"], n)]}. {m}.{a}')
    user = (f'Goal `{goal["name"]}`. Each line is a step, with its numbered actions under it. '
            f'There are {len(order)} actions, numbered 1 to {len(order)}; answer with {len(order)} entries.\n\n'
            + '\n'.join(lines)
            + '\n\nThe action classes used, and their properties:\n\n' + signatures(plan, cat))
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
    return json.loads(txt), time.time() - t0, resp.get('usage', {}), (user, order)


def flatten(answer, order):
    """{(step_index, action_position, PropertyName): value} — the number maps straight back."""
    out = {}
    for a in answer.get('actions', []):
        i = a.get('index')
        if not isinstance(i, int) or not (1 <= i <= len(order)): continue
        si, n, _, _ = order[i - 1]
        for p in a.get('properties') or []:
            out[(si, n, p.get('name'))] = p.get('value')
    return out
