# Plang Security & Privacy

## Overview

Plang is designed to be the most secure programming language through:
- Local-first architecture
- Automatic request signing
- %Identity% instead of passwords
- Contained apps with limited permissions
- Transparent, verifiable code
- Event sourcing with encryption
- **Modularization** - developers use high-level steps like `- hash %input%` without needing to know cryptographic implementation details, reducing mistakes that occur in traditional languages where developers must implement security correctly themselves

## %Identity% - Passwordless Authentication

### What is %Identity%?

%Identity% is the public key derived from the client's private key. It's:
- **Unique** to each client/device
- **Unforgeable** (cryptographically signed)
- **Anonymous** (doesn't reveal personal information)
- **Automatically included** on all signed HTTP/message/IO requests

**Cryptographic algorithms:**
- **Browser ↔ Server**: ECDSA-SHA-256 (better browser support)
- **PLang App ↔ PLang App**: Ed25519

### How It Works

1. **Private key lives on the client** (browser, app, desktop) - PLang handles key creation automatically, developers don't need to manage this
2. **Every request is signed** with this private key
3. **PLang framework validates** the signature on the server
4. **If signature is valid**, `%Identity%` is set to the public key
5. **If signature is invalid**, the request is rejected

**Key concept:**
- `%Identity%` not empty = **Authenticated** (valid signed request)
- Authenticated ≠ Authorized (authorization is your business logic)

### Identity Creation Flow (Web Apps)

1. **First GET request**: Browser has no identity yet, `%Identity%` is **empty**
2. **PLang client library**: Creates identity (generates key pair) in browser
3. **Subsequent requests**: All links, forms, fetch, `plang.callGoal()` are automatically signed (PLang JS framework overwrites fetch)
4. **Server receives**: Valid signature → `%Identity%` available

**Handle first visit:**
```plang
Landing
- if %Identity% is empty then
    / First visit, render page so browser can create identity
    - [ui] render "landing.html", navigate
    - end goal
/ Identity exists from signed request
- call goal LoadUser
```

### Simple Pattern (One Identity Per User)

```plang
Events
- before each goal, call LoadUser

LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - insert into users, identity=%Identity%, created=%now%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%
```

### Multi-Device Pattern (Multiple Identities Per User)

For users accessing from multiple devices, use separate tables. See [patterns-and-conventions.md](patterns-and-conventions.md) for the complete flow with email verification.

```plang
/ Schema
- create table users, columns: email(string, unique), created(datetime, default now)
- create table identities, columns: 
    identity(string, unique), 
    userId(foreign key to users.id, null),
    accessCode(string),
    accessCodeCreated(datetime)
```

Flow: Identity created → User requests access → Email verification → Identity linked to user

### Benefits of %Identity%

✅ **No passwords** - Nothing to forget, nothing to steal
✅ **No password resets** - Problem doesn't exist
✅ **No database breach risk** - Identity is derived, not stored as secret
✅ **Automatic authentication** - Every signed request is authenticated
✅ **No session hijacking** - Each request independently verified
✅ **Cryptographic proof** - `%!Signature%` can record user consent

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

**Current status** (as of Jan 2026):
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

### Hashing Sensitive Data

PLang uses `%Identity%` for authentication - **passwords should NOT be used**. However, if you need to hash other sensitive data:

**Hash algorithms**:
- **BCrypt** (default, with salt) - For sensitive data that needs to be verified
- **Keccak256** (no salt) - For blockchain addresses
- **SHA256** (available)

```plang
// BCrypt with salt
- hash %sensitiveData%, write to %hashed%

// Keccak256 without salt (for blockchain addresses)
- hash %address% without salt, write to %hash%
```

**CRITICAL**: Do not implement password-based authentication in PLang. Use `%Identity%` which provides cryptographically signed, passwordless authentication.

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
ProcessOrder
- insert into orders, %customerId%, %amount%

✅ SECURE:
ProcessOrder
- make sure %customerId% is not empty
- make sure %amount% > 0
- make sure %amount% <= 1000000
- insert into orders, %customerId%, %amount%
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

PLang automatically parameterizes all database queries, making SQL injection virtually impossible through normal usage.

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
- before each goal call CheckRateLimit

// CheckRateLimit.goal
CheckRateLimit
- select count from rate_limit where identity=%Identity% and time > %last_minute%
- if %count% > 100, throw 429 "Rate limit exceeded"
- insert into rate_limit, identity=%Identity%, time=%Now%
```

### 5. Error Handling

```plang
❌ EXPOSES INTERNALS:
ProcessFile
- read %filePath%, write to %content%
// On error, might show: "Error: File not found at /home/user/secret/data.json"

✅ SAFE ERRORS:
ProcessFile
- read %filePath%, write to %content%
    on error call HandleFileError

HandleFileError
- log error "File read failed: %!error.Message%"
- throw "Unable to process file"
// Generic error to user, detailed error logged for debugging
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
- before each goal call Authenticate

// Authenticate.goal
Authenticate
- select user_id from users where identity=%Identity%, write to %userId%
- if %userId% is empty, throw 401 "Unauthorized"
// If this succeeds, %userId% available in original goal
```

### Pattern 2: Role-Based Access

```plang
// Events.goal - runs before each request
Events
- before each goal, call LoadUser
- before each goal in /admin/.*, call CheckAdmin

// LoadUser.goal
LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - insert into users, identity=%Identity%, created=%now%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%

// CheckAdmin.goal
CheckAdmin
- if %user.role% does not contain "admin", throw 403 "Forbidden"

// admin/DeleteUser.goal
DeleteUser
/ %user% is already loaded by Events, and admin check passed
- delete from users where id=%targetUserId%
```

### Pattern 3: Isolated User Data (Advanced)

Create separate datasources per user for complete data isolation. This is valuable for GDPR compliance and data separation.

**Setup user's private database:**
```plang
SetupUserData
- create datasource "users/%user.id%"
- create table documents, columns:
    title(string, not null),
    content(string),
    created(datetime, default now)
- create table bookmarks, columns:
    url(string, not null),
    title(string),
    created(datetime, default now)
```

**Store user-specific data:**
```plang
SaveBookmark
- insert into bookmarks, url=%url%, title=%title%, datasource: "users/%user.id%"

GetUserDocuments
- select * from documents, ds: "users/%user.id%", write to %documents%
```

**GDPR deletion - delete entire user database:**
```plang
DeleteUserData
/ Simply delete the user's database file - all their data is gone
- delete file ".db/users/%user.id%.sqlite"
- delete from users where id=%user.id%
```

**Benefits:**
- ✅ Complete data isolation between users
- ✅ GDPR "right to be forgotten" - just delete one file
- ✅ No risk of accidentally exposing other users' data
- ✅ Simplified data export per user

**Note:** This pattern only works with SQLite. It adds complexity but provides strong data separation guarantees.

### Pattern 4: Data Encryption at Rest

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

### Pattern 5: Secure File Uploads

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

### Pattern 6: Audit Logging

```plang
// In Events.goal
Events
- after each step call AuditLog

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

**Critical**: Always validate inputs, use %Identity% for authentication, and verify code before running third-party apps.