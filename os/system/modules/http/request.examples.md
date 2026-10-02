Step text: `get https://api.example.com/rates, write to %rates%`
Properties: `{"Url": "https://api.example.com/rates"}` — GET is the default method; the trailing `write to %rates%` is its own action.

Step text: `post https://api.example.com/speech, headers Authorization: "Bearer %key%", body {"model": "tts", "input": "%text%"}, write to %audio%`
Properties: `{"Url": "https://api.example.com/speech", "Method": "POST", "Header": {"Authorization": "Bearer %key%"}, "Body": {"model": "tts", "input": "%text%"}}` — a json/dict `body` is the request's Body; posting data is a request, never an upload.
