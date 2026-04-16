# v5 Plan: Convert action handler properties to Data<T>

## Scope

Convert all action handler properties from plain types to `Data.@this<T>` across ALL module directories. Then update Run() methods and provider code that accesses those properties.

## Rules

Properties to convert:
- `string Foo` -> `Data.@this<string> Foo`
- `int Bar` -> `Data.@this<int> Bar`
- `object? Baz` -> `Data.@this Baz` (plain Data.@this for object?)
- `List<T> X` -> `Data.@this<List<T>> X`
- `GoalCall X` -> `Data.@this<GoalCall> X`
- `Dictionary<K,V> X` -> `Data.@this<Dictionary<K,V>> X`
- Nullable types: `string?` -> `Data.@this<string>?`, etc.

DO NOT convert:
- `[VariableName]` properties (stay string)
- `[Provider]` properties (stay as provider interface)
- Properties already typed as `Data.@this` or `Data.@this<T>`
- Context, Step, Action, Channels, Static properties (interface-supplied)
- Actor properties (domain types, not parameters from .pr)
- GoalCall with `[GoalCallback]` attribute (these are callbacks, keep as GoalCall?)

In Run() methods:
- `Foo` -> `Foo.Value!` for non-nullable
- `Foo` -> `Foo?.Value` or `Foo.Value` for nullable
- Provider delegated actions: no Run() changes needed (provider accesses action properties)

Provider updates:
- Where providers access `action.Foo`, change to `action.Foo.Value!` or `action.Foo.Value` as appropriate

## Files to convert (by module)

### http/ (3 action files)
- request.cs: Url, Method, Body, Headers, ContentType, Encoding, TimeoutInSec, Unsigned, SignOptions, OnStream, StreamAs
- download.cs: Url, SaveTo, IfExists, Headers, TimeoutInSec, Unsigned, SignOptions, OnProgress
- upload.cs: Url, Content, Method, Headers, Encoding, TimeoutInSec, Unsigned, SignOptions, As, OnProgress
- configure.cs: TimeoutInSec, BaseUrl, DefaultHeaders, ContentType, Encoding, Unsigned, FollowRedirects, MaxRedirects, Default
- DefaultHttpProvider.cs: update property accesses

### llm/ (1 action file)
- query.cs: Messages, Tools, OnToolCall, OnValidateResponse, OnStream, Schema, Format, Model, ContinuePreviousConversation, Temperature, TopP, MaxTokens, MaxToolCalls, MaxValidationRetries, Cache
- OpenAiProvider.cs: update property accesses

### signing/ (2 action files)
- sign.cs: already Data.@this for Data, convert Contracts, Headers, ExpiresInMs
- verify.cs: already Data.@this for Data, convert Contracts, Headers, TimeoutMs
- Ed25519Provider.cs: update property accesses

### crypto/ (2 action files)
- hash.cs: already Data.@this for Data, convert Algorithm
- verify.cs: already Data.@this for Data, convert Hash, Algorithm
- DefaultProvider.cs: update property accesses

### identity/ (8 action files)
- create.cs: Name, SetAsDefault, Provider
- get.cs: Name
- rename.cs: Name, NewName
- archive.cs: Name, Force
- unarchive.cs: Name
- export.cs: Name
- list.cs: no properties to convert
- setDefault.cs: Name
- DefaultIdentityProvider.cs: update property accesses

### ui/ (1 action file)
- render.cs: Template, IsFile (Parameters already Data.@this)
- FluidProvider.cs: update property accesses

### settings/ (3 action files)
- get.cs: Key
- set.cs: Key, Value
- remove.cs: Key

### cache/ (2 action files)
- check.cs: Step - leave alone (it's a Step type)
- store.cs: Step - leave alone, Data already Data.@this

### mock/ (3 action files)
- action.cs: ActionPattern, ReturnValue, GoalToCall, Parameters
- reset.cs: Mock
- verify.cs: Mock, ExpectedCount, Message

### module/ (2 action files)
- add.cs: Path, Namespace
- remove.cs: Name

### provider/ (4 action files)
- list.cs: Type
- load.cs: Path, Name
- remove.cs: Name, Type
- setDefault.cs: Name, Type

### builder/ (8 action files)
- app.cs: Path
- appSave.cs: no properties
- goals.cs: Path
- goalsSave.cs: Goal (already Goal type - leave alone)
- merge.cs: Step, StepFromLlm (Step types - leave alone)
- validate.cs: Actions (already Actions type - leave alone)
- actions.cs: no properties
- types.cs: no properties
- promoteGroups.cs: Steps (object type -> Data.@this)
- DefaultBuilderProvider.cs: update property accesses

### app/ (1 action file)
- run.cs: GoalName, Step, Action, Actor - leave alone (domain types)

### Additional modules not in the original list:
- assert/*: already use Data.@this for values, string? Message needs conversion
- condition/*: already use Data.@this for Left/Right, convert Negate, Operator
- error/throw.cs: Message, StatusCode, Key
- error/check.cs: Data already Data.@this, Step leave alone
- event/on.cs: Type, GoalToCall, GoalPattern, StepPattern, ActionPattern, IsRegex, Priority, Actor - leave alone
- event/remove.cs: EventId
- event/skipAction.cs: Value (object?)
- goal/call.cs: GoalName, Actor - leave alone
- goal/return.cs: Data already Data.@this, Depth
- loop/foreach.cs: Collection (object?), ItemName/KeyName are [VariableName]
- output/write.cs: Data already Data.@this
- timer/start.cs: Name, Scope
- timer/end.cs: Name
- variable/*: Name is [VariableName], Value already Data.@this, Type already string?, AsDefault already bool
- list/*: ListName is [VariableName], Value/Index/etc need conversion
- math/*: A, B, Value, etc need conversion

## Approach

1. Convert all action files systematically
2. Update provider files where they access changed properties
3. Build to verify
