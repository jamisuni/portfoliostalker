# Claude Jobs — Code Audit Findings

Bug tracker for issues found during code audit (started 2026-02-14).

## In Progress

_(none)_

## Open

### BUG-9: MonthsRemainingWorkDays — Double-counts current day
- **File:** `PFS/PfsExtFetch/FetchEodTask.cs:222-233`
- **Severity:** Low — off-by-one in monthly credit allocation

### BUG-10: ConfigureAwait no-op misuse
- **Files:** `FetchRates.cs:71`, `FetchEodTask.cs:264`
- **Severity:** Cosmetic — `ConfigureAwait(false)` on unawaited Task does nothing

### BUG-11: FetchEod.Fetch — Silent rejection when pending > 0
- **File:** `PFS/PfsExtFetch/FetchEod.cs:170-171`
- **Severity:** Medium — new fetch request silently dropped if previous still pending



## Postponed

### TASK-3: PfsExtFetch test coverage
- **Plan:** See [plan_pfsextfetch.md](plan_pfsextfetch.md) for full audit results and test proposal
- **Scope:** ~40-45 new tests across FetchEodPending, FetchEodTask, FetchEod
- **Status:** Plan complete, not urgent — revisit when fetch logic changes
- **Postponed:** 2026-02-15

## False Positives

### ~~BUG-2: StalkerDoCmd.HoldingEdit() — Overwrites OriginalUnits~~
- Guard clause on line 788-790 rejects edit if any trades exist, so OriginalUnits always equals Units at that point.

## Fixed

_(Items cleared after 30 days)_

### BUG-14: Division by HcInvested with wrong guard — Fixed 2026-02-15
- **Files:** `RepGenInvested.cs`, `RepGenExpHoldings.cs`
- **Fix:** Added `HcInvested != 0` guard to all 3 division sites (per-stock HcGainP, header HcGrowthP, header HcTotalGainP). Prevents DivideByZeroException on zero-cost holdings.

### BUG-15: OverviewGroups duplicate SRefs — Fixed 2026-02-15
- **File:** `PFS/PfsReports/OverviewGroups.cs`
- **Fix:** Removed redundant `pfGroup.SRefs.Add()` on line 93 — SRefs were already initialized from `portfolio.SRefs.Distinct()` on line 85.

### TASK-5: Complete Report Test Suite — Calculation Verification — Fixed 2026-02-15
- **Scope:** 31 new tests across 6 files (4 new + 2 extended), covering Weight, Dividend, PfStocks, Overview, PreCalc, BUG-14/15 repros, multi-currency, edge cases
- **Result:** 213 total tests, all passing (up from 182)
- **New files:** `WeightReportTests.cs` (6), `DividentReportTests.cs` (5), `OverviewReportTests.cs` (5), `PreCalcTests.cs` (6)
- **Extended:** `ReportCalcTests.cs` (+9), stub additions in `ReportStubs.cs` and `ReportTestFixture.cs`
- **Infrastructure:** Added `FilterByPfReportFilters`, `FilterBySectorReportFilters`, `FilterByOwningReportFilters`, `StubExtraColumns`, `StubFetchConfig`

### BUG-12: RepGenStMgHistory — stockMeta used before null check — Fixed 2026-02-15
- **File:** `PFS/PfsReports/RepGenStMgHistory.cs`
- **Fix:** Moved null check for `stockMeta` before its first usage (`stockMeta.marketCurrency`)

### BUG-13: RepGenStMgHoldings — No null check on stockMeta at all — Fixed 2026-02-15
- **File:** `PFS/PfsReports/RepGenStMgHoldings.cs`
- **Fix:** Added null check for `stockMeta` before its first usage, returning `FailResult` if null

### TASK-4: PfsReports Full Audit & Test Planning — Fixed 2026-02-15
- **Scope:** Audit all 14 report generators, document bugs, create test plan
- **Result:** Created [plan_pfsreports.md](plan_pfsreports.md) — full audit with 4 bugs (BUG-12–BUG-15), ~35 proposed tests, 10 stub classes needed. Updated [architecture.md](architecture.md) with expanded report pipeline docs.
- **Bugs found:** BUG-12 (HIGH), BUG-13 (HIGH), BUG-14 (LOW), BUG-15 (LOW)
- **Additional findings:** 9+ double-enumeration performance issues, return type inconsistency, 7 code markers, sort-inside-loop in RepGenDivident

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

### BUG-8: FindBySymbolAsync — Inverted market logic — Fixed 2026-02-15
- **File:** `PFS/PfsExtFetch/FetchEod.cs:240-243`
- **Fix:** Swapped the if/else branches — Unknown market now searches all actives, specified market searches only that market

### TASK-1: Unit Test Project for Stalker Library — Fixed 2026-02-14
- **Project:** `PFS/PfsData.Tests/PfsData.Tests.csproj` (xUnit, net10.0)
- **Result:** 112 tests, all passing. 13 test classes covering Stalker domain model
- **Note:** Added `TestCultureSetup.cs` module initializer to set InvariantCulture — Finnish locale caused 39/40 failures because `StalkerParam.cs` calls `decimal.TryParse()` without `CultureInfo.InvariantCulture` (unlike `DecimalExtensions.Parse()` which does it correctly)
