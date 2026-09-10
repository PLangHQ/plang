# Writing goals that behave

A goal is a list of steps, and that looks simple enough that people write goals the way they write
functions in another language. Most of the trouble comes from four places where a goal is not a
function. This page is those four, with the shape that works.

## One decision, one goal

The pattern that survives contact with a real flow is a chain of goals, each of which makes exactly
one decision and hands over:

```plang
ContinueToNextStep
- call goal LoadHold
- if %hold% is empty then call goal SlotStep, else call goal AfterSlot

AfterSlot
- call goal LoadCartLines
- if %cart% is empty then call goal ChooseStep, else call goal AfterChoose

AfterChoose
- if %user.id% is empty then call goal SignInStep, else call goal AfterSignIn
```

It reads like a flow chart because it is one, and every branch is one line. The alternative, a long
goal with nested `if` blocks and early exits, is where the next section's bug lives.

## `end goal` inside an `if` is a trap

```plang
DoTheThing
- if %alreadyDone% is true then
    - end goal
- ... the actual work ...
```

This works the first time and then quietly stops working. In any flow that is resumed by a callback,
an `ask user` answer being the common case, the goal stops from the second resume onward **even
when the condition is false**. Nothing is logged. The page simply does nothing and you go looking
for the bug in the step after it.

Write the branch instead:

```plang
DoTheThing
- if %alreadyDone% is true then call goal Skip, else call goal Work
```

`end goal` at the top level of a goal, not inside a condition, is fine.

## A called goal does not hand its variables back

```plang
Caller
- call goal LoadCustomer
- write out %customer.name%          # empty

LoadCustomer
- select * from customers where id=%id%, return 1 row, write to %customer%
```

Setting a variable inside a goal does not make it visible to the caller. What comes back is what
the goal **returns**, and the caller has to catch it:

```plang
Caller
- call goal LoadCustomer id=%id%, write to %customer%

LoadCustomer
- select * from customers where id=%id%, return 1 row, write to %customer%
- return %customer%
```

Parameters go the other way as named arguments: `call goal SendMail to=%email%, subject=%subject%`.

This is also why an accumulating loop does not work: `go through %files%, call SaveFile` cannot
build up a list in the caller. Have the called goal write its result somewhere, a table or a file,
rather than into a variable the caller hopes to read.

## Do not invent variables to hold what you already have

```plang
# noise
- set %userId% = %user.id%
- set %locationId% = %hold.locationId%
- set %slotStart% = %hold.slotStart%
- call goal BookSlot userId=%userId%, location=%locationId%, start=%slotStart%

# the same thing
- call goal BookSlot userId=%user.id%, location=%hold.locationId%, start=%hold.slotStart%
```

Variable paths resolve wherever a value is expected, including inside arguments, strings and sql
parameters. Every intermediate variable is a step that can fail, a name that can go stale, and a
line that hides the actual call.

## Loops name the item backwards from what you expect

```plang
- go through %files%, call SaveFile item=%file%
```

The parameter key is the literal word `item`, and the value is the name you want it to have inside
the goal, here `%file%`. Writing `file=%item%` looks more natural and passes a parameter called
`file` instead, leaving `%file%` empty in the goal and producing a NOT NULL error three steps later.

`%position%`, `%list%` and `%listCount%` are available too. `%position%` is a good filename when the
row id does not exist yet.

## `ask user` has to be told to wait

```plang
- [output] ask user template "/ui/pages/auth/signIn.html"
    append to %target%, scroll into view
    call back data: {"next": "%next%"}
    on callback ValidatePhone
    write to %signIn%
```

Without the `[output]` hint and a `call back data` object, the step can build as a plain render,
which draws the form and carries straight on to the next step instead of waiting for an answer. The
symptom is a flow that runs to the end the moment the page appears.

And because an `ask user` answer resumes the goal, everything above about `end goal` applies with
full force to any goal that contains one.

## Events are goals too

Cross cutting things are bound once rather than called from every goal:

```plang
Events
- before each goal(including private) in /admin/.*, call CheckAdmin
- before each goal(including private) in /pages/.*, call /pages/Layout
- on app error, call goal HandleError
```

Two things worth knowing: a guard has to come before the layout binding for the same path, and
rendering a page from inside an error event produces an empty response body, so an error page bound
that way never appears.

## See also

- [Identity in the browser](./IdentityInTheBrowser.md), which decides what a goal can know about
  who is asking.
- [Talking to an api](./TalkingToAnApi.md)
- [Steps that build the first time](./StepsThatBuild.md)
