Step text: `throw %!error%`
Properties: `{"Data": "%!error%"}` — a variable goes in Data alone; never `Message=%!error%`, never both.

Step text: `throw "checkout failed"`
Properties: `{"Message": "checkout failed"}`

Step text: `throw "not found", status 404`
Properties: `{"Message": "not found", "Status": 404}`
