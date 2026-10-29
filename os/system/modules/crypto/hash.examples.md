Step text: `hash %content%, write to %hash%`
Formal: `crypto.hash(Data=%content%)` — `Algorithm` is absent because the step does not name one.

Step text: `hash %content% with sha256, write to %hash%`
Formal: `crypto.hash(Data=%content%, Algorithm="sha256")`
