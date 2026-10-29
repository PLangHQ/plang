# coder v2: event-action stage 2

From the architect's rewritten plan (`.bot/event-action/architect/plan/{start.md,picture.md,expected.md}`) and rulings 41–46.

1. Events on the actor (`actor.event`, `event.list.@this`); `%!app.event%` answers the asker's actor's.
2. Lists by pull (load on first ask, every event whose `Item.Has(item)`) and push (a later bind joins loaded lists); a plain map per actor (43), keyed by the item's own equality.
3. The one question `@event.Item.Has(item)`: base (itself), type (`item.Is(this)`), module (its actions), pattern (`item.Is(pattern)`, the goal answers by its path), variable (equality by name).
4. Patterns: `goal/pattern/this.cs`, made by `goal.list.Find` for a wildcard name; the glob moved to `type/item/path/Glob.cs`.
5. Variable names (24): a name's list loads on its first set only when an event is bound on that name.
6. `event.setting.goal: app | all` (31, 32) with `IsMany` (44, 45): under app, inside plang's own goals, only events bound on many are quiet; clauses always run.
7. Keep, loop, clause, composed are kinds of action (43); stream, file, noop … kinds of channel (46).
8. 26's fold-ins: event.ToString gone, start-is-the-on in one place, no ReferenceEquals in event/, on.event's parent walk gone.
9. Tests: C# proof (ListTests, VariableEventsTests, composed pin, OnEventTests pins); 7 plang tests written for plang-builder.
