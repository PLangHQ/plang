# A name is a path — dot case

Part of the OBP docs: [the pattern](object_pattern_formal.md) (laws and rules), [the smells](obp-smells.md) (named violations). This doc owns one rule: how a name is written, and what writing it that way shows you.

## The rule

Never glue words. A compound name (`ListName`, `buildExecutionPath`, `MaxRedirects`) is a hierarchy written flat. Write it as a dot path, one word per segment:

```
setting.buildExecutionPath   →   setting.build.execution.path
MaxRedirects                 →   redirect.max
```

Then **check that the path navigates**: each segment must be an owner in the code whose member is the next. When it navigates, the name sits where the thing lives. When it doesn't, the path shows you where the thing really lives.

## Why it's a structural rule, not a style

The dot path is the same test as [the three paths agree](object_pattern_formal.md#the-three-paths-agree) (plang path = C# path = file path), applied to every single name. Done correctly, it guides you to where things should be, and it shows you when they don't match.

**The example that found it.** Sketching `list.query`, the action took `ListName`, the name of the variable holding the list, copied from the list actions it replaced:

```csharp
public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
```

As a path, `list.name` doesn't navigate: a list has no name. The variable that holds it has one. So the name is `list.variable.name`, reached through the list. The action takes the list itself:

```csharp
public partial data.@this<app.type.item.list.@this> List { get; init; }
```

`ListName` was a *flat copy* of `list.variable.name`. A glued name that won't navigate is usually one of the named smells: a *flat copy*, a *stray helper* or a *verb+noun*.

**Test**: write the name with dots. If a segment isn't an owner of the next, move the thing; don't rename it.

## The patterns (from the settings sweep)

A survey of every setting class and every action option found these, each a hierarchy the glued name was hiding.

1. **A limit belongs to what it limits.** `Max`/`Min` + noun → `noun.max`.
   `MaxRedirects` → `redirect.max`; `MaxTokens` → `token.max`; `MaxFrames` → `frame.max`.
2. **A unit in a name is a type.** The value becomes a typed value (a `duration`, a size), and the unit leaves the name.
   `TimeoutInSec`, `TimeoutMs`, `TimeoutSeconds` → `timeout`, a `duration` (`5s`, `200ms`).
3. **Verb + noun → `noun.verb`, and the path may lead out of the action.** `ResolveVariables` → `variable.resolve`; `IncludeSubfolders` → `subfolder.include`. When the path names another owner's job, the option moves there. `file.delete`'s `IgnoreIfNotFound`: not finding the file is an error, and ignoring an error is `on.error`'s job. So delete has no such option. The step says it: `delete file 'x.txt', on error 'NotFound' ignore`, which is `on.error.ignore`.
4. **A negated verb turns inside out, and opens up.** `SkipFreshnessCheck` → `freshness.check`, true by default. The positive name is a node, not just a bool, so it can grow:
   ```
   %!signing.verify.setting.freshness.check%            → true (the check runs)
   %!signing.verify.setting.freshness.check.every%      → 5s   (a duration under it)
   %!signing.verify.setting.freshness.check.enabled%    → true
   %!signing.verify.setting.freshness.check.disabled%   → false
   ```
   A setting node's `enabled` and `disabled` are always there, so any switch reads the same way, and any switch can gain members later without renaming.
5. **A name that repeats its owner drops that part.** `on.error`'s `IgnoreError` → `ignore`; `variable.set`'s `AsDefault` → `default`.
6. **Siblings sharing a prefix are one sub-record.** `FollowRedirects`, `MaxRedirects` → `redirect.follow`, `redirect.max`, one `redirect` under the action.

7. **A path only for a real group.** A glued name becomes a path when its first word is something that has, or plausibly will have, more than one member. Otherwise the action is the owner, and the name is the one word it isn't already.
   `ContinuePreviousConversation` → `conversation.continue` (a conversation is a group: its id, its history). But `file.copy`'s `IncludeSubfolders` → `subfolder` (true: included), `file.read`'s `ResolveVariables` → `resolve` (reading only resolves its file's variables), and `list.split`'s `RemoveEmpty` → `empty` (true: empty parts kept). A record type holding one bool is the path taken too literally.

Patterns 2, 3 (when the option moves) and 6 change more than a name: a type, an owner, or a shape. That's the rule doing its job: the unit, the owner and the shared prefix were the structure the glued name hid.

## Where it applies

Every plang-visible name: settings, action options, members, types, files. The one sanctioned compound stays the boolean question, `IsX`/`HasX`. Settings come first, as the template; the existing action properties follow.
