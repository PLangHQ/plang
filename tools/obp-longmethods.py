#!/usr/bin/env python3
"""Find long method bodies — OBP review candidates.

Long methods are an OBP smell: a method that runs several independent phases is
usually doing work that belongs to several owners. See `Documentation/Runtime2/
obp-cleanup.md` #6 for the smell write-up and the SMELL-vs-LEGIT triage rubric
(not every long method is a smell — parsers, exhaustive dispatchers, and
ordered-guard decision trees are legitimately flat; leave them).

This is a HEURISTIC finder (brace-matching with string/comment masking), not an
exact parser — eyeball the top hits. The exact home is an H4 pass in
`tools/ObpScan` (Roslyn: measure each MethodDeclarationSyntax.Body line span);
emit it as a WARNING, never a build error, because ~20% of long methods are
legitimate.

Usage:  python3 tools/obp-longmethods.py [root]      # root defaults to PLang
Output: line-span distribution + the top 40 longest method bodies, ranked.
"""
import os, re, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else "PLang"

# Tokens that take a `(...)` and a `{` block but are NOT method declarations.
KEYWORDS = {"if", "for", "foreach", "while", "switch", "catch", "using", "lock",
            "fixed", "return", "when", "nameof", "typeof", "sizeof", "default",
            "checked", "unchecked", "where", "new"}

ident_re = re.compile(r"[A-Za-z_][A-Za-z0-9_]*$")


def mask(src):
    """Replace string/char/comment contents with spaces (newlines preserved) so
    their braces — including interpolated-string `{}` holes — don't perturb the
    brace-depth count."""
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        two = src[i:i + 2]
        if two == "//":
            while i < n and src[i] != "\n":
                out.append(" "); i += 1
            continue
        if two == "/*":
            while i < n and src[i:i + 2] != "*/":
                out.append("\n" if src[i] == "\n" else " "); i += 1
            out.append("  "); i += 2
            continue
        if src[i:i + 3] == '"""':                       # raw string literal
            out.append('   '); i += 3
            while i < n and src[i:i + 3] != '"""':
                out.append("\n" if src[i] == "\n" else " "); i += 1
            out.append('   '); i += 3
            continue
        if two in ('@"', '$"') or src[i:i + 3] in ('$@"', '@$"'):
            pre = 2 if two in ('@"', '$"') else 3
            verbatim = '@' in src[i:i + pre]
            out.append(" " * pre); i += pre
            while i < n:
                if verbatim:
                    if src[i] == '"' and src[i + 1:i + 2] == '"':
                        out.append("  "); i += 2; continue
                    if src[i] == '"':
                        out.append(" "); i += 1; break
                else:
                    if src[i] == '\\':
                        out.append("  "); i += 2; continue
                    if src[i] == '"':
                        out.append(" "); i += 1; break
                out.append("\n" if src[i] == "\n" else " "); i += 1
            continue
        if c == '"':
            out.append(" "); i += 1
            while i < n:
                if src[i] == '\\':
                    out.append("  "); i += 2; continue
                if src[i] == '"':
                    out.append(" "); i += 1; break
                out.append("\n" if src[i] == "\n" else " "); i += 1
            continue
        if c == "'":
            out.append(" "); i += 1
            while i < n:
                if src[i] == '\\':
                    out.append("  "); i += 2; continue
                if src[i] == "'":
                    out.append(" "); i += 1; break
                out.append(" "); i += 1
            continue
        out.append(c); i += 1
    return "".join(out)


def find_methods(masked):
    """Yield (line_span, start_line, name) for every method/ctor/local-function
    body: a `{` preceded by `)` (skipping a `where` clause), not `=>` (lambda),
    whose paren-list identifier is not a control keyword and not `new Foo()`."""
    n = len(masked)
    line_of = [0] * (n + 1)
    ln = 1
    for idx, ch in enumerate(masked):
        line_of[idx] = ln
        if ch == "\n":
            ln += 1
    line_of[n] = ln

    results = []
    for i, ch in enumerate(masked):
        if ch != '{':
            continue
        j = i - 1
        while j >= 0 and masked[j] in " \t\r\n":
            j -= 1
        if j < 1:
            continue
        if masked[j - 1:j + 1] == "=>":                 # lambda / expression body
            continue
        if masked[j] != ')':                            # maybe a `where` clause sits between
            seg = masked[max(0, j - 300):i]
            if " where " in seg or seg.lstrip().startswith("where "):
                k = masked.rfind(')', 0, i)
                if k == -1:
                    continue
                if any(x in masked[k + 1:i] for x in "{};"):
                    continue
                j = k
            else:
                continue
        # j at ')': match back to its '('
        depth, k = 0, j
        while k >= 0:
            if masked[k] == ')':
                depth += 1
            elif masked[k] == '(':
                depth -= 1
                if depth == 0:
                    break
            k -= 1
        if k < 0:
            continue
        # identifier before '(' (skip a generic <...> first)
        p = k - 1
        while p >= 0 and masked[p] in " \t\r\n":
            p -= 1
        if p >= 0 and masked[p] == '>':
            d = 0
            while p >= 0:
                if masked[p] == '>':
                    d += 1
                elif masked[p] == '<':
                    d -= 1
                    if d == 0:
                        p -= 1; break
                p -= 1
            while p >= 0 and masked[p] in " \t\r\n":
                p -= 1
        q = p
        while q >= 0 and (masked[q].isalnum() or masked[q] == '_'):
            q -= 1
        name = masked[q + 1:p + 1]
        if not name or not ident_re.match(name) or name in KEYWORDS:
            continue
        r = q
        while r >= 0 and masked[r] in " \t\r\n":
            r -= 1
        s = r
        while s >= 0 and (masked[s].isalnum() or masked[s] == '_'):
            s -= 1
        if masked[s + 1:r + 1] == "new":                # object initializer
            continue
        # match the body braces
        d, b = 0, i
        while b < n:
            if masked[b] == '{':
                d += 1
            elif masked[b] == '}':
                d -= 1
                if d == 0:
                    break
            b += 1
        if b >= n:
            continue
        results.append((line_of[b] - line_of[k] + 1, line_of[k], name))
    return results


def main():
    allm = []
    for root, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in (".build", "bin", "obj")]
        for f in files:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(root, f)
            try:
                src = open(path, encoding="utf-8").read()
            except OSError:
                continue
            for length, sl, name in find_methods(mask(src)):
                allm.append((length, path, sl, name))

    allm.sort(reverse=True)
    bands = [(">=300", 300, 10 ** 9), ("200-299", 200, 299), ("150-199", 150, 199),
             ("100-149", 100, 149), ("60-99", 60, 99), ("40-59", 40, 59)]
    print(f"=== method body line-span distribution (root: {ROOT}) ===")
    for label, lo, hi in bands:
        print(f"  {label:>9}: {sum(1 for L, *_ in allm if lo <= L <= hi)}")
    print(f"  total methods detected: {len(allm)}")
    print("\n=== top 40 longest method bodies (triage with the obp-cleanup #6 rubric) ===")
    for length, path, sl, name in allm[:40]:
        print(f"{length:4d}  {path}:{sl}  {name}()")


if __name__ == "__main__":
    main()
