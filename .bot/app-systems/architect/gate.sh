#!/bin/bash
# usage: gate.sh <n> — restore, build all test suites, run the six ONE AT A TIME (memory), diff failing names against the baseline
# logs go to $S (default /tmp/claude-1001/architect-gate); kept here because a session restart wipes the scratchpad
N=$1; S=${S:-/tmp/claude-1001/architect-gate}; mkdir -p $S
cd /workspace/plang
git rev-parse --short HEAD > $S/gate$N-head.txt
# serially: a parallel restore races on Shared's assets file
nice dotnet msbuild PLang.Tests/All.proj -t:Restore -m:1 -v:q -nologo > $S/gate$N-restore.log 2>&1; echo RESTORE $?
nice dotnet msbuild PLang.Tests/All.proj -t:Build -p:Configuration=Debug -p:RunAnalyzers=false -v:q -nologo > $S/gate$N-build.log 2>&1; echo BUILD $?
dotnet build-server shutdown > /dev/null 2>&1
for p in Modules Types Wire Data Generator Runtime; do
  nice "PLang.Tests/$p/bin/Debug/net10.0/PLang.Tests.$p" --timeout 900s > "$S/gate$N-$p.log" 2>&1 < /dev/null
done
cat $S/gate$N-*.log | grep -E '^failed ' | sed -E 's/^failed ([^ ]+).*/\1/' | sort -u > $S/gate$N-names.txt
grep -v '^#' .bot/app-systems/coder/baseline-failures.txt | sed '/^$/d' | sort -u > $S/gate$N-base.txt
wc -l < $S/gate$N-names.txt
echo "--- new:"; comm -23 $S/gate$N-names.txt $S/gate$N-base.txt
echo "--- fixed:"; comm -13 $S/gate$N-names.txt $S/gate$N-base.txt
echo "--- http faults:"; grep -h "HttpTestServer: .* faulted" $S/gate$N-*.log | head -5
