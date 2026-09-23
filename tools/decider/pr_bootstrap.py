import json, sys, re, glob

CONDITIONS = {"if", "elseif", "else"}

def is_condition(a):
    return a.get("module") == "condition" and a.get("action") in CONDITIONS

# --- (c) flat inline condition -> child (operates on PLURAL pre-rename shape) ---
def fold_step(step):
    acts = step.get("actions") or []
    # find first condition action
    ci = next((i for i, a in enumerate(acts) if is_condition(a)), None)
    if ci is None:
        return step
    body = acts[ci + 1:]
    if not body:
        return step
    leading = acts[:ci]
    cond = acts[ci]
    # body actions become a single child step
    ptext = step.get("text") or ""
    # heuristic body-clause text: after first ", " or " then "
    ctext = ptext
    for sep in [" then call ", ", call ", " then ", ", "]:
        if sep in ptext:
            ctext = sep.replace("then ", "").strip(", ").strip() + ptext.split(sep, 1)[1] if False else ptext.split(sep, 1)[1]
            break
    child_step = {
        "index": step.get("index", 0) * 100 + 1,
        "text": ctext,
        "lineNumber": step.get("lineNumber", 0),
        "actions": body,
        "waitForExecution": True,
    }
    cond = dict(cond)
    cond["child"] = [child_step]
    step = dict(step)
    step["actions"] = leading + [cond]
    return step

def fold_goal(goal):
    goal = dict(goal)
    if "steps" in goal:
        goal["steps"] = [fold_step(s) for s in goal["steps"]]
    if "goals" in goal:
        goal["goals"] = [fold_goal(g) for g in goal["goals"]]
    return goal

# --- (a) structural wire-key rename to singular ---
def rn(d, old, new):
    if old in d:
        d[new] = d.pop(old)

def ren_action(a):
    a = dict(a)
    rn(a, "action", "name")
    rn(a, "parameters", "parameter")
    rn(a, "defaults", "default")
    rn(a, "modifiers", "modifier")
    if "modifier" in a:
        a["modifier"] = [ren_action(m) for m in a["modifier"]]
    if "child" in a:
        a["child"] = [ren_step(s) for s in a["child"]]
    return a

def ren_step(s):
    s = dict(s)
    rn(s, "actions", "action")
    s.pop("indent", None)  # drop indent
    if "action" in s:
        s["action"] = [ren_action(a) for a in s["action"]]
    return s

def ren_goal(g):
    g = dict(g)
    rn(g, "steps", "step")
    rn(g, "goals", "child")
    if "step" in g:
        g["step"] = [ren_step(s) for s in g["step"]]
    if "child" in g:
        g["child"] = [ren_goal(x) for x in g["child"]]
    return g

# --- (b) nav-string fix in all string values ---
NAV = [(".Steps[", ".step["), (".Steps.", ".step."), (".Steps%", ".step%"),
       (".Actions%", ".action%"), (".Actions.", ".action."), (".Actions[", ".action[")]
def fix_navs(o):
    if isinstance(o, str):
        for a, b in NAV: o = o.replace(a, b)
        return o
    if isinstance(o, list): return [fix_navs(x) for x in o]
    if isinstance(o, dict): return {k: fix_navs(v) for k, v in o.items()}
    return o

files = sys.argv[1:]
for f in files:
    d = json.load(open(f))
    d = fold_goal(d)      # (c) first, on plural shape
    d = ren_goal(d)       # (a) rename to singular
    d = fix_navs(d)       # (b) nav strings
    json.dump(d, open(f, "w"), indent=2, ensure_ascii=False)
    print("transformed", f)
