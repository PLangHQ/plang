Step text: `hash %content%, write to %hash%`
Formal: `crypto.hash(Data: item = %content%)` — `Algorithm` is absent because the step does not name one.

Step text: `hash %content% with sha256, write to %hash%`
Formal: `crypto.hash(Data: item = %content%, Algorithm: choice<hash> = "sha256")`
