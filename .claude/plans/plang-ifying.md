# Plan: PLang-ification Opportunities

## Summary

Exploration of the PLang codebase to identify C# code that could be converted to PLang native code, leveraging existing PLang modules instead of direct .NET implementations.

---

## Bootstrap Analysis (Critical Finding)

### PLang Startup Sequence
1. **Container Registration** (Layers 1-11)
   - FileSystem → Logging → Context → Parsing → Settings → Engine → Modules
2. **Engine Initialization** (`engine.Init(container)`)
3. **Executor.Run** starts actual PLang execution

### What MUST Stay C# (Bootstrap-Critical)
| Service | Reason |
|---------|--------|
| `IPLangFileSystem` | Needed to locate .goal files |
| `ILogger` | Needed for error reporting during bootstrap |
| `IPrParser` | Needed to parse .pr files |
| `ISettings` / `SqliteSettingsRepository` | Needed for identity keys before runtime |
| `IEngine` | Core runtime itself |

### What CAN Be PLang-ified (Post-Bootstrap)
| Service | Current | Can Use |
|---------|---------|---------|
| `PLangLlmService` HTTP calls | Raw HttpClient | HttpModule |
| `OpenAiService` HTTP calls | Raw HttpClient | HttpModule |
| `LlmCaching` | Direct SQLite | DbModule via PLang goals |
| `SqliteEventSourceRepository` | Direct SQLite | DbModule |

---

## Key Findings

### Tier 1: High-Value Candidates (Infrastructure Services)

#### 1. Settings Management - `SqliteSettingsRepository.cs`
**Location:** `PLang/Services/SettingsService/SqliteSettingsRepository.cs`
- Lines 141-158: Direct `File.Exists()`, `Directory.CreateDirectory()`, `File.Create()`
- Lines 160-199: Direct `SqliteConnection` and raw SQL for table creation
- Lines 258-288: Database queries for Get/Set/Remove operations

**Current:** C# service with raw SQLite and file operations
**Could be:** PLang goals using DbModule + FileModule
**Impact:** Settings could become declarative PLang configuration

#### 2. LLM Query Caching - `LlmCaching.cs`
**Location:** `PLang/Services/LlmService/LlmCaching.cs`
- Lines 13-50: File/directory creation for cache database
- Lines 36-47: Raw SQL for cache table creation
- Lines 85-103: Dapper queries for cache operations

**Current:** C# class managing SQLite cache
**Could be:** PLang goals for cache management
**Impact:** Cache strategy could be configurable via PLang

#### 3. Variable Persistence - `SqliteVariable.cs`
**Location:** `PLang/Services/VariableService/SqliteVariable.cs`
- Lines 156-171: Dynamic table creation with `SqliteConnection`
- Lines 73-135: Complex Save<T> method for variable storage

**Current:** C# utility for persisting typed variables
**Could be:** First-class PLang feature using DbModule
**Impact:** Core PLang capability becomes PLang-native

### Tier 2: Service Layer Candidates

#### 4. HTTP Operations (Outside HttpModule)
| File | Lines | Purpose | Candidate |
|------|-------|---------|-----------|
| `PLangLlmService.cs` | 319-341 | Payment link request | YES |
| `OpenAiService.cs` | 70-101 | OpenAI API calls | YES |
| `PLangAppsRepository.cs` | 23-28 | App downloads | PARTIAL |

#### 5. Event Source Repository - `SqliteEventSourceRepository.cs`
**Location:** `PLang/Services/EventSourceService/SqliteEventSourceRepository.cs`
- Database audit/event logging with direct SQL
- Could use DbModule for all operations

#### 6. App Installation - `PLangAppsRepository.cs`
**Location:** `PLang/Services/AppsRepository/PLangAppsRepository.cs`
- Downloads ZIP from GitHub, extracts to apps folder
- Could be a PLang workflow: HTTP download → extract → setup

### Tier 3: Utility Patterns

#### 7. JSON Serialization (8+ files)
Direct `JsonConvert.SerializeObject/DeserializeObject` in:
- `LlmCaching.cs`, `SigningService.cs`, `Encryption.cs`
- `GoalBuilder.cs`, `InstructionBuilder.cs`, `GoalParser.cs`

**Could use:** SerializerModule

#### 8. Hash Computation (4+ files)
`.ComputeHash().Hash` extension method calls in:
- `LlmCaching.cs`, `GoalBuilder.cs`

**Could use:** CryptographicModule.Hash()

#### 9. Directory Operations - `DirectoryHelper.cs`
Direct `Directory.CreateDirectory()`, `FileInfo.CopyTo()` operations
**Already available in:** FileModule

#### 10. Compression - `ZipArchive.cs`
Direct `ZipFile.CreateFromDirectory()` calls
**Already available in:** CompressionModule

---

## Architecture Consideration

### What Makes Sense to PLang-ify?

**Good candidates:**
- Runtime services that users might want to customize
- Operations that benefit from PLang's event system (before/after hooks)
- Features where declarative configuration is valuable

**Keep in C#:**
- Build-time compiler infrastructure (needs to run before PLang is available)
- Low-level bootstrap code (PLangFileSystem path resolution)
- Performance-critical hot paths

---

## Implementation Plan

### Phase 1: HTTP Service Consistency (LOW RISK) ⭐ Recommended Start

**Goal:** Replace raw `HttpClient` with injected `HttpModule.Program`

#### File 1: `PLang/Services/LlmService/PLangLlmService.cs`
**Lines 319-341** - `DoPlangRequest` method uses raw HttpClient

**Current:**
```csharp
using (var httpClient = new HttpClient())
{
    var request = new HttpRequestMessage(httpMethod, requestUrl + "/api/GetOrCreatePaymentLink");
    // ... manual setup
}
```

**Proposed:**
```csharp
private async Task<string> DoPlangRequest(object[] countryArray)
{
    var country = countryArray[0].ToString();
    var requestUrl = url.Replace("api/Llm", "").TrimEnd('/') + "/api/GetOrCreatePaymentLink";

    var parameters = new Dictionary<string, object?>
    {
        { "name", nameOfPayer },
        { "country", country }
    };

    // Use injected HttpModule (already available as 'http' field)
    var result = await http.Post(requestUrl, parameters, doNotSignRequest: false, timeoutInSeconds: 30);
    return result.Data?.ToString() ?? string.Empty;
}
```

**Note:** This service already uses `http.Post()` at line 151, so DoPlangRequest is inconsistent.

#### File 2: `PLang/Services/LlmService/OpenAiService.cs`
**Lines 70-101** - Entire Query method uses raw HttpClient

**Changes needed:**
1. Add `HttpModule.Program http` to constructor injection
2. Replace manual HttpClient with `http.Post()`

---

### Phase 2: LLM Caching via PLang Goals (MEDIUM RISK)

**Goal:** Create PLang goals for LLM cache operations

#### New Goals: `system/llm-cache/`

**Setup.goal:**
```plang
Setup
- create data source 'llm-cache', type: sqlite, keep history: false
- [sql] CREATE TABLE IF NOT EXISTS LlmCache (
    Id INTEGER PRIMARY KEY,
    Hash TEXT NOT NULL UNIQUE,
    LlmQuestion TEXT NOT NULL,
    Created DATETIME DEFAULT CURRENT_TIMESTAMP,
    LastUsed DATETIME DEFAULT CURRENT_TIMESTAMP
  )
```

**GetCache.goal:**
```plang
GetCache
- select * from LlmCache where Hash = %hash%, write to %cached%
- if %cached% is not empty
    - update LlmCache set LastUsed = CURRENT_TIMESTAMP where Hash = %hash%
- return %cached%
```

**SetCache.goal:**
```plang
SetCache
- insert into LlmCache (Hash, LlmQuestion) values (%hash%, %questionJson%)
```

#### C# Changes: `LlmCaching.cs`
Add runtime check to use PLang goals when available, fallback to C# otherwise.

---

### Phase 3: Event Source via PLang (MEDIUM RISK)

Similar pattern to Phase 2 - create `system/event-source/` goals.

---

### Phase 4: Settings Repository - DO NOT IMPLEMENT

**Reason:** Bootstrap-critical. Settings are needed to:
- Get identity keys (for signing HTTP requests)
- Get encryption keys (for secure storage)
- Store build settings

PLang-ifying this would require complex fallback logic and risks breaking identity/encryption.

---

### Phase 5: Utility Patterns - DEFER

JSON serialization, hashing, compression in C# code is fine:
- Internal C# patterns are appropriate for runtime infrastructure
- PLang code already uses the respective modules
- No user-facing benefit to changing internal patterns

---

## Risk Matrix

| Phase | Risk | Complexity | Value | Priority |
|-------|------|------------|-------|----------|
| 1: HTTP Services | LOW | LOW | MEDIUM | **DO** |
| 2: LLM Caching | MEDIUM | MEDIUM | LOW | CONSIDER |
| 3: Event Source | MEDIUM | MEDIUM | LOW | DEFER |
| 4: Settings | HIGH | HIGH | LOW | **DO NOT** |
| 5: Utilities | LOW | LOW | LOW | DEFER |

---

## Critical Files

| File | Phase | Change Type |
|------|-------|-------------|
| `PLang/Services/LlmService/PLangLlmService.cs` | 1 | Refactor DoPlangRequest |
| `PLang/Services/LlmService/OpenAiService.cs` | 1 | Add HttpModule injection |
| `PLang/Services/LlmService/LlmCaching.cs` | 2 | Add PLang goal integration |
| `system/llm-cache/*.goal` | 2 | New PLang goals |

---

## Verification

1. **Phase 1:** Run `plang build` on any project - verify LLM calls still work
2. **Phase 2:** Build a project twice - verify cache hits work
3. Test both online (fresh) and offline (cached) scenarios
