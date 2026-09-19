# Building a goal's parameters in one request

The builder asks the llm once per step. It does not have to. Once the decision engine has fixed
every step's module and method, what is left is filling parameters, and that can be asked for a
whole goal at once.

Everything below was measured on two goals of one app, `admin/dev/File` with 10 steps and
`routes/AdminRoutes` with 28, against a hand written reference rather than against an earlier
build, because an earlier build is just what one model answered and scoring against it rewards
imitating that model.

## What it is worth

| | llm calls | request size | accuracy |
| --- | --- | --- | --- |
| one request per step, as today | 10 | 67307 chars | 10 of 10 |
| one request for the goal | 1 | 31662 chars | 10 of 10 |

The saving is mostly one thing: a per step request carries the method's whole signature, and ten
steps carry it ten times. Grouped, `AddRoute` and its examples are written once for all 28 route
steps: 2341 characters of method information against 4247 characters of steps.

On the 28 step goal the single request takes about 8 seconds where 28 sequential calls would
take minutes.

## The shape of the request

Three messages.

**System.** The existing builder system prompt, with two corrections. It tells the model to map
intent onto one of the functions provided, which is no longer its job, and the `Reasoning` field
it describes is spent on explaining a choice that has already been made. Dropping `Reasoning`
scored the same or better and is smaller.

**Assistant.** Only the methods the decider picked, one entry each however many steps use them,
and only the types those methods refer to. As lines rather than json: the json spends most of its
characters repeating the keys `Type`, `Name`, `Description`, `IsRequired` and `DefaultValue` for
every parameter of every method. Lines cost 31676 characters where json costs 48018, for the same
answers.

Two fields must survive that compaction, and both were lost once and cost real accuracy:

- `Examples` on the method. Dropping them took the routes goal from 27 of 28 to 8 of 28.
- `AvailableValues` on an enum property. Without it the model invented a different spelling of
  `ConditionKind` on every run: `Compound`, `CompoundCondition`, and once the full type name.

**User.** The response scheme, a line saying the modules and methods are already decided, the step
numbers being asked about, and then each step labelled with the method it calls.

## Send only the steps that need building

The request does not have to hold the whole goal. Any subset works, and the builder already knows
which steps changed.

| | accuracy |
| --- | --- |
| all 10 steps of a goal | 10 of 10 |
| three changed steps of it | 3 of 3 |
| each step on its own | 10 of 10 |

A step keeps its meaning without its neighbours, including `read file %target%` where `%target%`
is set two steps earlier, because what the step needs is its own text and its method, not the
goal around it. Sending the goal text as context is therefore not needed, though it remains an
option if a goal is found where it is.

**Number the steps you send, do not describe a range.** Saying "numbered 0 to n-1" while labelling
a step `step 5` makes the model answer `0`, and the answer then belongs to no step. Name the
numbers: "numbered 5, 6, 7. Return 3 entries, using those numbers."

## Split a long goal

Above roughly 20 steps, two requests in parallel are worth it: the 28 step goal drops from about 8
seconds to about 4, because the answer is generated in two halves at once. Below that it is not
worth doing and it hurts: a 10 step goal split in two scored 4 of 10.

The input is paid twice, since each half carries the method information, so this buys wall clock
and not tokens.

## What did not help

Measured and rejected, so they are not tried again:

- Short json keys in the answer, `n` and `p` and `v` instead of `Number` and `Parameters` and
  `Value`. No change to time or accuracy.
- One message instead of three. No change.
- A cap on the answer length. No change, and one run got slower.
- A prompt rule telling the model that a goal takes parameters only when the step writes them.
  It works, 18 of 28 to 27 of 28, but examples on the method do the same thing and belong to the
  method rather than to every step in the language.

## An example must not look like a real step

The one that bit hardest. An `AddRoute` example was written as
`add route /admin/dev/chat/%id%(number)/archive, call /admin/dev/Archive goalId=%id%`, which is a
real route in the app it was measured against, differing only by the parameter the example exists
to show. The model stopped reading that step and answered it by retrieving the example, writing
`goalId=%id%` onto three steps that never mention it.

Write examples with values nothing in a real app would use. An example teaches a shape; when it
doubles as a lookup entry for a real line it stops teaching and starts being copied.

## Only 4 of 371 methods have an example

That is the largest single lever found, and it is unrelated to batching. `AddRoute` was one of the
367 without. Four examples on it took that goal from 18 of 28 to 28 of 28 and made it stable across
runs where it had been swinging.

A description written as prose did not do the same work: the same content as a description scored
11, 11, 27, 27 where examples scored 27 four times.
