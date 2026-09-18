# Writing steps the decider can answer

The builder decides a step in one of two ways. The decision engine answers a set of typed
questions about the step, which is fast and cheap, or the step falls back to an llm, which is
neither. A build reports which happened for every step, and the app this was measured on builds
88 percent of its steps without the llm.

Nothing here is syntax. plang has none, and a step that reads clearly to a person is the one that
reads clearly to the engine. What follows is measured: each shape below was built and the result
read off the build report, and where a shape falls back the reason is the builder's, not yours,
unless it says otherwise.

## Read your own build

The build ends with a decider report that names every step that went to the llm and why. That
report is the only thing worth trusting; the notes below will age.

```
plang build --llmservice=openai
```

Add `--rebuild` to build steps again that have not changed, which is how you compare two ways of
writing the same thing. `--decider=off` turns the engine off entirely and builds with the llm
alone.

## Shapes that are answered

These were built and decided, each at 0.9 or above:

```
set %title% = "Hópkaup"
set %target% = "/%path%"
set %question% = %answer.text%
set %title% = "Dev", %chatId% = %id%
return %result%
call goal /admin/dev/Chat id=%chatId%
read file %target%, into %content%, empty variable if not found
write out %result%
if %content% is empty then
if %content% is not empty then
if %path% contains ".goal" then
if %path% starts with "admin" then
if %path% does not start with "tests/" then
if %n% == 3 then
insert into messages, ds: "dev", chatId=%chatId%, who="diff"
[terminal] run "bash", parameters: ["-c", "diff -u a b"], write to %diff%
```

## The one thing you control

**Two variables set to the same value in one step is the one shape worth rewriting.**

```
set %content% = "", %markdown% = ""     falls back
set %a% = "x", %b% = "y"                decided
```

A dictionary holds each value once, so when two names share a value the builder cannot tell which
name kept it and gives the step to the llm. Writing the two assignments as two steps decides both.
Two names with different values is fine and needs no change.

## Shapes that fall back, and why it is not your doing

Rewriting these will not help. They are listed so you recognise them in the report and leave them
alone.

**A default in the assignment.** `set %day% = %request.query.day%, default is 0` needs a list of
records, one per variable, and the builder cannot construct one. Splitting it does not help: the
follow-up step `set %day% = 0 only if it is empty` falls back too.

**Ends with.** `if %path% ends with ".md" then` maps to a method taking a condition object, which
the builder cannot fill, while contains, starts with, is empty and the equality tests all decide.

**Two tests in one step.** `if %path% ends with ".md" and %content% is not empty then` needs the
same condition object.

**A route with a typed placeholder.** `add route "/admin/crm/offer/%id%(number)", call X` needs a
list of records again.

**Navigate.** `[ui] navigate to "/admin/ideas", scroll to top` leaves a required field of the
execute message unfilled.

**Anything the module writes itself.** Sql, c#, templates and prompts are generated, not chosen,
and are meant to go to the llm. The report counts them separately and they are not a problem.

## Why a description is usually the fix

Most fallbacks that are worth fixing are fixed in the module, not in the step. A parameter with no
description is asked as `optional, type Boolean. Which of these is its value in this step?` and
nothing more, and the engine has nothing to go on. Eight booleans in plang were like that, and
writing one sentence each took them from guesswork to 0.99.

Two things learned from doing it:

- Say what the parameter means for the step, and name the case it is not for. `ThrowErrorModule`
  said it lets the user "return out of goal", so `return %result%` chose it and then no method
  matched and the step did not build.
- A value's own contents are not a reason. `write out %result%` is Write even where an earlier
  step put json in %result%, and `IsTemplateFile` is about what the step writes, not what the
  variable holds.

## What a fallback costs

One llm call, about six seconds, and a second one if validation rejects the first answer. Correct
is worth more than decided: a step the engine answers wrongly is a wrong build that no one sees
until it runs, and the builder is written to fall back whenever two readings of a step disagree.
