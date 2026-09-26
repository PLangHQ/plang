"""The builder's bootstrap, python's half: python DECIDES, C# WRITES.

For each .goal file of the plang builder (os/system/builder/**) and each goal in it, the same two remote
calls the plang builder makes: the decider (v5, stages 1 and 2) and prompt C (gpt-5.4-nano), with the
check and one per-step retry exactly as c_eval runs them. What it keeps is only what was ANSWERED:

    tools/decider/out/bootstrap/<rel .goal>/<goal>.json
        answer1, answer2   the decider's stage-1 / stage-2 answers (what step.Pick.Take reads)
        answer             nano's first answer, in formal (what goal.step.list.Read reads)
        retry              its answer for the steps refused on the first (null when none were)
        refused            what python's check still refuses after the retry (the file is then not built)

The C# install (a throwaway test) parses each .goal with goal.Parse, hands each goal's steps these
answers through the builder's own doors (Take, Scope, Read, fold) and writes the .pr with plang.Text —
one implementation of the value layer, so a python-written .pr never has to match it by hand.

The goals are split as goal.Parse splits them (goal/this.cs Parse), so step i here is step i there.
Rendered requests go to /shared/coder/2.0/bootstrap/ (the only place for them).

    python3 bootstrap.py            # every .goal under os/system/builder
"""
import json, os, re, sys, glob, time, concurrent.futures as cf
import harness as h
import child_eval as e
import decider_eval as d
import prompt_c as c
import c_eval as ce

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = h.ROOT
OUT = os.path.join(HERE, 'out', 'bootstrap')
SHARED = '/shared/coder/2.0/bootstrap'
MODEL = 'gpt-5.4-nano'

def parse(text):
    """A .goal file's goals as goal.Parse reads them: a goal starts at a line that is not a step, a
    comment or a continuation; `/` lines (and /* */ blocks) are the comment of what follows; a blank
    line ends a step and drops a pending comment; an indented line (or a `\\` line) right after a step
    continues it; steps before any goal name belong to an implicit `Start`."""
    goals, goal, step, pending, block = [], None, None, [], False
    def close():
        nonlocal step
        if step is not None: goal['steps'].append(step); step = None
    for n, raw in enumerate(text.replace('\t', '    ').split('\n'), 1):
        raw = raw.rstrip('\r')
        if block:
            end = raw.find('*/')
            if end >= 0:
                if raw[:end].strip(): pending.append(raw[:end].strip())
                block = False
            else: pending.append(raw.strip())
            continue
        trimmed = raw.lstrip()
        if trimmed.startswith('/*'):
            after = trimmed[2:]
            end = after.find('*/')
            if end >= 0: pending.append(after[:end].strip())
            else: block = True; pending.append(after.strip())
            continue
        if not raw.strip():
            pending = []; close(); continue
        if trimmed.startswith('/'):
            pending.append(trimmed[1:].lstrip()); continue
        if trimmed.startswith('- ') or trimmed == '-':
            if goal is None:
                goal = {'name': 'Start', 'comment': None, 'steps': []}; goals.append(goal)
            close()
            indent = (len(raw) - len(trimmed)) // 4
            step = {'index': len(goal['steps']), 'text': trimmed[2:].strip() if len(trimmed) > 2 else '',
                    'indent': indent, 'line': n, 'comment': '\n'.join(pending) if pending else None}
            pending = []
            continue
        if step is not None and raw[0] == ' ':
            step['text'] += '\n' + trimmed.rstrip(); continue
        if step is not None and trimmed.startswith('\\'):
            step['text'] += '\n' + trimmed[1:].rstrip(); continue
        close()
        goal = {'name': trimmed, 'comment': '\n'.join(pending) if pending else None, 'steps': []}
        goals.append(goal); pending = []
    if goal is not None: close()
    return goals

def picks_of(decided):
    """A step's picks as c_eval reads them from decider_eval.one."""
    return {s['index']: {**{a: v['score'] for a, v in s['pick'].items()},
                         **({'@popular': s['popular']} if s.get('popular') else {})} for s in decided['steps']}

def build(rel, goal, cat):
    """One goal: the decider, prompt C, the check, one retry. The answers, or why the goal was refused."""
    cid = f'{rel}#{goal["name"]}'
    case = {'id': cid, 'goal': goal['name'], 'comment': goal['comment'],
            'steps': [{'text': s['text'], 'indent': s['indent'], 'comment': s['comment']} for s in goal['steps']]}
    if not goal['steps']:
        return {'goal': goal['name'], 'answer1': {}, 'answer2': {}, 'answer': '', 'retry': None, 'refused': []}
    folder = os.path.join(SHARED, rel, goal['name'])
    t0 = time.time()
    decided = d.one(case, cat, os.path.join(folder, 'decider'))
    picks = picks_of(decided)
    rec = ce.one_c(MODEL, case, picks)
    answers = os.path.join(ce.OUT, 'C', MODEL, cid)
    read = lambda name: open(os.path.join(answers, name), encoding='utf-8').read() \
        if os.path.exists(os.path.join(answers, name)) else None
    system, user = ce.request('C', case, picks)
    open(os.path.join(folder, 'system.txt'), 'w', encoding='utf-8').write(system)
    open(os.path.join(folder, 'user.txt'), 'w', encoding='utf-8').write(user)
    answer1 = json.load(open(os.path.join(folder, 'decider', '1.decider.answers.json'), encoding='utf-8'))
    answer2_path = os.path.join(folder, 'decider', '2.decider.answers.json')
    answer2 = json.load(open(answer2_path, encoding='utf-8')) if os.path.exists(answer2_path) else {}
    return {'goal': goal['name'], 'answer1': answer1, 'answer2': answer2,
            'answer': read('1.answer.txt'), 'retry': read('2.answer.txt'),
            'caught': rec['caught'], 'refused': rec['refused2'] if rec['caught'] else [],
            'warnings': rec['warnings'], 'seconds': round(time.time() - t0, 1)}

if __name__ == '__main__':
    target = os.path.join(ROOT, sys.argv[1] if len(sys.argv) > 1 else 'os/system/builder')
    files = sorted(f for f in glob.glob(f'{target}/**/*.goal', recursive=True) if '/.build/' not in f)
    ce.OUT = os.path.join(HERE, 'runs', 'bootstrap_' + time.strftime('%Y%m%d_%H%M%S'))
    # GOALS=a,b: only those goals are asked again; the others keep the answers already recorded
    wanted = [x for x in os.environ.get('GOALS', '').split(',') if x]
    cat = h.catalogue()
    jobs = {}
    with cf.ThreadPoolExecutor(8) as ex:
        for f in files:
            rel = os.path.relpath(f, ROOT)
            jobs[rel] = [(g, ex.submit(build, rel, g, cat)) for g in parse(open(f, encoding='utf-8').read())
                         if not wanted or g['name'] in wanted]
        bad = 0
        for rel, goals in jobs.items():
            print(rel)
            for g, fu in goals:
                r = fu.result()
                dest = os.path.join(OUT, rel, g['name'] + '.json')
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                json.dump(r, open(dest, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
                state = 'REFUSED: ' + '; '.join(r['refused']) if r['refused'] else \
                    (f'retried steps {r["caught"]}' if r.get('caught') else 'first try')
                bad += bool(r['refused'])
                print(f'  {g["name"]:<20} {len(g["steps"]):>3} steps  {state}')
    print('answers ->', os.path.relpath(OUT, ROOT), f'({bad} goal(s) refused)')
