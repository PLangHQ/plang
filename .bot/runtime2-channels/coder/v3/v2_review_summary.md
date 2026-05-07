# v2 review (self-review during v3 scoping)

v2 standardized closed-set parameter validation via `[Choices]` and
unblocked 14 channel `.test.goal` builds. C# stayed 2744/0/0; PLang
went 188/0/18 → 191/10/5. The 13 stale stubs that built clean exposed
10 runtime failures.

Debugging those 10 surfaced design problems below the surface. Three
broad shapes:

1. **`channel.add` and `channel.set` look up helper goals via
   `app.Goals.Get(name)`** — a registry-only call that doesn't lazy
   load. The proper resolution is `Data<GoalCall>` typed at build time,
   like `goal.call`, `event.on`, `app.run`, etc. — builder stamps
   PrPath; runtime loads from PrPath. Already a uniform pattern across
   the rest of the codebase; channel handlers just diverged.
2. **`Channels.Resolve` throws `ChannelNotFoundException`** instead of
   returning `Data` with an error like every other failure path. The
   exception then gets flattened by `Step.RunAsync` to
   `Key="StepError"`, destroying the typed identity. So
   `- on error key:"ChannelNotFound"` never matches, and 1 of the 10
   tests fails for that reason. The honest fix is to make the resolve
   path Data-shaped from the start.
3. **The role / custom-channel split is a fiction.** `Role` enum,
   `Channel.Role` property, the difference between `channel.add` and
   `channel.set`, the special-case removal protection — all of it
   exists because we drew a line between "channels we knew at design
   time" and "channels users register at runtime." There's no
   *behaviour* that requires the line — only convenience defaults
   (output/error/input pre-registered at boot). The channels are just
   channels; "output", "error", "input" are just names that happen to
   be pre-registered.

(3) is the architectural one and absorbs most of (1)/(2) along with it
— once `set` and `add` collapse and channels are uniform, the
goal-resolution path stops being two divergent handlers and becomes
one. This is v3.
