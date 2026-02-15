# Claude Jobs — Code Audit Findings

Bug tracker for issues found during code audit (started 2026-02-14).

## In Progress

_(none)_

## Open

### BUG-8: FindBySymbolAsync — Inverted market logic
- **File:** `PFS/PfsExtFetch/FetchEod.cs:240-243`
- **Severity:** High — search returns wrong results
- **Description:** if/else branches swapped: Unknown market searches only Unknown, specified market searches all actives. Should be opposite.

### BUG-9: MonthsRemainingWorkDays — Double-counts current day
- **File:** `PFS/PfsExtFetch/FetchEodTask.cs:222-233`
- **Severity:** Low — off-by-one in monthly credit allocation

### BUG-10: ConfigureAwait no-op misuse
- **Files:** `FetchRates.cs:71`, `FetchEodTask.cs:264`
- **Severity:** Cosmetic — `ConfigureAwait(false)` on unawaited Task does nothing

### BUG-11: FetchEod.Fetch — Silent rejection when pending > 0
- **File:** `PFS/PfsExtFetch/FetchEod.cs:170-171`
- **Severity:** Medium — new fetch request silently dropped if previous still pending

### TASK-3: PfsExtFetch test coverage
- **Plan:** See [plan_pfsextfetch.md](plan_pfsextfetch.md) for full audit results and test proposal
- **Scope:** ~40-45 new tests across FetchEodPending, FetchEodTask, FetchEod
- **Status:** Plan complete, awaiting implementation decision

## False Positives

### ~~BUG-2: StalkerDoCmd.HoldingEdit() — Overwrites OriginalUnits~~
- Guard clause on line 788-790 rejects edit if any trades exist, so OriginalUnits always equals Units at that point.

## Fixed

_(Items cleared after 30 days)_

### BUG-1: StalkerDoCmd.StockSet() — Off-by-one on FindIndex — Fixed 2026-02-14
- **File:** `PFS/PfsData/Stalker/StalkerDoCmd.cs` line ~489
- **Fix:** Changed `if (index > 0)` to `if (index >= 0)`

### BUG-3: SAlarmTrailingBuyP.IsAlarmTriggered() — Wrong divisor in formula — Fixed 2026-02-14
- **File:** `PFS/PfsTypes/Stalker/SAlarm.cs` line ~254
- **Fix:** Changed `/ eod.Close` to `/ Low` to match TrailingSellP symmetry

### BUG-4: StalkerData.DeepCopy() — Missing _sectors copy — Fixed 2026-02-14
- **File:** `PFS/PfsData/Stalker/StalkerData.cs` line ~42-52
- **Fix:** Added sector array initialization and DeepCopy loop for all non-null sectors

### BUG-5: SSector.DeepCopy() — Doesn't copy FieldNames array — Fixed 2026-02-14
- **File:** `PFS/PfsTypes/Stalker/SSector.cs`
- **Fix:** Added `ret.FieldNames = (string[])FieldNames.Clone()` replacing `!!!TODO!!!`

### BUG-6: StalkerXML.ImportXml() — Silent data loss on sector mismatch — Fixed 2026-02-14
- **File:** `PFS/PfsData/Stalker/StalkerXML.cs`
- **Fix:** Added warning messages when sector or field name not found during import

### BUG-7: StalkerAddOn_Transactions.Validate() — Resurrected validation — Fixed 2026-02-15
- **Files:** `Transaction.cs`, `StalkerAddOn_Transactions.cs`, `ImportTransactions.razor.cs`
- **Fix:** Implemented `Transaction.IsValid()` with per-action field validation. Rewrote `Validate()` to return `(ErrMsg, IsDuplicate)` tuple (removed `#if false`). Unwrapped caller in UI to wire results to `BtAction.TAStatus`.

### TASK-2: Expand Stalker Unit Test Coverage — Fixed 2026-02-15
- **Result:** 142 tests, all passing (up from 112)
- **Changes:**
  1. Hardened ~70 setup calls with `StalkerAssert.Ok()` across 7 test files
  2. Enriched `CreatePopulated()` with 1 trade, 4 dividends, 2nd sector (Geography)
  3. Added 6 XML round-trip tests: orders, trades, dividends, sector assignments, currency rates
  4. Created `RealDataPatternTests.cs` — 14 tests: special symbols, Delete-Divident, multi-partial-sales, 3 sectors, zero fees
  5. Added 12 trailing alarm tests: TrailingSellP/TrailingBuyP trigger logic, prms round-trip, DoAction integration

### TASK-2b: InvariantCulture fixes in source code — Fixed 2026-02-14
- **Files fixed:** StalkerParam.cs, Decimal.cs, SAlarm.cs, RepGenWeight.cs — all now use InvariantCulture for decimal parsing/formatting

### TASK-1: Unit Test Project for Stalker Library — Fixed 2026-02-14
- **Project:** `PFS/PfsData.Tests/PfsData.Tests.csproj` (xUnit, net10.0)
- **Result:** 112 tests, all passing. 13 test classes covering Stalker domain model
- **Note:** Added `TestCultureSetup.cs` module initializer to set InvariantCulture — Finnish locale caused 39/40 failures because `StalkerParam.cs` calls `decimal.TryParse()` without `CultureInfo.InvariantCulture` (unlike `DecimalExtensions.Parse()` which does it correctly)
