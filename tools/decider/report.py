"""Scores a runs/<name>.jsonl. Threshold sweep is free — probabilities are stored."""
import json, sys, os, collections

ALIAS = {'builder': 'build'}   # module renamed on disk; the .pr labels predate it

# Ruled by Ingi: a yes/no question is `condition`, whatever it is about — `list.contains`
# should not exist. The .pr labels predate the ruling, so they are corrected here.
RELABEL = {('list', 'contains'): ('condition', 'compare'),
           ('list', 'any'): ('condition', 'compare')}

def load(name):
    p = os.path.join(os.path.dirname(__file__), 'runs', f'{name}.jsonl')
    return [json.loads(l) for l in open(p)]

def score(recs, threshold):
    tp = fp = fn = 0; exact = total = 0
    act_ok = act_n = 0
    store_ok = store_n = 0; store_misses = []
    misses = []
    for r in recs:
        if 'error' in r: continue
        probs = r['stage1']['probs']; choice = r['stage2']['choice']
        for s in r['steps']:
            i = str(s['index'])
            s['label'] = [RELABEL.get((m, a), (m, a)) for m, a in s['label']]
            label_mods = {ALIAS.get(m, m) for m, _ in s['label']}
            # the result-store: `variable` beside another module is the trailing write, scored on its own
            stores = 'variable' in label_mods and len(label_mods) > 1
            if stores: label_mods.discard('variable')
            # module answers only: `@store` (older runs) and the common actions (`variable.set`, …) are not modules
            pred = {m for m, p in probs.get(i, {}).items() if m != '@store' and '.' not in m and p is not None and p >= threshold}
            # Decision rule: a step whose only work is the variable module IS the store — there is
            # nothing extra to keep. The store answer only counts when other work was done.
            work = pred - {'variable'}
            p_store = probs.get(i, {}).get('@store', probs.get(i, {}).get('variable.set'))
            if p_store is not None and work:
                store_n += 1
                if (p_store >= 0.5) == stores: store_ok += 1
                else: store_misses.append((s['text'], stores, round(p_store, 2)))
            # and `variable` tagged beside other work at a middling probability is that same store, not a work module
            if work and 'variable' in pred and p_store is not None and p_store >= 0.5: pred.discard('variable')
            total += 1
            if pred == label_mods: exact += 1
            tp += len(pred & label_mods); fp += len(pred - label_mods); fn += len(label_mods - pred)
            if pred != label_mods:
                misses.append((r['goal'], s['text'], sorted(label_mods), sorted(pred),
                               {m: round(probs[i].get(m, 0) or 0, 2) for m in (label_mods | pred)}))
            # stage 2: only where the module was right and the label names an action
            label_acts = {(ALIAS.get(m, m), a) for m, a in s['label']}
            for m in pred & label_mods:
                key = f'{s["index"]}|{m}'
                if key not in choice: continue
                want = {a for mm, a in label_acts if mm == m}
                got = choice[key][0]
                act_n += 1
                if got in want: act_ok += 1
    prec = tp / (tp + fp) if tp + fp else 0; rec = tp / (tp + fn) if tp + fn else 0
    return dict(threshold=threshold, steps=total, exact=exact, precision=prec, recall=rec,
                action_acc=(act_ok / act_n if act_n else 0), action_n=act_n, misses=misses,
                store_acc=(store_ok / store_n if store_n else None), store_n=store_n, store_misses=store_misses)

def timing(recs):
    s1 = [r['stage1'] for r in recs if 'stage1' in r]; s2 = [r['stage2'] for r in recs if 'stage2' in r]
    n = len(s1) or 1
    return dict(goals=len(s1), errors=sum('error' in r for r in recs),
                s1_secs=sum(x['secs'] for x in s1) / n, s1_q=sum(x['questions'] for x in s1) / n, s1_kb=sum(x['bytes'] for x in s1) / n / 1024,
                s2_secs=sum(x['secs'] for x in s2) / n, s2_q=sum(x['questions'] for x in s2) / n, s2_kb=sum(x['bytes'] for x in s2) / n / 1024,
                s1_tokens=sum((x.get('usage') or {}).get('input_tokens', 0) for x in s1) / n)

if __name__ == '__main__':
    name = sys.argv[1] if len(sys.argv) > 1 else 'sample'
    recs = load(name)
    t = timing(recs)
    print(f"goals {t['goals']}  errors {t['errors']}")
    print(f"stage1  {t['s1_secs']:.1f}s/goal  {t['s1_q']:.0f} questions  {t['s1_kb']:.0f} KB  {t['s1_tokens']:.0f} input tokens")
    print(f"stage2  {t['s2_secs']:.1f}s/goal  {t['s2_q']:.0f} questions  {t['s2_kb']:.0f} KB")
    print()
    print(f"{'thr':>4} {'steps':>5} {'exact':>6} {'prec':>6} {'recall':>6} {'action':>7} {'n':>5} {'store':>6}")
    for thr in (0.5, 0.6, 0.7, 0.8, 0.9):
        s = score(recs, thr)
        st = f"{s['store_acc']:.1%}" if s['store_acc'] is not None else '-'
        print(f"{thr:>4} {s['steps']:>5} {s['exact']/s['steps']:>6.1%} {s['precision']:>6.1%} {s['recall']:>6.1%} {s['action_acc']:>7.1%} {s['action_n']:>5} {st:>6}")
    s = score(recs, 0.5)
    if s['store_misses']:
        print("\n--- store misses (step | stores? | p)")
        for t, st, p in s['store_misses']: print(f"{t[:70]} | {st} | {p}")
    show = int(sys.argv[2]) if len(sys.argv) > 2 else 25
    thr = float(sys.argv[3]) if len(sys.argv) > 3 else 0.5
    s = score(recs, thr)
    print(f"\n--- first {show} module misses at {thr} (goal | step | label -> predicted | probs)")
    for g, txt, lab, pred, pr in s['misses'][:show]:
        print(f"{g} | {txt[:70]} | {lab} -> {pred} | {pr}")
    # action misses
    print(f"\n--- first {show} action misses at {thr}")
    shown = 0
    for r in recs:
        if 'error' in r: continue
        for st in r['steps']:
            i = str(st['index']); probs = r['stage1']['probs']
            label = {(ALIAS.get(m, m), a) for m, a in st['label']}
            for m in {m for m, p in probs.get(i, {}).items() if (p or 0) >= thr} & {m for m, _ in label}:
                key = f'{st["index"]}|{m}'
                if key in r['stage2']['choice']:
                    got, conf = r['stage2']['choice'][key]
                    want = {a for mm, a in label if mm == m}
                    if got not in want and shown < show:
                        shown += 1; print(f"{r['goal']} | {st['text'][:70]} | {m}: want {sorted(want)} got {got} ({conf:.2f})")
    for r in recs:
        if 'error' in r: print('ERROR', r['goal'], r['error'][:200])
