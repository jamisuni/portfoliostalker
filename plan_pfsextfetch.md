# Plan: PfsExtFetch Code Audit & Test Coverage Proposal

## Context

After completing the Stalker domain model audit (BUG-1 through BUG-7) and building 142 tests for PfsData, the next target is `PFS/PfsExtFetch/` — the EOD fetch orchestration layer (~1,500 lines across 4 source files + 2 interfaces). This code coordinates multiple stock data providers, manages API credit budgets, queues fetch requests, and routes results to storage. It currently has **zero test coverage**.

This plan is **analysis-only** — cataloguing bugs found and proposing a test strategy for future implementation.

---

## Files Analysed

| File | Lines | Role |
|------|-------|------|
| `FetchEod.cs` | 657 | Main orchestrator (IFetchEod, ICmdHandler, IOnUpdate, IDataOwner) |
| `FetchEodTask.cs` | 427 | Per-provider state machine wrapper |
| `FetchEodPending.cs` | 236 | Two-tier priority/general pending queue |
| `FetchRates.cs` | 119 | Currency rate fetching |
| `Interface/IFetchEod.cs` | 34 | Public EOD fetch interface |
| `Interface/IFetchRates.cs` | 29 | Public rates interface |

---

## Bugs Found

### BUG-8: FindBySymbolAsync — Inverted market logic
- **File:** `FetchEod.cs:240-243`
- **Severity:** High — search returns wrong results
- **Description:** When `optMarketId == Unknown` (no market specified), it passes `[optMarketId]` (just Unknown) — should search all active markets. When `optMarketId != Unknown` (market specified), it searches all actives — should search just the specified market. The conditions are swapped.
- **Fix:** Swap the if/else branches.

### BUG-9: MonthsRemainingWorkDays — Double-counts current day
- **File:** `FetchEodTask.cs:222-233`
- **Severity:** Low — off-by-one in credit allocation
- **Description:** Current day is counted at line 227-228 (`ret++`), then the `for` loop starting at line 229 also counts it again via `AddWorkingDays(+1)` starting from today. Result is one extra workday, giving slightly more daily credits than intended from monthly pool.
- **Also:** Missing space at line 227: `DayOfWeek.Saturday&&` (cosmetic).

### BUG-10: FetchRates.ConfigureAwait — No-op misuse
- **File:** `FetchRates.cs:71`
- **Severity:** Cosmetic — `_workerThread.ConfigureAwait(false)` called on a `Task` reference but the return value is discarded. `ConfigureAwait` returns a `ConfiguredTaskAwaitable` that must be awaited to have effect. Same issue in `FetchEodTask.cs:264`.
- **Fix:** Remove both lines (fire-and-forget doesn't need ConfigureAwait).

### BUG-11: FetchEod.Fetch — Silent rejection when pending > 0
- **File:** `FetchEod.cs:170-171`
- **Severity:** Medium — user action silently ignored
- **Description:** If a previous fetch still has pending items, `Fetch()` returns `void` with no indication of failure. The UI caller has no way to know the request was dropped.
- **Fix:** Change return type to `Result` or log a warning. (Interface change needed.)

### CODE-MARKERS: Existing TODO/THINK items
- `FetchEod.cs:78` — `!!!THINK!!!` restore backups issue
- `FetchEod.cs:352` — `!!!TODO!!!` Activate BATCH later
- `FetchEod.cs:390` — `!!!TODO!!!` Batch failure lock
- `FetchEodTask.cs:272` — `!!!THINK!!!` OutOfCredits state
- `FetchEodTask.cs:379` — `!!!LATER!!!` Symbol conversion with `~`
- `FetchEodTask.cs:414` — `!!!LATER!!!` Convert response back to real symbol
- `FetchEod.cs:634-656` — `#if false` disabled ImportXml code

---

## Non-Bug Observations (for awareness, not action)

1. **Unbounded uptime blacklist** (`FetchEodPending._uptimeBlockRetrySRefs`) — grows forever during app lifetime. Not a real problem since WASM apps have session-length lifetimes.

2. **LSE penny conversion hardcoded** (`FetchEodTask.cs:357,416`) — assumes all providers return LSE prices in pennies. Fragile but works for current providers.

3. **No batch fetching** — hardcoded `maxRet=1` at `FetchEod.cs:352`. All providers fetched one symbol at a time. Marked as TODO.

4. **FetchRates thread leak potential** (`FetchRates.cs:76`) — `_workerThread` overwritten without checking previous task. Low risk since rate fetches are rare and fast.

---

## Test Coverage Proposal

### Approach: New test project or extend existing?

**Recommendation: Extend `PFS/PfsData.Tests/`** — rename it to `PFS.Tests` would be ideal but for now the existing project can reference PfsExtFetch. The alternative is a new `PfsExtFetch.Tests` project, but the existing test infrastructure (fixtures, asserts, culture setup) is reusable.

### Testability Assessment

| Class | Testability | Mocking needed |
|-------|-------------|----------------|
| `FetchEodPending` | **High** — pure logic, single dependency (`IPfsFetchConfig`) | Mock `IPfsFetchConfig` |
| `FetchEodTask` | **Medium** — state machine with async, needs mock provider | Mock `IExtDataProvider`, `IExtProvider`, `IPfsStatus` |
| `FetchEod` | **Low** — heavy DI, event-driven, async orchestration | Many mocks, complex setup |
| `FetchRates` | **Medium** — simple but async | Mock `IPfsFetchConfig`, `IPfsProvConfig`, `IPfsStatus` |

### Priority 1: FetchEodPending tests (highest value, easiest)

Pure queue logic with one dependency. ~15-20 tests:

1. **AddToPending routing:**
   - Symbol with dedicated provider → goes to priority queue
   - Symbol without dedicated rule → goes to general/market queue
   - Enforced provider → goes to priority queue
   - Comma-separated symbols split correctly

2. **GetPending priority logic:**
   - Priority items served first
   - Blocked SRefs (uptime blacklist) skipped
   - Orphan priority items moved to general queue when all blocked
   - Market filter respected
   - `maxRet` batch size limit honoured

3. **FetchFailedBy blacklisting:**
   - Failed provider+symbol combo blocked on retry
   - Different provider for same symbol still allowed

4. **SetRestToFailedSRefs:**
   - Remaining pending items recorded as cant-find-provider
   - Both priority and general queues drained

5. **Edge cases:**
   - Empty queue returns `(Unknown, null)`
   - `_noJobsLeft` optimization prevents redundant scanning
   - `ClearAllPendings` resets everything

### Priority 2: FetchEodTask state machine tests (~15 tests)

Needs mock `IExtDataProvider` + `IExtProvider` + `IPfsStatus`:

1. **State transitions:**
   - Disabled (no key) → Free (key set) → Fetching → Ready/Error → Free
   - Key cleared while fetching → still finishes

2. **Credit management:**
   - Daily credit reset on day change
   - Monthly credit → daily allocation via workday division
   - Credits deducted on both success and failure
   - Zero credits → `GetMarkets()` returns empty

3. **Date validation:**
   - Older-than-expected date → Error
   - Future date → corrected to expected date
   - Matching date → Success

4. **LSE conversion:**
   - LSE market → prices divided by 100
   - Non-LSE → prices untouched

5. **Error threshold:**
   - After 3 errors for a market → market excluded from `GetMarkets()`

### Priority 3: FetchEod orchestration tests (~10 tests)

Complex but high-value. Needs full mock setup:

1. **Fetch lifecycle:**
   - Symbols enqueued → assigned to free providers → results collected → event fired

2. **Provider selection:**
   - Free provider with credits and market support gets work
   - Disabled/busy/out-of-credits providers skipped

3. **Error handling:**
   - Failed symbol blacklisted and retried with different provider
   - All providers exhausted → `SetRestToFailedSRefs`

4. **FindBySymbolAsync** (after BUG-8 fix):
   - Unknown market → searches all actives
   - Specific market → searches that market only

### Mock infrastructure needed

```
IPfsFetchConfig (mock) — controls dedicated/market rules
IPfsProvConfig (mock) — controls API keys
IPfsStatus (mock) — captures events, provides AppCfg values
IExtDataProvider (mock) — returns configurable FullEOD results
IExtProvider (mock) — controls key, error, credit price
IMarketMeta (mock) — returns market metadata
IPfsPlatform (mock) — localStorage stub
```

### Test project changes

Add to `PfsData.Tests.csproj`:
```xml
<ProjectReference Include="..\PfsExtFetch\PfsExtFetch.csproj" />
<ProjectReference Include="..\PfsConfig\PfsConfig.csproj" />
```

Note: `FetchEodPending` and `FetchEodTask` are `internal` classes. Testing options:
- Add `[InternalsVisibleTo("PfsData.Tests")]` to `PfsExtFetch.csproj`
- Or test through `FetchEod` public API only (less granular)

**Recommendation:** Add `InternalsVisibleTo` — these are the most testable classes.

---

## Verification

After implementing bugs + tests:
- `dotnet build PortfolioStalker.sln` — compiles clean
- `dotnet test` — all existing 142 tests pass + new fetch tests pass
- Manual: FindBySymbolAsync search works correctly with/without market specified
