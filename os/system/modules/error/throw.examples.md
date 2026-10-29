Step text: `throw %!error%`
Formal: `error.throw(Data: item = %!error%)` — a variable goes in Data alone; never `Message=%!error%`, never both.

Step text: `throw "checkout failed"`
Formal: `error.throw(Message: text = "checkout failed")`

Step text: `throw "not found", status 404`
Formal: `error.throw(Message: text = "not found", Status: status = 404)`

Step text: `throw "the build has broken goals", cause %broken%`
Formal: `error.throw(Message: text = "the build has broken goals", Cause: list<error> = %broken%)` — the list of errors after `cause` goes in Cause, not Data.
