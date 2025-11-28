# Plang Skill Documentation

This skill provides comprehensive knowledge about Plang (plang.is), a natural language programming language that compiles pseudo-code into executable instructions.

## Skill Structure

### Core Documentation

**SKILL.md** - Main skill file with workflows and when to use Plang tools

### References

Detailed documentation organized by topic:

1. **conventions.md** - Common mistakes and how to fix them
   - Setup vs Start (critical distinction!)
   - Variable naming
   - Goal calling patterns
   - Anti-patterns to avoid
   - Migration patterns

2. **project-structure.md** - Folder organization and file placement
   - Standard project layout
   - When to use Setup.goal vs Setup/ folder
   - API endpoint mapping
   - Module and service organization
   - Apps and containment

3. **patterns.md** - Architectural patterns (EXISTING FILE)
   - Goal organization
   - Code structure
   - Reusability patterns

4. **syntax.md** - Language syntax reference (EXISTING FILE)
   - Variables
   - Comments
   - Goal structure
   - Step syntax

5. **database.md** - Database operations (EXISTING FILE)
   - Table creation
   - CRUD operations
   - Queries
   - Event sourcing

6. **modules.md** - Deep dive into module capabilities
   - 30+ built-in modules
   - Usage examples for each
   - When to use which module
   - Custom module creation

7. **security.md** - Security patterns and Identity usage
   - %Identity% (passwordless authentication)
   - Private keys
   - Encryption
   - Access control
   - Privacy patterns

8. **error-handling.md** - Error patterns and validation
   - Input validation
   - Throwing errors
   - Try-catch via Events
   - Retry logic
   - Error logging

## Key Concepts

### Critical: Setup vs Start

This is the #1 confusion point for new Plang developers:

**Setup.goal / Setup/** = ONE-TIME initialization
- Database table creation
- Schema modifications
- Configuration files
- Initial data seeding
- Each step runs ONCE (tracked in system.sqlite)

**Start.goal / Start/** = Application ENTRY POINT
- Runs EVERY TIME app starts
- Start webserver
- Start UI
- Listen to message queues
- Schedule recurring tasks

**Common mistake**: Creating tables in Start.goal
```plang
❌ WRONG - Start.goal:
Start
- create table users, columns: id, name, email
- start webserver

✅ CORRECT:
// Setup.goal
Setup
- create table users, columns: id(int, pk), name(string, not null), email(string, not null)

// Start.goal
Start
- start webserver on port 8080
```

### %Identity% - Passwordless Authentication

Plang's signature feature for security:
- Unique string derived from Ed25519 private key
- Automatically included in all HTTP/message requests
- No passwords needed
- No username needed
- Tamper-proof (requests are signed)

```plang
// Server-side authentication
AuthenticateUser
- select user_id from users where identity=%Identity%, write to %userId%
- if %userId% is empty, throw 401 "Unauthorized"
```

### Event Sourcing

Automatic for SQLite databases:
- All INSERT/UPDATE/DELETE encrypted and stored
- Enables sync between devices
- Complete history available
- Privacy-preserving (encrypted with app's keys)

### Goals and Steps

**Goal** = Function/method (your business logic)
**Step** = Single operation within a goal

```plang
// CreateUser is a Goal
CreateUser
- make sure %email% is not empty           // Step 1
- hash %password%, write to %hashed%       // Step 2
- insert into users, %email%, %hashed%     // Step 3
```

## Quick Reference

### Project Structure
```
MyApp/
├── Start.goal              # Entry point (required)
├── Setup.goal              # One-time init (optional)
├── Events.goal             # Event bindings (optional)
├── api/                    # REST endpoints
├── .build/                 # Generated code (don't edit)
└── .db/                    # Databases (auto-generated)
    ├── system.sqlite       # Settings, keys
    └── data.sqlite         # App data
```

### Essential Patterns

**Goal calling**:
```plang
- call !ProcessUser %userData%
- call !api/users/GetUser %userId%, write to %user%
```

**Variable syntax**:
```plang
- read file.txt into %content%
- hash %password%, write to %hashed%
```

**Validation**:
```plang
- make sure %email% is not empty
- make sure %email% contains @
```

**Error handling**:
```plang
- if %user% is empty, throw 404 "User not found"
- on error call !HandleError %error%
```

**Module hints**:
```plang
- [file] read data.txt
- [db] select * from users
- [http] get https://api.example.com
```

## When to Read Each File

### Starting a new project?
Read: **project-structure.md**, **conventions.md**

### Common errors in your code?
Read: **conventions.md**, **error-handling.md**

### Need to understand a specific module?
Read: **modules.md**

### Working with databases?
Read: **database.md**, **patterns.md**

### Security/authentication questions?
Read: **security.md**

### Syntax questions?
Read: **syntax.md**

### Architecture decisions?
Read: **patterns.md**, **project-structure.md**

## Common Questions

**Q: Why won't my tables create?**
A: Tables should be in Setup.goal, not Start.goal. See conventions.md

**Q: How do I authenticate users?**
A: Use %Identity%, not passwords. See security.md

**Q: What's the difference between goals and steps?**
A: Goal = function, Step = line in that function. See syntax.md

**Q: How do I call another goal?**
A: `call !GoalName %parameters%` (note the ! prefix). See conventions.md

**Q: Where should my API endpoints go?**
A: In api/ folder. They auto-map to URLs. See project-structure.md

**Q: How do I handle errors?**
A: Multiple ways - validate inputs, throw errors, use Events.goal. See error-handling.md

**Q: What modules are available?**
A: 30+ modules including db, file, http, llm, blockchain, etc. See modules.md

**Q: How does %Identity% work?**
A: It's a unique, signed identifier for each user. See security.md

## File Organization Decision Tree

```
Creating database tables?
└─> Use Setup.goal or Setup/CreateTables.goal

Starting webserver or listening to queues?
└─> Use Start.goal

Need to authenticate all API calls?
└─> Use Events.goal with "on all goals in api/*"

Creating REST API endpoints?
└─> Create goals in api/ folder

User interface goals?
└─> Create goals in ui/ folder

Shared business logic?
└─> Create goals in services/ or lib/ folder

Custom C# code?
└─> Create module in modules/ folder

Third-party Plang apps?
└─> They go in apps/ folder (auto-generated)
```

## Best Practices Summary

1. ✅ **Setup.goal** for ONE-TIME initialization
2. ✅ **Start.goal** for application entry point
3. ✅ Use `%variable%` syntax consistently
4. ✅ Prefix goal calls with `!`
5. ✅ Validate inputs before processing
6. ✅ Use %Identity% instead of passwords
7. ✅ Keep steps simple (one operation per step)
8. ✅ Use Events.goal for cross-cutting concerns
9. ✅ Cache expensive operations
10. ✅ Always hash passwords

## Anti-Patterns to Avoid

1. ❌ Creating tables in Start.goal
2. ❌ Not using %variable% syntax
3. ❌ Forgetting ! prefix on goal calls
4. ❌ Hard-coding API keys/secrets
5. ❌ Using "create" instead of "insert" for records
6. ❌ Not validating inputs
7. ❌ Storing plain passwords
8. ❌ Complex multi-part steps
9. ❌ Ignoring error handling
10. ❌ Not using %Identity% for auth

## Getting Help

1. Check conventions.md for common mistakes
2. Check project-structure.md for file placement
3. Check modules.md for module capabilities
4. Check error-handling.md for validation patterns
5. Check security.md for authentication patterns
6. Check official docs at plang.is

## Contributing

To improve this skill:
1. Add examples to appropriate reference files
2. Document new patterns as they emerge
3. Update conventions.md with new common mistakes
4. Add more module examples to modules.md

## Version

This skill is based on:
- Plang version: 0.1.18 (November 2025)
- White paper: "Programming 3.0 - Theory"
- Some features described may not be fully implemented yet

Check plang.is for latest updates and changes.