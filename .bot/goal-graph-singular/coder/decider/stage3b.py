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

Each step of a goal is written in plain language. Under each step are the actions it uses, with
their properties listed as blanks.

That list says WHICH actions the step uses. It does not say in what order they run, and it does
not say how many times each one runs. You decide both, by reading the step.

An action listed once may run more than once. What repeats is the work the step names more than
once, never the clause that wraps it: `call A, on error call B` names two goals, so `goal.call`
appears twice and `error.handle` — the one `on error` clause — appears once.

Answer with JSON only: one key per step, holding that step's actions IN THE ORDER THEY RUN. Each
entry names which of that step's listed actions it is, and fills its blanks. You may only name an
action that step was given; never any other.

Worked example. Given:

  step 1: wait 2 seconds
     timer.sleep
        Ms (number, required)
  step 2: add 5 and %bonus%, write to %total%
     math.add
        A (item, required)
        B (item, required)
        Overflow (Overflow, optional, default Promote)
     variable.set
        Name (variable, required)
        Value (item, required)
        Type (item, optional)
        AsDefault (bool, optional, default false)
  step 3: call Save id=%orderId%, on error call Rollback
     goal.call
        GoalName (GoalCall, required, an object { "name": the goal's name as text, "parameter": a list of { "name", "value" } — one per argument the step passes to that goal, omitted when it passes none })
     error.handle
        RetryCount (number, optional)
        IgnoreError (bool, optional, default false)

the answer is:

  {"1": [{"action": "timer.sleep", "properties": {"Ms": 2000}}],
   "2": [{"action": "math.add",    "properties": {"A": 5, "B": "%bonus%"}},
         {"action": "variable.set","properties": {"Name": "%total%", "Value": "%!data%"}}],
   "3": [{"action": "goal.call",   "properties": {"GoalName": {"name": "Save", "parameter": [{"name": "id", "value": "%orderId%"}]}}},
         {"action": "error.handle","properties": {}},
         {"action": "goal.call",   "properties": {"GoalName": {"name": "Rollback"}}}]}

Step 3 shows `goal.call` twice though it was listed once: the step calls two goals, one of them
only on failure. `Ms` is 2000 because the step says seconds and the blank asks for milliseconds — a
required blank is worked out from what the step means. Four optional blanks were shown and NONE
appears in the answer, because no step asks for them: `Overflow` is absent, not `Promote`;
`AsDefault` is absent, not `false`; `Type`, `RetryCount` and `IgnoreError` are absent. `Value` is
`%!data%` because that is the result of the action before it in the same step.

Filling a blank:
- A value the step writes goes in as written. Quote marks in the step are punctuation, not part of
  the value: `throw error "item failed"` gives `"Message": "item failed"` — never
  `"Message": "\\"item failed\\""`.
- A %variable% keeps its % signs. A number is a number, not text. A `variable` blank is a variable
  name with its % signs.
- A required blank the step does not write is yours to work out from what the step means.
- An optional blank appears in your answer ONLY if the step asks for it. If the step is silent
  about it, it must not be in your answer at all — not as a default, not as null, not as an empty
  value. A property shown with a default already has that default; writing it changes nothing and
  is wrong.
- Give a `type` only when the step forces one (`as text`, `(json)`); otherwise the value speaks for
  itself.

The types a blank can ask for:

  text      textual content — names, labels, identifiers, prose, anything with no stronger meaning
  number    a quantity. Its kind is the precision: int, long, decimal, double, …
  bool      true or false
  date      a calendar day, written ISO `2026-01-01`
  time      a clock time, written `14:30:00`
  datetime  both, written ISO `2026-01-01T12:00:00Z`
  duration  a length of time, written ISO-8601 `PT30S`, `PT1H30M`
  path      a file, folder or URL — `report.csv`, `/var/log/app.txt`, `https://x.com/a`
  image     an image by its path — `photo.jpg`; video and audio likewise
  list      several values
  dict      named values
  variable  the NAME of a variable, with its % signs — `%total%`, not its value
  item      any value at all; the step decides what it is

A value the step writes is READ for what it plainly is, not taken as text by default. `"2026-01-01"`
is a date. `"PT30S"` is a duration. `"42"` is a number. `"photo.jpg"` is an image. But a name, a
label, a version (`"v1.2.3"`, `"2.0"`), a code where leading zeros matter (`"0012"`), and anything
relative that only means something at runtime (`"next friday"`, `"tomorrow"`) all stay text.

A written date, time or duration is rewritten in its ISO form: `1st jan 2026` becomes `2026-01-01`,
`9am` becomes `09:00:00`, `5 minutes` becomes `PT5M`. You are the reader of the words; what the
runtime parses is ISO.'''


def render(goal, plan, cat):
    """The actions of a step are a MENU, not a sequence: which actions it uses, with their blanks.
    The order they run in and how often each runs are read off the step by the model."""
    order = {}
    lines = []
    for s in goal['steps']:
        acts = plan.get(s['index'], [])
        if not acts: continue
        order[str(h.step_no(s))] = (s['index'], {f'{m}.{a}' for m, a in acts})
        lines.append(f'step {h.step_no(s)}: ' + '    ' * s.get('indent', 0) + s['text'])
        for m, a in acts:
            lines.append(f'   {m}.{a}')
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
    for m, a in sorted({(m, a) for acts in plan.values() for m, a in acts}):
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
    """{(step_index, position, PropertyName): value} plus the chain the model produced."""
    out = {}
    chains = {}
    for k, entries in (answer or {}).items():
        hit = order.get(str(k))
        if hit is None or not isinstance(entries, list): continue
        si, allowed = hit
        chain = []
        for n, e in enumerate(entries):
            name = (e or {}).get('action')
            if name not in allowed: continue        # only what the step was given
            m, _, a = name.partition('.')
            chain.append((m, a))
            for pname, v in ((e or {}).get('properties') or {}).items():
                out[(si, len(chain) - 1, pname)] = v
        chains[si] = chain
    return out, chains
