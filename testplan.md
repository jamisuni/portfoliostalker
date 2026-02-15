# Test Plan

Testing strategy and coverage for Portfolio Stalker. See [architecture.md](architecture.md) for system overview.

## Test Infrastructure

- **Framework**: xUnit 2.x with Microsoft.NET.Test.Sdk
- **Target**: net10.0
- **Projects**: `PFS/PfsData.Tests/PfsData.Tests.csproj` (domain), `PFS/PfsReports.Tests/PfsReports.Tests.csproj` (reports)
- **CI**: `dotnet test --verbosity diagnostic` runs on every push to `main` (`.github/workflows/main.yml`)

## Test Helpers

| File | Purpose |
|------|---------|
| `PfsData.Tests/Helpers/StalkerTestFixture.cs` | `CreateEmpty()` — fresh StalkerDoCmd; `CreatePopulated()` — pre-loaded with 3 portfolios, 7 stocks, 8 holdings, 1 trade, 4 dividends, 2 orders, 2 alarms, 2 sectors |
| `PfsData.Tests/Helpers/StalkerAssert.cs` | `Ok(Result)` — asserts success with error message display; `Fail(Result, substring)` — asserts failure with optional message check |
| `PfsReports.Tests/Helpers/ReportTestFixture.cs` | Creates full stub environment (EOD, rates, markets, stock meta) matching `CreatePopulated()` fixture data |
| `PfsReports.Tests/Helpers/ReportStubs.cs` | Stub implementations: `StubStockMeta`, `StubEodLatest`, `StubLatestRates`, `StubMarketMeta`, `StubPfsPlatform`, `StubStockNotes`, `StubPfsStatus`, `StubReportFilters`, `StubExtraColumns`, `StubFetchConfig`, plus filter variants |

## Current Test Coverage

### PfsData.Tests — Stalker Domain Model (142 tests across 14 classes)

All tests follow Arrange-Act-Assert pattern using fixture helpers and Result/FailResult assertions.

| Test Class | Tests | Area | Key Scenarios |
|------------|-------|------|---------------|
| `StalkerSplitTests` | 8 | Command tokenizer | Simple words, bracketed values, standalone brackets, empty strings, multiple spaces, nested brackets |
| `StalkerParamTests` | 10 | Parameter parsing | SRef validation (valid/invalid markets), date parsing, decimal range, enum parsing, named parameter validation |
| `StalkerActionTests` | 6 | Command creation | Valid combos (Add-Portfolio, Add-Holding), invalid/unsupported combos, SetParam, IsReady validation |
| `PortfolioTests` | 10 | Portfolio CRUD | Add/rename/delete, case-insensitive duplicate check, prevent deletion with holdings, Top reordering, Follow/Unfollow SRef tracking |
| `StockTests` | 10 | Stock lifecycle | Delete (clean/blocked by holdings/orders/tracking), rename (propagates to holdings+trades), split (factor doubles units/halves price), close (moves holdings to trades), DeleteAll |
| `HoldingTests` | 10 | Holdings CRUD | Add/edit/note/delete, prevent edit after trades, fee calculation, delete prevention (with trades/dividends), rounding units |
| `TradeTests` | 11 | Sales & FIFO | Full/partial sales, FIFO (oldest first), multi-holding FIFO, overselling prevention, duplicate TradeId check, delete restores units, note updates, newer trade blocks older deletion |
| `OrderTests` | 7 | Orders CRUD | Add Buy/Sell, duplicate price prevention, edit, delete, reset fill date |
| `AlarmTests` | 22 | Alarm management | Add Under/Over, duplicate prevention, delete, edit, triggering logic (Under/Over), TrailingSellP (create, trigger on drop, high updates, prms round-trip), TrailingBuyP (create, trigger on recovery, low updates, prms round-trip), DoAction integration for trailing alarms |
| `DividentTests` | 8 | Dividend management | Auto-assign to eligible holdings, exclude recent purchases, unit mismatch validation, duplicate ex-div dates, targeted assignment by purchase ID, pre/post ex-div trade handling, delete all |
| `SectorTests` | 8 | Sector classification | Set/update names, edit field names, delete field/sector with stock cleanup, Follow/Unfollow stock assignment, error on uninitialized sector |
| `StalkerDataQueryTests` | 8 | Query & deep copy | Portfolio listing, SRef sorting, holdings filtering by SRef, unknown portfolio handling, PurchaseId lookup, stock lookup, DeepCopy independence, action tracking |
| `StalkerXmlTests` | 12 | XML persistence | Export structure, import without warnings, round-trip: portfolios, holdings, alarms, sectors, orders, trades, dividends, sector stock assignments, currency rates |
| `RealDataPatternTests` | 14 | Real-world patterns | Symbols with dots/dashes, custom PurhaceId formats, multiple dividends per holding, dividend on trade scenarios, multiple partial sales, Delete-Divident (from holding/trade), zero fees, 3 sectors, Close-Stock CLOSED$ format, XML round-trip with special symbols |

### PfsReports.Tests — Report Generation (71 tests across 7 classes)

Tests use `ReportTestFixture` (stub-based infrastructure) and `StalkerTestFixture.CreatePopulated()` for fixture data. Stubs: `StubStockMeta`, `StubEodLatest`, `StubLatestRates`, `StubMarketMeta`, `StubPfsPlatform`, `StubStockNotes`, `StubPfsStatus`, `StubReportFilters`, `StubExtraColumns`, `StubFetchConfig`, plus filter variants (`FilterByPfReportFilters`, `FilterBySectorReportFilters`, `FilterByOwningReportFilters`).

| Test Class | Tests | Area | Key Scenarios |
|------------|-------|------|---------------|
| `RCGrowthTests` | 5 | RCGrowth record | Constructor calculations, McAvrgPrice, HcValuation, growth percentages |
| `RCStockRecalcTests` | 9 | RCStock.RecalculateTotals | Single/multi holding, dividends, no holdings, no EOD, trade-only |
| `ReportCalcTests` | 36 | StMgHoldings, StMgHistory, Invested, PfSales, ExpHoldings, RCEod, RCDivident, RRDivident, SHolding, PfStocks, BUG-14/15 repros, multi-currency, edge cases | Growth calculation, partial sales, null StockMeta, header totals, gain formulas, percentage sums, holding time, dividend aggregation, zero-invested edge case, empty stalker, overview group duplicates, failed messages for missing EOD, alarm/order inclusion, multi-currency conversion, StockMetaHist events |
| `WeightReportTests` | 6 | RepGenWeight | Happy path, CurrentP sums to 100%, TargetP from Weight sector, AvrgTimeAsMonths formula, trade profit accumulation, TakenAgainstValuation formula |
| `DividentReportTests` | 5 | RepGenDivident | Happy path, monthly grouping, 13-month detail cutoff, YearlyDivP projection, no-dividends edge case |
| `OverviewReportTests` | 5 | OverviewGroups, OverviewStocks | Default group creation (5 + per-PF), valuation totals, CLOSED market exclusion, all stocks returned, alarms/orders populated |
| `PreCalcTests` | 6 | ReportPreCalc | All stocks aggregated, PF filter, sector filter, owning filter, RecalculateTotals correctness, closest-trigger order kept |

## Coverage Gaps

Areas without automated tests, listed by priority for future coverage.

### High Priority

**PfsConfig — Configuration Management**
- `MarketConfig`: Market definitions, holiday checks, active/inactive toggling
- `FetchConfig`: Provider-to-market rule matching, detailed vs market-wide rules, priority ordering
- `ProvConfig`: API key storage, key operations (set/del/clear)
- `AppConfig`: Settings persistence

**PfsReports — Report Generation (partially covered)**
- Covered: RepGenWeight, RepGenDivident, RepGenPfStocks, RepGenInvested, RepGenPfSales, RepGenExpHoldings, RepGenStMgHoldings, RepGenStMgHistory, OverviewGroups, OverviewStocks, ReportPreCalc filtering
- Remaining gaps: RepGenTracking, RepGenExpSales, RepGenExpDividents, expired EOD detection edge cases

**PfsHelpers — Utilities**
- `CmdParser`: Template matching, enum parsing, bracket handling

### Medium Priority

**PfsExtFetch — Fetch Orchestration**
- `FetchEod`: Request queuing, result tracking, provider selection
- `FetchEodTask`: State transitions, credit tracking, timeout handling
- `FetchEodPending`: Queue management
- `FetchRates`: Currency rate fetching

**Client — Frontend Services**
- `ClientData`: Startup orchestration, IDataOwner lifecycle
- `ClientStalker`: Persistence coordination
- `FEStalker`: Action forwarding, data access
- `FEReport`: Filter management, report generation integration
- `ClientScheduler`: Timer-based callback scheduling

**Store Classes — Data Persistence**
- `StoreLatestEod`: EOD storage and retrieval, history rolling
- `StoreStockMeta`: Metadata CRUD, market currency mapping
- `StoreNotes`, `StoreUserEvents`, `StoreExtraColumns`, `StoreReportFilters`: Storage lifecycle

### Lower Priority

**PfsExtProviders — External API Adapters**
- Each provider needs: response parsing tests (with sample JSON), error handling, market support validation
- Best tested with recorded HTTP responses (mock HttpClient)
- `ExtCurrencyApi`: Rate parsing and currency mapping

**PfsExtTransactions — Bank CSV Import**
- `BtParser`: Header mapping, field conversion
- `BtNordnet`: Nordnet CSV format parsing, Finnish column headers
- Note: `StalkerAddOn_Transactions.Validate()` is dead code (`#if false`) — needs resurrection or removal

**PfsUI — Blazor Frontend**
- Component rendering tests (bUnit) would be beneficial for complex components
- Priority targets: DlgHoldingsEdit (currency conversion), ImportTransactions (CSV parsing), ReportExp* (export generation)

**PfsCmdLine — Console App**
- Minimal implementation; test when functionality expands

## CI Pipeline

### Main Workflow (`.github/workflows/main.yml`)

Triggers on push to `main`:
1. Checkout code
2. Setup .NET SDK 10.0.100
3. `dotnet test --verbosity diagnostic`
4. `dotnet publish PortfolioStalker.sln -c Release -o release --nologo`
5. Modify base href in index.html for GitHub Pages
6. Copy index.html → 404.html (SPA routing)
7. Add .nojekyll
8. Deploy to GitHub Pages (JamesIves action)

### Other Workflows

- `manual.yml` — Manual version release (creates GitHub release, deploys to versioned folder)
- `manualnewrepo.yml` — Creates new repository per version release

## Testing Patterns

### Conventions

- **Fixture-based setup**: `CreateEmpty()` for isolated tests, `CreatePopulated()` for integration-style tests with realistic data
- **Result pattern**: Operations return `Result`/`FailResult`. Use `StalkerAssert.Ok()` / `StalkerAssert.Fail()` for clean assertions
- **Error message validation**: Tests check for specific substrings in failure messages ("duplicate", "UnitMismatch", "newer")
- **State verification**: Tests verify both success and side effects (holding deleted, units adjusted, references propagated)

### Adding New Tests

1. Add test class to `PFS/PfsData.Tests/Tests/`
2. Use `StalkerTestFixture.CreatePopulated()` for tests needing pre-existing data
3. Follow naming: `MethodName_Scenario_ExpectedResult`
4. Use `StalkerAssert.Ok()` / `StalkerAssert.Fail()` instead of raw xUnit assertions for Result types
5. Run: `dotnet test --verbosity diagnostic`
