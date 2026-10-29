Step text: `throw %!error%`
Formal: `error.throw(Data=%!error%)` — a variable goes in Data alone; never `Message=%!error%`, never both.

Step text: `throw "checkout failed"`
Formal: `error.throw(Message="checkout failed")`

Step text: `throw "not found", status 404`
Formal: `error.throw(Message="not found", Status=404)`

Step text: `throw "the build has broken goals", cause %broken%`
Formal: `error.throw(Message="the build has broken goals", Cause=%broken%)` — the list of errors after `cause` goes in Cause, not Data.
