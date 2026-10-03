Step text: `sign %payload%, write to %signed%`
Properties: `{"Data": "%payload%"}`

Step text: `sign %data% with contracts ['C1']`
Properties: `{"Data": "%data%", "Contracts": ["C1"]}`

Step text: `sign %doc%, expires in 5 minutes, write to %signed%`
Properties: `{"Data": "%doc%", "Expires": "PT5M"}`
