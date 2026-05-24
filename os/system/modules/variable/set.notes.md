When the **intent** is to assign a value to a named variable with an "only-if-unset" flag (PLang `set %x% = … as default`, "default to", and equivalents in other languages), use plain `variable.set` plus `{"name":"AsDefault","value":true,"type":"bool"}`.

The action for variable assignment is ALWAYS `variable.set` — **never** `code.setDefault`. `code.setDefault` selects a default *provider* (signing/crypto/identity/key role) and has nothing to do with assigning a value to a `%variable%`. Match by the intent (assigning to a variable), not by surface words like "default" in the step text.
