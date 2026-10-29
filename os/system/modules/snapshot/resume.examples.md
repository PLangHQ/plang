Step text: `resume %snap%`
Properties: `{"Snapshot": "%snap%"}`

Step text: `resume %snapshot%`
Properties: `{"Snapshot": "%snapshot%"}`

Step text: `resume %snap%, don't wait`
Properties: `{"Snapshot": "%snap%", "Parallel": true}` — "don't wait" / "in the background" is `Parallel=true`: the resumed run goes beside the step and the step gets a task at once.
