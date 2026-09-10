# CryptographicModule

Hint: `[cryptographic]`  
Type: `PLang.Modules.CryptographicModule.Program`

Encrypt, descryption and hashing & verify using bcrypt, compute Sha256Hash, generate & validate Bearer token

## Methods

### AddPrivateKey

```
AddPrivateKey(String privateKey) : object
```


### ConvertFromBase64

```
ConvertFromBase64(String base64) : Byte[]
```


### ConvertToBase64

```
ConvertToBase64(String content) : String
```


### CreateSalt

```
CreateSalt(Int32 workFactor = 12) : String
```


### CreateToken

```
CreateToken(String password = null, Int32 validForSeconds = 600, String secretKeyName = null) : String
```

Creates a simple token that is valid for x seconds using HMACSHA256. When password is empty, the runtime uses password from settings


### Decrypt

```
Decrypt(String content) : Object
```


### Encrypt

```
Encrypt(Object content) : String
```


### GenerateBearerToken

```
GenerateBearerToken(String uniqueString, String issuer = PLangRuntime, String audience = user, Int32 expireTimeInSeconds = 604800) : String
```


### GetBearerSecret

```
GetBearerSecret() : String
```


### GetHashOfFile

```
GetHashOfFile(String filePath, String hashAlgorithm = sha256, String encoding = base64) : String
```


### GetPrivateKey

```
GetPrivateKey() : String
```


### GetPrivateKeyHash

```
GetPrivateKeyHash() : String
```


### Hash

```
Hash(Object variable, Nullable<Boolean> useSalt = null, String salt = null, String type = keccak256) : Object
```

Hash input. Salt is provided by language when user does not provide. hashAlgorithm: keccak256 | sha256 | bcrypt


### HashHmacShaInput

```
HashHmacShaInput(String input, String secretKey = null, Int32 hashSize = 256) : String
```

Hmac hash sizes are 256, 384, 512


### HashIdentityString

```
HashIdentityString(String identity) : String
```

Hashes Identity string to standard hash


### HashInput

```
HashInput(Object variable, Boolean useSalt = True, String salt = null, String hashAlgorithm = keccak256) : String
```

Hash input with salt, such as password. hashAlgorithm: keccak256 | sha256 | bcrypt


### HashPassword

```
HashPassword(Object variable, Boolean returnAsString = False, Boolean useSalt = True, String salt = null, String type = keccak256) : Object
```

Hash input, useSalt = true for passwords. Salt is provided by language when use does not provide. hashAlgorithm: keccak256 | sha256 | bcrypt


### SetCurrentBearerToken

```
SetCurrentBearerToken(String name) : object
```


### ValidateBearerToken

```
ValidateBearerToken(String token, String issuer = PLangRuntime, String audience = user) : Boolean
```


### ValidateToken

```
ValidateToken(String token, String password = null, String secretKeyName = null) : object
```

Validates token (HMACSHA256) when password is null the runtime uses password from settings


Examples:

- validate %token% => token=%token%

### VerifyHashedValues

```
VerifyHashedValues(String text, String hash, String hashAlgorithm = keccak256, Boolean useSalt = True, String salt = null) : Boolean
```

Used to verify hash. hashAlgorithm: keccak256 | sha256 | bcrypt


### VerifyHashOfFile

```
VerifyHashOfFile(String filePath, String expectedHash, String hashAlgorithm = sha256, String encoding = base64) : Boolean
```

Used to verify hash comparing file and a hash. hashAlgorithm: md5 | sha1 | sha256 | sha512. encoding: base64|hex


