Step text: `read file.txt, write to %content%`
Properties: `{"Path": "file.txt"}` — the trailing `write to %content%` is its own action.

Step text: `read 'config/settings.json'`
Properties: `{"Path": "config/settings.json"}`

Step text: `read 'receipt.txt', load vars, write to %receipt%`
Properties: `{"Path": "receipt.txt", "Variables": true}` — "load vars" (also "fill in the variables", "with variables") fills the %variables% in the file's text before returning; a plain read with none of these does not. The trailing `write to %receipt%` is its own action.
