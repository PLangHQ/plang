# ask user

`ask user` stops a goal, puts a question to the user, and continues **on the same step** when
the answer arrives. Think of it as `Console.ReadLine()`.

```plang
- [output] ask user "What is your name?", write to %name%
- write out "Hello %name%"
```

The second step runs only after the user has answered. Nothing else about the goal changes.

## The one thing you must know: stateful or stateless

`ask user` behaves differently depending on where the app runs, and almost every mistake with
it comes from not knowing which one you are in.

### Console: stateful

The same thread is still sitting there waiting. Every variable from the earlier steps is still
in memory, exactly like `Console.ReadLine()`.

```plang
- select * from orders where id=%id%, return 1 row, write to %order%
- [output] ask user "Confirm order for %order.total%?", write to %answer%
- write out "Order %order.id% confirmed"        # %order% is still here
```

### Web: stateless

There is no thread waiting. The request that rendered the form finished and went away, and with
it every variable the earlier steps produced. When the user submits, a **new request** arrives,
the runtime jumps straight to the `ask user` step, and execution continues from there.

The same goal, written for the web, is broken:

```plang
- select * from orders where id=%id%, return 1 row, write to %order%
- [output] ask user template "confirm.html", write to %answer%
- write out "Order %order.id% confirmed"        # %order% is EMPTY, that step never ran
```

**Whatever the continuation needs must travel with the question**, in `call back data`, and be
used to load the data again:

```plang
Confirm
- select * from orders where id=%id%, return 1 row, write to %order%
- [output] ask user template "confirm.html"
    replace "#order-%id%"
    call back data: {"orderId": "%order.id%"}
    write to %answer%

# from here down we are in the second request, everything above is gone
- select * from orders where id=%answer.orderId%, return 1 row, write to %order%
- update orders set confirmed=1 where id=%order.id%
- [ui] render "order.html", cssSelector: "#order-%order.id%", action: "replace"
```

Anything that is part of the **request** survives, because it arrives again: route parameters,
query string, the identity. Only variables produced by earlier steps are gone.

## How the callback works

The template gets two variables from the `ask user` step:

```html
<form action="{{ url }}" method="post">
  <input type="hidden" name="callback" value="{{ callback }}">
  <input name="email" type="email" required>
  <button type="submit">Continue</button>
</form>
```

`callback` is base64 JSON holding `CallbackInfo(GoalName, GoalHash, StepIndex)` plus a signature
bound to the browser's identity. On the POST the server verifies the signature, finds the goal by
its hash, and sets the step index, so execution resumes at the `ask user` step. The posted form
fields plus `call back data` come back as the variable you wrote the answer to.

Three consequences:

- **The route must accept POST.** The form posts to the url it was rendered from. A `GET` only
  route gives `Routing not found`.
- **A callback is used once.** After the answer is consumed the form is spent. To show the form
  again you must run `ask user` again, which produces a new callback.
- **Do not call the goal from inside itself while a callback is in flight.** The nested call
  matches the same goal hash, jumps to the same `ask user` step, gets the same answer, and
  recurses until the engine stops it at 1000 frames.

## One shot forms

Because a callback is spent once, a form is naturally one shot. Do not try to keep one form alive
for repeated edits. Give the user an edit action that runs the goal again:

```plang
Vendor
- select * from vendors where id=%id%, return 1 row, write to %v%
- [ui] render "card.html", cssSelector: "#vendor-%id%", action: "replace"

Edit
- select * from vendors where id=%id%, return 1 row, write to %v%
- [output] ask user template "form.html"
    replace "#form-%id%"
    call back data: {"id": "%id%"}
    write to %answer%

- update vendors set name=%answer.name% where id=%id%
- call goal Vendor
```

`card.html` holds an empty `<div id="form-{{ v.id }}"></div>` and a link to `/vendor/{{ v.id }}/edit`.
Opening the vendor shows the card. Pressing edit fills the form slot. Saving runs `Vendor` again,
which replaces the card, and the form is gone with it. Pressing edit again starts a new cycle.

Two goals, two routes, no recursion, and each step updates only the part of the page it owns.

## Validating the answer

`on callback` names a goal that runs before execution resumes. Use it to reject bad input:

```plang
- [output] ask user template "email.html"
    append to "#signIn"
    call back data: {"step": "email"}
    on callback ValidateEmail
    write to %answer%

ValidateEmail
- if %answer.email% does not match regex "^[^@\s]+@[^@\s]+\.[^@\s]+$" then
    - throw error "That is not a valid email", target: "#signInError", show
```

Throwing in the callback goal keeps the user on the form with the error shown. It is a guard, not
the place to do the work. The work belongs after the `ask user` step.

## Rendering the answer

A form submit is a request like any other, so the server decides what the browser shows. Render
the part that changed:

```plang
- [ui] render "list.html", cssSelector: "#list", action: "replace"
```

Beware of replacing a container that holds something the user still has open. Replacing `#list`
removes every element inside it, including a card another `ask user` just rendered there.

See also [Identity in the browser](./IdentityInTheBrowser.md) for why the signature on the
callback is tied to the browser, and [Everybody hates forms](./blogs/EverbodyHatesForms.md) for
gathering information without a form at all.
