Step text: `read file.txt, write to %content%`
Properties: `{"Path": "file.txt"}` — the trailing `write to %content%` is its own action.

Step text: `read 'config/settings.json'`
Properties: `{"Path": "config/settings.json"}`

Step text: `read 'receipt.txt', load vars, write to %receipt%`
Properties: `{"Path": "receipt.txt", "Template": "plang"}` — "load vars" (also "fill in the variables", "with variables") makes the file's text a plang template, its %variables% filled from memory; a plain read with none of these leaves Template out. The trailing `write to %receipt%` is its own action.
