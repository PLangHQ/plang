Step text: `resume %snap%`
Formal: `snapshot.resume(Snapshot=%snap%)`

Step text: `resume %snapshot%`
Formal: `snapshot.resume(Snapshot=%snapshot%)`

Step text: `resume %snap%, don't wait`
Properties: `{"Snapshot": "%snap%", "Parallel": true}` — "don't wait" / "in the background" is `Parallel=true`: the resumed run goes beside the step and the step gets a task at once.
