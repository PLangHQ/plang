Step text: `sign %payload%, write to %signed%`
Formal: `signing.sign(Data=%payload%)`

Step text: `sign %data% with contracts ['C1']`
Formal: `signing.sign(Data=%data%, Contracts=["C1"])`

Step text: `sign %doc%, expires in 5 minutes, write to %signed%`
Formal: `signing.sign(Data=%doc%, Expires="PT5M")`
