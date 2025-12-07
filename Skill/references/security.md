# Plang Security & Privacy

## Overview

Plang is designed to be the most secure programming language through:
- Local-first architecture
- Automatic request signing
- %Identity% instead of passwords
- Contained apps with limited permissions
- Transparent, verifiable code
- Event sourcing with encryption

## %Identity% - Passwordless Authentication

### What is %Identity%?

%Identity% is a unique string generated from your app's Ed25519 private key. It's:
- **Unique** to each user
- **Unforgeable** (only you can generate it)
- **Anonymous** (doesn't reveal personal information)
- **Automatically signed** on all HTTP/message/IO requests

### How to Use %Identity%

**Server-side authentication**:
```plang
// api/CreateOrder.goal
CreateOrder
- select * from users where identity=%Identity%, return 1, write to %userId%
- if %user.id% is empty, throw 401 "Unauthorized"
- insert into orders, user_id=%user.id%, amount=%amount%
```

**User registration (first time)**:
```plang
// api/Register.goal
Register
- select id from users where identity=%Identity%, write to %userId%
- if %userId% is empty
    - insert into users, identity=%Identity%, name=%name%, write to %userId%
- write %userId% to response
```

**Recommended pattern**:
```plang
/ events/Events.goal
Events
- before any goal, call goal LoadUser
- before any goal in /admin, call goal IsAdmin

LoadUser
/ events/LoadUser.goal
LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty
    - insert into users, identity=%Identity%, write to %userId%
    - select * from users where id=%userId%, return 1, write to %user%


/ /events/IsAdmin.goal
IsAdmin
- if %user.role% does not contain 'admin' then "Not an admin"
```

### Benefits of %Identity%

✅ **No passwords** - Nothing to forget, nothing to steal
✅ **No username** - One less piece of personal data
✅ **No password resets** - Problem doesn't exist
✅ **No database breach risk** - Identity is derived, not stored as secret
✅ **Automatic authentication** - Every request is signed
✅ **No session hijacking** - Each request independently authenticated

### %Identity% Security

**Tampering Protection**:
```plang
// All requests are automatically signed
// Server can verify:
// 1. Request came from claimed Identity
// 2. Request hasn't been modified
// 3. Request is recent (replay protection)
```

**Per-App Identity**:
Each app has its own Identity:
```
MyApp/
└── %Identity% = "abc123..."

MyApp/apps/StocksApp/
└── %Identity% = "xyz789..."  // Different!
```

## Private Keys

### Four Key Types Per App

Each Plang app automatically generates 4 types of private keys:

1. **Ed25519 (Identity)**
   - Creates %Identity%
   - Signs all requests
   - Verifies signatures

2. **AES256 (Encryption)**
   - Encrypts your data
   - Decrypts your data
   - Used in event sourcing

3. **EVM/Blockchain**
   - Enables payments
   - Interacts with smart contracts
   - Unique wallet address

4. **Nostr**
   - Sends private messages
   - Receives messages
   - Nostr protocol communication

### Key Storage

**Location**: `.db/system.sqlite` in each app

**Current status** (as of Sept 2024):
- ⚠️ Keys stored **unencrypted**
- Future: Bio/pin/face unlock planned

**Security per app**:
```
MyApp/
├── .db/
│   └── system.sqlite    # MyApp's keys
└── apps/
    ├── Contacts/
    │   └── .db/
    │       └── system.sqlite    # Contacts' keys
    └── Stocks/
        └── .db/
            └── system.sqlite    # Stocks' keys
```

**Isolation benefit**: If one app's keys leak, only that app's data is compromised.

### Accessing Your Identity

```plang
GetIdentity
- get my identity, write to %myIdentity%
- write out %myIdentity%

GetPublicKey
- get my public key, write to %publicKey%
```

## Encryption

### Data Encryption

**Automatic encryption**:
```plang
// Encrypt file/data
- encrypt %sensitiveData%, write to %encrypted%
- encrypt file.txt

// Decrypt file/data
- decrypt %encrypted%, write to %original%
- decrypt file.txt
```

**Event sourcing** (automatic):
- All database changes encrypted (AES256)
- Stored in `__Events__` table
- Enables sync between devices
- Encrypted with app's private key

### Password Hashing
Password should NOT be needed when using plang, but if needed

**ALWAYS hash passwords**:
```plang
❌ WRONG:
- insert into users, email=%email%, password=%password%

✅ CORRECT:
- hash %password%, write to %hashedPassword%
- insert into users, email=%email%, password=%hashedPassword%
```

**Hash algorithms**:
- **BCrypt** (default, with salt) - Use for passwords
- **Keccak256** (no salt) - Use for blockchain
- **SHA256** (available)

```plang
// BCrypt with salt (for passwords)
- hash %password%, write to %hashed%

// Keccak256 without salt (for blockchain addresses)
- hash %address% without salt, write to %hash%
```

## Message Signing

### Automatic Signing

All HTTP and message requests are **automatically signed**:

```plang
// Client side
- post https://api.example.com/data
    body: %data%
```

**What happens**:
1. Request signed with your private key
2. Signature included in request headers
3. Server receives: data + signature + %Identity%
4. Server verifies signature matches %Identity%

### Server-Side Verification

```plang
// Server automatically verifies signature
// If invalid, request is rejected before your code runs

ProcessRequest
// If this runs, request is verified
- select user_id from users where identity=%Identity%
```

### Manual Signing (Blockchain)

```plang
// Sign transaction for blockchain
- sign 10 usdc transfer to %recipientAddress%, write to %signature%
- send %signature% to server

// Server executes transaction (user pays no gas)
```

## Access Control

### File System Security

**Automatic containment**:
- Apps can only access files in their own folder
- Accessing parent folders requires **permission**

```plang
// In MyApp/Start.goal
Start
// Can access:
- read data.txt                    ✅
- read config/settings.json        ✅
- read subfolder/file.txt          ✅

// Cannot access without permission:
- read ../OtherApp/secrets.txt       ❌ Permission required
- read ../data.json                  ❌ Permission required
```

**Permission flow**:
1. App A requests access to App B's files
2. User is asked to approve
3. App B signs permission token
4. App A stores signed token
5. Future accesses automatic (until token expires)

### Network Security

**Signed requests**:
```plang
// All HTTP requests automatically include:
// - %Identity%
// - Signature
// - Timestamp (replay protection)

// Prevents:
// ✅ Man-in-the-middle attacks
// ✅ Request tampering
// ✅ Replay attacks
// ✅ Impersonation
```

## Privacy

### Local-First Architecture

**Data stays local**:
```
Your Computer
├── MyApp/
│   └── .db/
│       └── data.sqlite    # Your data lives here
└── Cloud backup (optional)
    └── Encrypted data only
```

**Benefits**:
- ✅ You own your data
- ✅ No server-side data breach exposure
- ✅ Offline-first operation
- ✅ Sync between devices (encrypted)

### No Personal Data Required

**Traditional registration**:
```
❌ Name: John Doe
❌ Email: john@example.com
❌ Password: secret123
❌ Phone: +1234567890
```

**Plang registration**:
```
✅ %Identity%: random_string_abc123
   (That's it!)
```

**When services need info**:
```plang
// Only provide what's necessary
Register
// Service doesn't need your email
// user is registered using only %Identity%

OrderProduct
// Service needs shipping address (obviously)
- insert into orders, %Identity%, address=%shippingAddress%
```

### Anonymous Analytics

**Server-side** (behavior tracking without personal data):
```plang
// Track user behavior
TrackEvent
- insert into events, identity_hash=SHA256(%Identity%), event=%eventName%, timestamp=%Now%

// Server knows:
// ✅ User abc123 did action X
// ❌ Who user abc123 is (no name, email, etc.)
```

### GDPR Compliance

**Plang makes GDPR easier**:
- ✅ No personal data stored (just %Identity%)
- ✅ Data stays local (minimal transfer)
- ✅ Easy data deletion (delete local .db folder)
- ✅ Data portability (copy .db folder)
- ✅ Transparent processing (verifiable code)

## Security Best Practices

### 1. Input Validation

```plang
❌ INSECURE:
CreateUser
- insert into users, %name%, %email%, %password%

✅ SECURE:
CreateUser
- make sure %name% is not empty
- make sure %email% is not empty
- make sure %email% contains @
- make sure %password% length >= 8
- hash %password%, write to %hashedPassword%
- insert into users, %name%, %email%, %hashedPassword%
```

### 2. SQL Injection Protection

**Built-in protection**:
```plang
// Plang automatically parameterizes queries
// This is SAFE:
- select * from users where email=%userInput%

// Becomes (in C#):
// SELECT * FROM users WHERE email=@email
// Parameters: { email: userInput }
```

**Still be careful**:
```plang
❌ AVOID dynamic SQL:
- [code] execute sql "SELECT * FROM users WHERE name='" + %name% + "'"

✅ USE parameterized:
- select * from users where name=%name%
```

### 3. Secure Settings Storage

On first usage of a %Settings.% key, plang will ask the system for the information.

```plang
// Store API keys in Settings (system.sqlite)
- set %Settings.ApiKey% = %apiKey%
- set %Settings.DatabaseUrl% = %dbUrl%

// Use Settings variables
GetData
- get %Settings.ApiUrl%/data
    bearer %Settings.ApiKey%
```

**Never hardcode secrets**:
```plang
❌ INSECURE:
- get https://api.example.com/data
    bearer "secret_key_12345"

✅ SECURE:
- get %Settings.ApiUrl%/data
    bearer %Settings.ApiKey%
```

### 4. Rate Limiting

```plang
// In Events.goal
Events
- before api/* call !CheckRateLimit

// CheckRateLimit.goal
CheckRateLimit
- select count from rate_limit where identity=%Identity% and time > %last_minute%
- if %count% > 100, throw 429 "Rate limit exceeded"
- insert into rate_limit, identity=%Identity%, time=%Now%
```

### 5. Error Handling

```plang
❌ EXPOSES INTERNALS:
ProcessPayment
- charge card %cardNumber%
// On error, shows: "Error: Invalid card number 1234-5678-9012-3456"

✅ SAFE ERRORS:
ProcessPayment
- charge card %cardNumber%
    on error throw "Payment processing failed"
// Generic error, no sensitive data exposed
```

## App Security

### Contained Apps

Apps from others run in containers:
```
MyApp/
└── apps/
    └── ThirdPartyApp/
        ├── Own memory space
        ├── Own private keys
        ├── Own file access (folder only)
        └── Requires permission for:
            - Parent folder access
            - Network access (future)
            - Other app access
```

### Code Verification

**Transparent code**:
```
.build/
└── CreateUser/
    ├── 0.Goal.pr          # JSON - human readable
    ├── 1.ValidateInput.pr # JSON - human readable
    └── 2.CreateUser.pr    # JSON - human readable
```

**Verify what app does**:
```plang
// You can inspect .pr files
// See exactly what each step does
// No hidden behavior
```

**Signed code** (planned):
```plang
// Future: Code signed by publisher
// Modified code = signature invalid = won't run
// Prevents virus-like behavior
```

## Common Security Patterns

### Pattern 1: User Authentication

```plang
// In Events.goal - runs before all API calls
Events
- on all goals in api/* call Authenticate

// Authenticate.goal
Authenticate
- select user_id from users where identity=%Identity%, write to %userId%
- if %userId% is empty, throw 401 "Unauthorized"
// If this succeeds, %userId% available in original goal
```

### Pattern 2: Role-Based Access

```plang
// Authenticate.goal
Authenticate
- select user_id, role from users where identity=%Identity%, write to %user%
- if %user% is empty, throw 401 "Unauthorized"
- set global %currentUser% = %user%

// api/admin/DeleteUser.goal
DeleteUser
- get global %currentUser%, write to %user%
- if %user.role% is not "admin", throw 403 "Forbidden"
- delete from users where id=%userId%
```

### Pattern 3: Data Encryption at Rest

```plang
// Store sensitive data encrypted
SaveSecret
- encrypt %secretData%, write to %encrypted%
- insert into secrets, user_id=%userId%, data=%encrypted%

// Retrieve and decrypt
GetSecret
- select data from secrets where user_id=%userId%, write to %encrypted%
- decrypt %encrypted%, write to %secretData%
```

### Pattern 4: Secure File Uploads

```plang
UploadFile
// Validate file type
- [code] get file extension from %fileName%, write to %ext%
- if %ext% not in ["jpg","png","pdf"], throw error "Invalid file type"

// Validate file size
- [code] get file size of %uploadedFile%, write to %size%
- if %size% > 10000000, throw error "File too large"  // 10MB

// Generate safe filename
- [code] generate random filename with extension %ext%, write to %safeFileName%
- save %uploadedFile% to ./uploads/%safeFileName%
```

### Pattern 5: Audit Logging

```plang
// In Events.goal
Events
- after each step in api/* call !AuditLog

// AuditLog.goal
AuditLog
- insert into audit_log
    identity=%Identity%
    goal=%__GoalName__%
    step=%__StepText__%
    timestamp=%Now%
```

## Security Checklist

✅ **Input Validation**
- [ ] Validate all user inputs
- [ ] Check for empty/null values
- [ ] Validate format (email, phone, etc.)
- [ ] Check length constraints

✅ **Authentication & Authorization**
- [ ] Use %Identity% instead of passwords
- [ ] Implement authentication in Events.goal
- [ ] Check authorization before sensitive operations
- [ ] Use role-based access control

✅ **Data Protection**
- [ ] Hash passwords (never store plain)
- [ ] Encrypt sensitive data at rest
- [ ] Use %Settings% for API keys
- [ ] Never hardcode secrets

✅ **Secure Communication**
- [ ] All HTTP requests automatically signed
- [ ] Verify signatures on server
- [ ] Use HTTPS in production
- [ ] Implement rate limiting

✅ **Error Handling**
- [ ] Don't expose sensitive data in errors
- [ ] Log errors for debugging
- [ ] Return generic error messages to users
- [ ] Handle all edge cases

✅ **Code Security**
- [ ] Review .pr files in .build folder
- [ ] Verify third-party apps before installing
- [ ] Keep Plang runtime updated
- [ ] Follow principle of least privilege

## Threat Mitigation

### XSS (Cross-Site Scripting)
✅ **Not applicable** - Apps run locally, no remote code injection

### SQL Injection
✅ **Protected** - Automatic parameterization of queries

### CSRF (Cross-Site Request Forgery)
✅ **Protected** - Signed requests prevent CSRF

### Session Hijacking
✅ **Not applicable** - No sessions, each request independently authenticated

### MITM (Man-in-the-Middle)
✅ **Protected** - Signed requests prevent tampering

### Password Attacks
✅ **Not applicable** - No passwords, using %Identity% instead

### Data Breaches
✅ **Minimized** - Data stored locally, only %Identity% on server

### Phishing
✅ **Reduced** - Apps verify sender via signatures

### Directory Traversal
✅ **Protected** - Apps contained to own folders

## Summary

Plang's security model:
1. ✅ **%Identity%** replaces passwords
2. ✅ **Automatic signing** of all requests
3. ✅ **Local-first** data storage
4. ✅ **Encrypted** event sourcing
5. ✅ **Contained** apps with permissions
6. ✅ **Transparent** verifiable code
7. ✅ **Per-app** private keys
8. ✅ **Built-in** SQL injection protection
9. ✅ **Privacy-first** architecture
10. ✅ **GDPR-friendly** by design

**Critical**: Always validate inputs, hash passwords, use %Identity%, and verify code before running third-party apps.