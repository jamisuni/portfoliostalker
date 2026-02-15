# PfsReports — Full Audit & Test Plan

Audit of the PfsReports layer — report generation pipeline sitting on top of the verified Stalker domain model. Current test coverage: **zero**. This document covers code analysis, bugs, and future test strategy.

Audit date: 2026-02-15.

---

## 1. Report Generator Inventory

### 1a. PreCalc-Based Reports (use `IReportPreCalc` + `IReportFilters`)

These reports receive pre-filtered, pre-aggregated `RCStock` data from `ReportPreCalc`.

| # | Class | Return Type | Dependencies | Description |
|---|-------|-------------|--------------|-------------|
| 1 | `RepGenInvested` | `(header, List<RepDataInvested>)` or `(null, null)` | IReportFilters, IReportPreCalc, IStockMeta, StalkerData, IStockNotes | Investment summary — holdings with growth, gain, dividends. Header totals. Sub-holdings per stock. |
| 2 | `RepGenWeight` | `(header, List<RepDataWeight>)` or `(null, null)` | IReportFilters, IReportPreCalc, IStockMeta, StalkerData, IStockNotes, IPfsStatus | Portfolio weight vs target allocation. Uses "Weight" sector for targets. Trade history. Taken-vs-valuation ratio. |
| 3 | `RepGenPfStocks` | `List<RepDataPfStocks>` (never null, can be empty) | IReportFilters, IReportPreCalc, StalkerData, IStockNotes | Portfolio stock listing. Includes stocks without EOD (shows error message). Alarms, orders. |
| 4 | `RepGenPfSales` | `List<RepDataPfSales>` or `null` | IReportFilters, IReportPreCalc, IStockMeta, StalkerData, IStockNotes | Closed positions grouped by TradeId. Per-trade growth and dividends. |
| 5 | `RepGenDivident` | `Result<RepDataDivident>` | IReportFilters, IReportPreCalc, StalkerData, IStockMeta | Dividend analysis — monthly totals, last 13 months detail. Yearly div% projection. |
| 6 | `RepGenExpHoldings` | `List<RepDataExpHoldings>` or `null` | DateOnly, IReportFilters, IReportPreCalc, IStockMeta, StalkerData | Export: current holdings with gain, sector, average holding time. |
| 7 | `RepGenExpSales` | `List<RepDataExpSales>` or `null` | IReportFilters, IReportPreCalc, IStockMeta, StalkerData | Export: all sales (tax report focus). No dividends. |
| 8 | `RepGenExpDividents` | `List<RepDataExpDividents>` or `null` | IReportFilters, IReportPreCalc, IStockMeta, StalkerData | Export: all dividends per payment date. |
| 9 | `ReportOverviewGroups` | `Result<List<OverviewGroupsData>>` | IReportFilters, IReportPreCalc, IPfsStatus, StalkerData, ILatestRates | Group-level summary: Alarms, Investments, Top Valuations, Oldies, Tracking, per-Portfolio. |
| 10 | `ReportOverviewStocks` | `List<OverviewStocksData>` (never null, can be empty) | IReportFilters, IReportPreCalc, IPfsStatus, StalkerData, IStockMeta, IMarketMeta, IExtraColumns, IStockNotes | Stock-level overview with alarms, orders, extra columns. Filters out CLOSED/DISABLED markets. |

### 1b. Standalone Reports (direct StalkerData access, no PreCalc)

| # | Class | Return Type | Dependencies | Description |
|---|-------|-------------|--------------|-------------|
| 11 | `RepGenStMgHistory` | `Result<List<RepDataStMgHistory>>` | string sRef, StalkerData, IStockMeta, IEodLatest, ILatestRates, StoreStockMetaHist | Single-stock transaction history: Own/Buy/Sold entries with dividends. StockMetaHist events. |
| 12 | `RepGenStMgHoldings` | `Result<List<RepDataStMgHoldings>>` | string sRef, StalkerData, IStockMeta, IEodLatest, ILatestRates | Single-stock current holdings with growth and dividends per holding. |
| 13 | `RepGenTracking` | `List<RepDataTracking>` | StalkerData, IMarketMeta, IStockMeta, IEodLatest, IPfsFetchConfig, IStockNotes | All tracked stocks metadata. PF ownership tracking. Fetch provider info. |

### 1c. Pre-Calculator

| # | Class | Interface | Description |
|---|-------|-----------|-------------|
| 14 | `ReportPreCalc` | `IReportPreCalc` | Constructor aggregates StalkerData → `List<RCStock>`. `GetStocks()` applies sector/market/owning filters via `yield return`. Used by all PreCalc-based reports. |

---

## 2. Confirmed Bugs

### BUG-12: RepGenStMgHistory — stockMeta used before null check (HIGH)

**File:** `PFS/PfsReports/RepGenStMgHistory.cs:32-36`

```csharp
StockMeta stockMeta = stockMetaProv.Get(sRef);           // line 32
FullEOD fullEod = latestEodProv.GetFullEOD(sRef);        // line 33
decimal latestConversionRate = latestRatesProv.GetLatest(stockMeta.marketCurrency); // line 34 — CRASH if null

if (stockMeta == null)                                    // line 36 — too late!
    return new FailResult<...>(...);
```

**Impact:** NullReferenceException when `stockMetaProv.Get()` returns null for unknown SRef.
**Fix:** Move null check to immediately after line 32, before accessing `stockMeta.marketCurrency`.

### BUG-13: RepGenStMgHoldings — No null check on stockMeta at all (HIGH)

**File:** `PFS/PfsReports/RepGenStMgHoldings.cs:30-32`

```csharp
StockMeta stockMeta = stockMetaProv.Get(sRef);           // line 30
FullEOD fullEod = latestEodProv.GetFullEOD(sRef);        // line 31
decimal latestConversionRate = latestRatesProv.GetLatest(stockMeta.marketCurrency); // line 32 — CRASH if null
```

**Impact:** NullReferenceException — same pattern as BUG-12 but with zero null checking anywhere.
**Fix:** Add null check after line 30, return FailResult if null.

### BUG-14: Division by HcInvested with wrong guard (LOW)

**Files:** `RepGenInvested.cs:69-70`, `RepGenInvested.cs:102`, `RepGenExpHoldings.cs:65-66`

```csharp
// RepGenInvested.cs:69-70
if (entry.HcGain != 0)                                    // Guards HcGain...
    entry.HcGainP = (int)(entry.HcGain / entry.RCTotalHold.HcInvested * 100);  // ...but divides by HcInvested

// RepGenInvested.cs:102 — NO guard at all
header.HcGrowthP = (int)((header.HcTotalValuation - header.HcTotalInvested) / header.HcTotalInvested * 100);

// RepGenExpHoldings.cs:65-66 — same wrong guard
if (entry.HcGain != 0)
    entry.HcGainP = (int)(entry.HcGain / entry.RCTotalHold.HcInvested * 100);
```

**Impact:** DivideByZeroException if HcInvested is 0 but HcGain is non-zero (possible with dividend-only gains). Header line 102 can divide by zero if all holdings have zero invested.
**Fix:** Change guards to `HcInvested != 0` (or `> 0`). Add guard to header line 102.

### BUG-15: OverviewGroups duplicate SRefs (LOW)

**File:** `PFS/PfsReports/OverviewGroups.cs:85,93`

```csharp
SRefs = portfolio.SRefs.Distinct().ToList(),              // line 85 — copies all portfolio SRefs
...
foreach (RCStock stock in reportStocks)
{
    if (portfolio.SRefs.Contains(stock.StockMeta.GetSRef()) == false)
        continue;

    pfGroup.SRefs.Add(stock.StockMeta.GetSRef());         // line 93 — adds AGAIN for matching stocks
```

**Impact:** Duplicate SRefs in portfolio group data. The comment on line 86 acknowledges this: "creates duplicate list but not items / as below has adds so that doesnt mess stalker" — but line 93 adds duplicates to the already-copied list.
**Fix:** Either initialize SRefs empty and build only from the filtered loop, or remove line 93.

---

## 3. Performance: Double Enumeration of GetStocks()

`ReportPreCalc.GetStocks()` is a `yield return` method. Multiple reports call it twice — once for `.Count()` and once for `foreach` — causing the entire filter pipeline to run twice.

| File | Lines | Pattern |
|------|-------|---------|
| `RepGenInvested.cs` | 33+38 | `.Count()` then `foreach GetStocks()` |
| `RepGenWeight.cs` | 34+39 | `.Count()` then `foreach GetStocks()` |
| `RepGenPfSales.cs` | 30+37 | `.Count()` then `foreach reportStocks` (same IEnumerable) |
| `RepGenExpHoldings.cs` | 30+35 | `.Count()` then `foreach reportStocks` |
| `RepGenExpSales.cs` | 38+43 | `.Count()` then `foreach reportStocks` |
| `RepGenExpDividents.cs` | 30+35 | `.Count()` then `foreach reportStocks` |
| `RepGenDivident.cs` | 27+34 | `.Count()` then `foreach reportStocks` |
| `ReportOverviewStocks.cs` | 30+41 | `.Count()` then `foreach GetStocks()` (calls GetStocks twice!) |
| `ReportOverviewGroups.cs` | 29 | Single `.Where()` but enumerated multiple times (line 29, 59, 73) |

**Fix:** Add `.ToList()` to first call, reuse materialized list. Or remove the Count check and let the loop handle empty gracefully.

---

## 4. Return Type Inconsistencies

Reports return "no data" in three different ways:

| Pattern | Reports |
|---------|---------|
| Returns `null` | RepGenInvested (tuple of nulls), RepGenWeight (tuple of nulls), RepGenPfSales, RepGenExpHoldings, RepGenExpSales, RepGenExpDividents |
| Returns empty collection | ReportOverviewStocks (`new()`), RepGenPfStocks (implicit empty list) |
| Returns `Result<FailResult>` | RepGenDivident, RepGenStMgHistory, RepGenStMgHoldings, ReportOverviewGroups |

This inconsistency means callers must handle null checks differently for each report. Not a bug but a maintenance concern.

---

## 5. Code Markers

| File | Line | Marker | Content |
|------|------|--------|---------|
| `RepGenExpSales.cs` | 30 | `!!!THINK!!!` | Add currency conversion rate column for tax report |
| `RCDivident.cs` | 38 | `!!!TODO!!!` | RCDivident seems too targeted to one use case — rename? move as sub of RCStock? |
| `RCDivident.cs` | 53 | `!!!TODO!!!` | Missing HoldingUnits so can't do general use |
| `RCDivident.cs` | 57 | `!!!TODO!!!` | Move RCTotalHcDivident under RCStock and rename RCStockTotalHcDiv |
| `ReportFilters.cs` | 80 | `!!!CODE!!!` | Alternative way to create fixed dictionary (commented out) |
| `ReportFilters.cs` | 88 | `!!!CODE!!!` | Static readonly ImmutableDictionary pattern |
| `ReportFilters.cs` | 156 | `!!!CODE!!!` | Switch variant for variable set |

---

## 6. Additional Observations

### RepGenInvested.cs:79 — Potential NullReferenceException
```csharp
hcTotalDiv += stock.RCHoldingsTotalDivident.HcDiv;  // line 79
```
This is inside a loop that skips stocks without holdings (line 45-46) and without EOD (line 48-49). After `RecalculateTotals()`, `RCHoldingsTotalDivident` is only non-null when both EOD and holdings exist. So the guard is implicit via the continue checks above. However, if a stock has holdings but `RecalculateTotals` was not called (or failed), this would crash. **Not a bug in current flow** but fragile.

### RepGenWeight.cs:111 — Same fragile pattern
```csharp
entry.RRTotalDivident.HcDiv  // line 111 — accessed unconditionally
```
`entry.RRTotalDivident` is only set on line 61 when `stock.RCHoldingsTotalDivident != null`. If it's null, line 111 crashes. However, the null check on line 60 means it's only set conditionally, but line 111 accesses it always. **This looks like a bug** — if a stock has holdings but no dividends, `RRTotalDivident` is null and line 111 will throw NullReferenceException.

### ReportOverviewGroups.cs — reportStocks enumerated multiple times
Line 29 creates `reportStocks` as `IEnumerable` (with `.Where()`), then enumerates it on line 59 (foreach), line 73 (Where+OrderBy+Take). Each enumeration re-runs GetStocks + Where filter.

### RepGenDivident.cs:66 — LastPayments re-sorted inside loop
```csharp
ret.LastPayments = ret.LastPayments.OrderByDescending(d => d.PaymentDate).ToList();  // line 66
```
This sort happens inside the outer `foreach (RCStock stock in reportStocks)` loop, re-sorting after every stock. Should be moved outside the loop.

---

## 7. Test Strategy

### New test project: `PFS/PfsReports.Tests/`

Separate from `PfsData.Tests` due to different mock infrastructure needs.

**References:** PfsReports, PfsData, PfsTypes, PfsConfig

### Mock/Stub Infrastructure (10 classes)

| Stub | Interface | Behavior |
|------|-----------|----------|
| `StubEodLatest` | `IEodLatest` | Returns `FullEOD` with configurable prices per SRef. `GetFullEOD(MarketId, symbol)` and `GetFullEOD(sRef)` variants. |
| `StubLatestRates` | `ILatestRates` | Returns fixed conversion rates per `CurrencyId`. |
| `StubStockMeta` | `IStockMeta` | Returns `StockMeta` per SRef. Supports `AddUnknown()`. |
| `StubMarketMeta` | `IMarketMeta` | Returns active markets list, `LastClosing` dates. |
| `StubPfsPlatform` | `IPfsPlatform` | Returns fixed UTC time. |
| `StubPfsStatus` | `IPfsStatus` | Returns configurable `AppCfg` values (OverviewStockAmount, ExtraColumns, IOwn). |
| `StubStockNotes` | `IStockNotes` | Returns empty/null headers. |
| `StubExtraColumns` | `IExtraColumns` | Returns null/empty. |
| `StubReportFilters` | `IReportFilters` | Allow-all default. Configurable per filter type. |
| `StubFetchConfig` | `IPfsFetchConfig` | For Tracking report — returns empty provider arrays. |

### Test Fixture: `ReportTestFixture`

Wraps `StalkerTestFixture.CreatePopulated()` (from PfsData.Tests) with matching stub data:

- **EOD prices** for fixture stocks: MSFT ($420), AAPL ($195), NVDA ($880), KO ($62), ENB (CAD $52), SAP (EUR $195), RY (CAD $155)
- **Conversion rates:** USD=1.0, CAD=0.74, EUR=1.08 (to EUR home currency)
- **PrevClose** prices: -1% from Close for all stocks
- **Markets:** NASDAQ, NYSE, TSX, OMXH active; no CLOSED markets

### Test Data Approach

- **Primary:** Fixture-based — `StalkerTestFixture` + stubs. Fast, deterministic, covers most scenarios.
- **Secondary:** XML import from `/home/jami/pfs/export/stalker.txt` for integration-style tests with real-world data patterns.

---

## 8. Proposed Tests (~35 total)

### Priority 1 — Bug Reproduction (5 tests)

| Test | Bug | What it verifies |
|------|-----|------------------|
| `StMgHistory_NullStockMeta_ReturnsFailResult` | BUG-12 | No NRE when stockMeta is null |
| `StMgHoldings_NullStockMeta_ReturnsFailResult` | BUG-13 | No NRE when stockMeta is null |
| `Invested_ZeroInvested_NoDivideByZero` | BUG-14 | No DivByZero when HcInvested is 0 |
| `ExpHoldings_ZeroInvested_NoDivideByZero` | BUG-14 | Same for export holdings |
| `OverviewGroups_PfSRefs_NoDuplicates` | BUG-15 | No duplicate SRefs in portfolio groups |

### Priority 2 — Core Happy-Path (10 tests)

| Test | Description |
|------|-------------|
| `Invested_WithHoldings_ReturnsReport` | Fixture stocks produce header + entries with correct totals |
| `Weight_WithHoldings_ReturnsReport` | Weight% calculated, target from Weight sector |
| `PfStocks_AllStocks_IncludesNoEod` | Stocks without EOD appear with FailedMsg |
| `PfSales_WithTrades_GroupedByTradeId` | Trades grouped correctly, growth calculated |
| `Divident_WithDividents_MonthlyTotals` | Monthly totals and last-13-months detail |
| `StMgHistory_SingleStock_OwnBuySold` | Own, Buy, Sold entries created correctly |
| `StMgHoldings_SingleStock_AllHoldings` | All holdings for sRef across portfolios |
| `OverviewGroups_DefaultGroups_Created` | 5 default groups + per-portfolio groups |
| `OverviewStocks_ActiveMarkets_Only` | CLOSED market stocks excluded |
| `Empty_Data_ReturnsCorrectNullOrEmpty` | Each report returns expected "no data" value |

### Priority 3 — Calculation Verification (10 tests)

| Test | Description |
|------|-------------|
| `Invested_MultiCurrency_ConversionCorrect` | USD + CAD + EUR stocks convert to HC properly |
| `Invested_HeaderTotals_MatchStockSum` | Header HcTotalInvested/Valuation equals sum of entries |
| `Weight_CurrentP_SumsTo100` | Weight percentages sum correctly |
| `Weight_TargetFromSector_Parsed` | Target% parsed from Weight sector field |
| `Divident_YearlyDivP_Projection` | DivPaidTimesPerYear * latest div / HcAvrgPrice |
| `StMgHistory_AvrgHoldingTime_Correct` | Holding time calculation |
| `PfSales_TotalGrowth_AcrossHoldings` | Multi-holding trade sums correctly |
| `ExpHoldings_AvrgTimeAsMonths_Correct` | Weighted average holding time calculation |
| `OverviewGroups_HcTotalValuation_Correct` | Group valuations sum holdings correctly |
| `Invested_HcGainP_IncludesDividends` | Gain% includes both growth and dividends |

### Priority 4 — ReportPreCalc (8 tests)

| Test | Description |
|------|-------------|
| `PreCalc_AllStocks_Aggregated` | All portfolio stocks appear in _retStocks |
| `PreCalc_FilterByPf_LimitsStocks` | PF filter excludes non-matching portfolios |
| `PreCalc_FilterBySector_Excludes` | Sector filter applied in GetStocks |
| `PreCalc_FilterByMarket_Excludes` | Market filter applied |
| `PreCalc_FilterByOwning_Holding` | Owning=Holding shows only stocks with holdings |
| `PreCalc_LimitSinglePf_OverridesFilter` | LimitSinglePf ignores PF filter selection |
| `PreCalc_RecalculateTotals_Correct` | RCTotalHold and RCHoldingsTotalDivident set |
| `PreCalc_Orders_ClosestTrigger_Kept` | Only closest-to-trigger order per PF kept |

### Priority 5 — Edge Cases & Performance (2 tests)

| Test | Description |
|------|-------------|
| `Tracking_AllStockMeta_InReport` | Every StockMeta entry appears in tracking report |
| `Divident_SortOutsideLoop` | (After fix) Verify sort happens once, not per-stock |

---

## 9. Dependencies for Test Implementation

1. **`InternalsVisibleTo`** — May need in `PfsReports.csproj` if `ReportPreCalc._retStocks` or other internals need direct access.
2. **`StalkerTestFixture`** — Either share from PfsData.Tests (make it a shared project/package) or duplicate the fixture helper.
3. **`StoreStockMetaHist`** — Needed for `RepGenStMgHistory`. Concrete class, not interface-based. May need to instantiate directly with test data.
4. **`ExpiredStocks`** — Used by `ReportPreCalc` constructor. Static helper, needs `IStockMeta`, `IEodLatest`, `IMarketMeta`. Works with stubs.

---

## 10. Summary

| Metric | Count |
|--------|-------|
| Report generators audited | 14 (13 generators + 1 pre-calculator) |
| Confirmed bugs | 4 (BUG-12 through BUG-15) |
| HIGH severity | 2 (BUG-12, BUG-13) |
| LOW severity | 2 (BUG-14, BUG-15) |
| Code markers found | 7 (3 !!!TODO!!!, 3 !!!CODE!!!, 1 !!!THINK!!!) |
| Performance issues | 9+ reports with double enumeration |
| Additional observations | 3 (fragile null patterns, sort-inside-loop) |
| Proposed tests | ~35 |
| Stub classes needed | 10 |
