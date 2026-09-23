"""Narrow a commit range to candidates touching security-relevant files (read-only on git)."""
import re, subprocess, sys

PATTERNS = {
    "tamper": r"signing|verify|/Wire\.cs|Properties\.cs|canonical|Transport|this\.Output|data/reader|ReadContext|Cut4_Properties|PropertiesWireShape|Signature",
    "mask": r"Sensitive|AssertionError|assert/|Diagnostic|mask|Redact|SensitivePropertyFilter|error/Error\.cs|this\.Output|Normalize",
}
exclude = re.compile(r"\.bot/|\.md$|\.pr$|\.goal$|/obj/|Documentation|tools/decider", re.I)
PATTERNS["tamper2"] = PATTERNS["tamper"]
rng = {"tamper": "0ea5a4b94..da067599c^", "tamper2": "da067599c..HEAD", "mask": "da067599c^..HEAD"}[sys.argv[1]]
pattern = re.compile(PATTERNS[sys.argv[1]], re.I)
if len(sys.argv) > 2 and sys.argv[2] == "hashes":
    log = subprocess.run(["git", "log", "--reverse", "--format=@@%h", "--name-only", rng],
                         capture_output=True, text=True, cwd="/workspace/plang").stdout
    cur, hit = None, False
    for line in log.splitlines() + ["@@END"]:
        if line.startswith("@@"):
            if cur and hit: print(cur)
            cur, hit = line[2:], False
        elif line.strip() and pattern.search(line) and not exclude.search(line):
            hit = True
    sys.exit(0)
exclude = re.compile(r"\.bot/|\.md$|\.pr$|\.goal$|/obj/|Documentation|tools/decider", re.I)

log = subprocess.run(["git", "log", "--reverse", "--format=@@%h %s", "--name-only", rng],
                     capture_output=True, text=True, cwd="/workspace/plang").stdout
cands, cur, hits = [], None, []
for line in log.splitlines():
    if line.startswith("@@"):
        if cur and hits: cands.append((cur, hits))
        cur, hits = line[2:], []
    elif line.strip() and pattern.search(line) and not exclude.search(line):
        hits.append(line.strip())
if cur and hits: cands.append((cur, hits))
print(f"{len(cands)} candidates")
for c, h in cands:
    print(c[:110])
    for f in h[:6]: print("    " + f)
