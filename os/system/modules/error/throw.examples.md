Step text: `throw %!error%`
Properties: `{"Data": "%!error%"}` — a variable goes in Data alone; never `Message=%!error%`, never both.

Step text: `throw "checkout failed"`
Properties: `{"Message": "checkout failed"}`

Step text: `throw "not found", status 404`
Properties: `{"Message": "not found", "Status": 404}`

Step text: `throw "the build has broken goals", cause %broken%`
Properties: `{"Message": "the build has broken goals", "Cause": "%broken%"}` — the list of errors after `cause` goes in Cause, not Data.
