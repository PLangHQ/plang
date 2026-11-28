# IdentityModule

Hint: `[identity]`  
Type: `PLang.Modules.IdentityModule.Program`

Handles Identity in plang. Sign content and verifies signature with the identity.

## Methods

### ArchiveIdentity

```
ArchiveIdentity(String nameOrIdentity) : PLang.Interfaces.Identity
```

Archives a identity. Name or identity, identity could be a public address, 0x123....


### CreateIdentity

```
CreateIdentity(String name, Boolean setAsDefault = False) : PLang.Interfaces.Identity
```

Create a new identity in the system


### GetIdentities

```
GetIdentities() : PLang.Interfaces.Identity
```

Gets all identites in the system


### GetIdentity

```
GetIdentity(String nameOrIdentity) : PLang.Interfaces.Identity
```

Get an identity by name or identification


### GetMyIdentity

```
GetMyIdentity() : PLang.Interfaces.Identity
```

Get the current identity, also called %MyIdentity%


### GetPrivateKey

```
GetPrivateKey() : String
```


### RemoveSharedIdentity

```
RemoveSharedIdentity() : object
```


### SetCurrentIdentity

```
SetCurrentIdentity(String nameOrIdentity) : PLang.Interfaces.Identity
```

Set the current identity(MyIdentity). Name or identity, identity could be a public address, 0x123....


### Sign

```
Sign(Object body, List<String> contracts = null, Nullable<Int32> expiresInSeconds = null, Dictionary<String, Object> headers = null, Boolean skipNonce = False) : PLang.Models.SignedMessage
```

Sign a content with specific headers and contracts. Returns signature object that contains the values to validate the signature


### SignFile

```
SignFile(String path, List<String> contracts = null, Nullable<Int32> expiresInSeconds = null, String identity = default) : PLang.Models.SignedMessage
```

Sign a file with the current identity. Writes the signature next to it as <file>.signature. The signature covers the file's name and the sha256 of its bytes. identity is 'default' (the current identity) or the name of another identity of this app


### SignIntoProperty

```
SignIntoProperty(Object body, String property) : Object
```

Sign a object to a specific property on that object. Returns signature object that contains the values to validate the signature


### UseSharedIdentity

```
UseSharedIdentity() : object
```

Set the app to use shared identity. This is usefull when app is running on multiple location but want to use one identity for them all


### VerifyFile

```
VerifyFile(String path, List<String> contracts = null) : PLang.Models.SignedMessage
```

Verify a file against its <file>.signature. Returns the signature (its Identity says who signed) when the bytes, the name, the contract and the expiry match; an error otherwise, also when there is no .signature file


### VerifySignature

```
VerifySignature(Object signatureFromUser = null, Dictionary<String, Object> headers = null, Object body = null, List<String> contracts = null, Boolean requireData = False) : PLang.Models.SignedMessage
```

Validate a signature. Return the signature when valid, gives error when invalid

- `signatureFromUser` *Object*, default `null`
- `headers` *Dictionary<String, Object>*, default `null`
- `body` *Object*, default `null`
- `contracts` *List<String>*, default `null`
- `requireData` *Boolean*, default `False` — true when the step says the signature must cover the body, e.g. `body is required`. Without it a signature that carries no hash of its data is accepted for any body

### VerifySignatureOnProperties

```
VerifySignatureOnProperties(Object signatureFromUser, List<String> properties = null) : PLang.Models.SignedMessage
```

Validate a signature on specific properties


