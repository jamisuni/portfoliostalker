# Architecture

Detailed architecture reference for Portfolio Stalker. See [CLAUDE.md](CLAUDE.md) for build commands and quick-start guidance.

## Solution Structure

```
PortfolioStalker.sln
├── PfsUI/              Blazor WASM frontend (MudBlazor)
├── PFS/Client/         Backend facade & frontend services
├── PFS/PfsReports/     Report generation
├── PFS/PfsExtFetch/    EOD fetch orchestration
├── PFS/PfsExtProviders/ External stock data API adapters
├── PFS/PfsData/        Persistence & Stalker domain model
├── PFS/PfsConfig/      Market/provider/fetch configuration
├── PFS/PfsHelpers/     Shared utilities
├── PFS/PfsTypes/       Core domain types, interfaces, enums
├── PFS/PfsExtTransactions/ Bank CSV import
├── PFS/PfsCmdLine/     Standalone .NET 8.0 console app
└── PFS/PfsData.Tests/  xUnit tests for Stalker domain
```

## Dependency Graph

```
PfsUI (Blazor WASM, MudBlazor)
└── Client (IFEClient facade)
    ├── PfsReports (RepGen* report generators)
    ├── PfsExtFetch (FetchEod orchestration)
    │   └── PfsExtProviders (9 stock data API adapters)
    ├── PfsData (Stalker domain model, Store* persistence)
    │   └── PfsHelpers (CmdParser, extensions)
    └── PfsConfig (MarketConfig, ProvConfig, FetchConfig)
        └── PfsHelpers
All above reference → PfsTypes (domain types, interfaces, enums)

PfsExtTransactions (bank CSV import) → PfsTypes
PfsCmdLine (net8.0 console) → PfsConfig, PfsTypes
```

---

## PfsTypes — Core Domain Types

Foundation library used by every other project. Contains all domain models, interfaces, and enums.

### Stalker Domain Entities (S-prefix)

| Entity | File | Description |
|--------|------|-------------|
| `SPortfolio` | `Stalker/SPortfolio.cs` | Broker/bank account — contains tracked stock SRefs, orders, holdings, trades |
| `SStock` | `Stalker/SStock.cs` | Tracked stock — SRef (MarketId$SYMBOL), sector assignments, alarms |
| `SHolding` | `Stalker/SHolding.cs` | Stock ownership — units, purchase price/date, fees, currency rate, nested dividends and sale records |
| `SOrder` | `Stalker/SOrder.cs` | Pending buy/sell order — type, units, price, last date, optional fill date |
| `SAlarm` | `Stalker/SAlarm.cs` | Abstract base with polymorphic types: `SAlarmUnder`, `SAlarmOver`, `SAlarmTrailingSellP`, `SAlarmTrailingBuyP`. Custom JSON converter for serialization |
| `SSector` | `Stalker/SSector.cs` | User-defined stock grouping — up to 3 sectors, 18 fields each |

### Core Interfaces

| Interface | Purpose |
|-----------|---------|
| `IDataOwner` | localStorage persistence lifecycle: `OnLoadStorage()`, `OnSaveStorage()`, `CreateBackup()`, `RestoreBackup()` |
| `ICmdHandler` | Text command routing: `GetCmdPrefixes()`, `CmdAsync()` |
| `IExtDataProvider` | External stock API adapter: `GetEodLatestAsync()`, `GetEodHistoryAsync()`, `IsMarketSupport()` |
| `IEodLatest` / `IEodHistory` | EOD price access |
| `ILatestRates` | Currency exchange rates |
| `IStockMeta` | Stock metadata (ISIN, company name) |
| `IMarketMeta` | Market definitions and metadata |
| `IOnUpdate` | Scheduler callback for timed updates |
| `IPfsPlatform` | Platform abstractions (time, storage, provider support) |

### Report Types

- **RC-prefix** (Report Cell): `RCStock`, `RCHolding`, `RCTrade`, `RCOrder`, `RCEod`, `RCDivident`, `RCGrowth`, `RCExtraColumn`
- **RepData-prefix** (Report Data): `RepDataPfStocks`, `RepDataDivident`, `RepDataTracking`, `RepDataWeight`, `RepDataInvested`, `RepDataExpHoldings`, `RepDataExpSales`, etc.

### Key Data Types

- **FullEOD**: Date, Close, Open, High, Low, PrevClose, Volume. -1 indicates unavailable data.
- **StockMeta**: Market + symbol + company name + ISIN
- **Transaction**: Broker import fields (action, date, ISIN, units, price, fee, currency)

---

## PfsData — Persistence & Domain Model

### Stalker Core

| Class | File | Role |
|-------|------|------|
| `StalkerData` | `Stalker/StalkerData.cs` | Raw data container — `_portfolios`, `_stocks`, `_sectors`. Read-only accessors via `ref readonly` and `ReadOnlyCollection` |
| `StalkerDoCmd` | `Stalker/StalkerDoCmd.cs` | Extends StalkerData. Parses and executes text commands ("Add-Portfolio PfName=[...]"). Tracks all performed actions for audit |
| `StalkerXML` | `Stalker/StalkerXML.cs` | XML import/export. Structure: `<PFS><Portfolios>...<Stocks>...<Sectors>...</PFS>`. Supports partial exports by symbol filter |
| `StalkerAction` | `Stalker/StalkerAction.cs` | Command object — Operation (Add/Delete/Set/Close/Split/Modify) + Element (Portfolio/Stock/Holding/Order/Alarm) + parameters |
| `StalkerParam` | `Stalker/StalkerParam.cs` | Parameter parsing — SRef, Date, Decimal range, Enum validation |
| `StalkerSplit` | `Stalker/StalkerSplit.cs` | Command tokenizer — handles bracketed values like `PfName=[My Portfolio]` |
| `StalkerEnums` | `Stalker/StalkerEnums.cs` | Domain enums for operations, elements, alarm types |

### Store Classes (IDataOwner implementations)

Each Store class manages a localStorage key and participates in the persistence lifecycle.

| Class | Storage Key | Interfaces | Content |
|-------|-------------|------------|---------|
| `StoreLatestEod` | `eod` | IEodLatest, IEodHistory, ICmdHandler, IDataOwner | Latest EOD prices + rolling history |
| `StoreStockMeta` | `stockmeta` | IStockMeta, IStockMetaUpdate, ICmdHandler, IDataOwner | Company name + ISIN mappings |
| `StoreStockMetaHist` | `stockhist` | IDataOwner | Historical stock metadata |
| `StoreNotes` | `notes` | IStockNotes, IDataOwner | User stock notes |
| `StoreLatestRates` | `rates` | ILatestRates, IDataOwner | Currency exchange rates |
| `StoreUserEvents` | `userevents` | IUserEvents, IDataOwner | User event history |
| `StoreExtraColumns` | `extracols` | IExtraColumns, IDataOwner | Custom report columns |
| `StoreReportFilters` | `filters` | IDataOwner | Saved report filter presets |

### Helpers

- `ExpiredStocks` — Identifies stocks with stale EOD data
- `HoldingLvlEvents` — Generates holding-level events (dividends, trades)
- `UserEvent` — User event tracking

---

## PfsConfig — Configuration

| Class | Storage Key | Interfaces | Content |
|-------|-------------|------------|---------|
| `MarketConfig` | `cfgmarket` | IMarketMeta, IPfsSetMarketConfig, ICmdHandler, IDataOwner | Per-market settings: active status, holidays, min fetch interval. Static definitions for NASDAQ, NYSE, AMEX, TSX, TSXV, OMXH, OMX, XETRA, LSE, etc. |
| `ProvConfig` | `cfgprov` | IPfsProvConfig, ICmdHandler, IDataOwner | API keys for external providers. Commands: "setkey", "delkey", "clearall" |
| `FetchConfig` | `cfgfetch` | IPfsFetchConfig, ICmdHandler, IDataOwner | Provider-to-market/symbol mappings. Detailed rules (specific symbols) take priority over market-wide rules |
| `AppConfig` | `cfgapp` | ICmdHandler, IDataOwner | Application-wide settings |
| `MarketHolidays` | — | — | Static market holiday definitions |

---

## PfsExtFetch — Fetch Orchestration

Orchestrates stock data fetching from multiple providers with credit management.

| Class | Role |
|-------|------|
| `FetchEod` | Main orchestrator (IFetchEod, IOnUpdate, IDataOwner). Owns FetchEodTask[] instances. ConcurrentQueue for fetch requests. Tracks results in rotating buffer (last 30). Storage key: `fetcheod` |
| `FetchEodTask` | Per-provider wrapper. States: Disabled/Free/Fetching/Ready/Error/Testing. Tracks daily/monthly credits. Spawns fetches on thread pool |
| `FetchEodPending` | Manages pending symbol fetch queue |
| `FetchRates` | Currency rate fetching via ExtCurrencyApi |

---

## PfsExtProviders — External API Adapters

Nine provider adapters, each implementing `IExtProvider` + `IExtDataProvider`:

| Provider | API | Markets |
|----------|-----|---------|
| `ExtAlphaVantage` | Alpha Vantage | Multiple |
| `ExtEodHD` | EODHD | NASDAQ, NYSE, AMEX, TSX, TSXV, OMXH, OMX, XETRA, LSE |
| `ExtPolygon` | Polygon.io | US markets |
| `ExtTiingo` | Tiingo | Multiple |
| `ExtFmp` | Financial Modeling Prep | Multiple |
| `ExtMarketstack` | Marketstack | Multiple |
| `ExtTwelveData` | Twelve Data | Multiple |
| `ExtUnibit` | Unibit | Multiple |
| `ExtCurrencyApi` | CurrencyAPI | FX rates only |

Common interface: `SetPrivateKey()`, `IsMarketSupport()`, `GetBatchSizeLimit()`, `GetEodLatestAsync()`, `GetEodHistoryAsync()`

---

## PfsReports — Report Generation

### Pipeline

1. `ReportPreCalc` aggregates domain data into `List<RCStock>` (stock-oriented report cells)
2. `RepGen*` classes transform RCStock data with filters into `RepData*` report containers
3. UI renders RepData through report components

### Report Generators

| Generator | Output | Description |
|-----------|--------|-------------|
| `RepGenPfStocks` | `RepDataPfStocks` | Portfolio stock listing with holdings/alarms/orders |
| `RepGenDivident` | `RepDataDivident` | Dividend analysis |
| `RepGenTracking` | `RepDataTracking` | Watched stocks not in portfolio |
| `RepGenWeight` | `RepDataWeight` | Portfolio weight vs target allocation |
| `RepGenInvested` | `RepDataInvested` | Investment summary and growth |
| `RepGenPfSales` | `RepDataPfSales` | Closed positions, profit/loss |
| `RepGenStMgHoldings` | `RepDataStMgHoldings` | Stock management holdings view |
| `RepGenStMgHistory` | `RepDataStMgHistory` | Stock management transaction history |
| `RepGenExpHoldings` | `RepDataExpHoldings` | Export: holdings |
| `RepGenExpDividents` | `RepDataExpDividents` | Export: dividends |
| `RepGenExpSales` | `RepDataExpSales` | Export: sales |
| `OverviewGroups` | `OverviewGroupsData` | Group-level summary |
| `OverviewStocks` | `OverviewStocksData` | Stock-level summary |

---

## Client — Backend Facade

### Core

| Class | Role |
|-------|------|
| `Client` | Main facade (IFEClient). Timer-based scheduler (1s tick). Emits `EventPfsClient2PHeader` and `EventPfsClient2Page` for UI updates. Manages startup warnings |
| `ClientData` | Startup orchestrator — calls `OnLoadStorage()` on all IDataOwner components |
| `ClientStalker` | Wraps StalkerDoCmd as IDataOwner + ICmdHandler. Manages portfolio persistence |
| `ClientScheduler` | Manages IOnUpdate callbacks for timed background tasks |
| `ClientContent` | Global state container (IPfsStatus) |
| `ClientCmdTerminal` | Command terminal service for debug/admin UI |

### Frontend Services (FE-prefix)

These are the API surface that UI components call:

| Service | Interface | Key Methods |
|---------|-----------|-------------|
| `FEStalker` | `IFEStalker` | `DoAction(cmd)`, `DoActionSet()`, `GetCopyOfStalker()`, `CloseStock()` |
| `FEReport` | `IFEReport` | Report generation, filter management |
| `FEConfig` | `IFEConfig` | Configuration access |
| `FEAccount` | `IFEAccount` | Account/demo management |
| `FEEod` | `IFEEod` | EOD data access, `GetHistoryRateAsync()` |

---

## PfsUI — Blazor WASM Frontend

### DI Setup (`Program.cs`)

All services registered as singletons via multi-interface pattern (Andrew Lock). Key registrations:
- `PfsClientAccess` — service locator facade exposing `Cmd()`, `Account()`, `Config()`, `Stalker()`, `Client()`, `Report()`, `Eod()`, `Platform()`
- `PfsUiState` — global event bus (`OnMenuUpdated`)
- `BlazorPlatform` — IPfsPlatform implementation (localStorage bridge, demo mode)
- `UiF` — UI formatting utilities (currency symbols, decimal formatting)

### Pages

| Page | Route | Content |
|------|-------|---------|
| `Home` | `/` | Dashboard with tabs: Overview, Invested, Weight, Dividends, Export, Tracking. Supports `?demo=N` query param |
| `Portfolio` | `/portfolio/{PfName}` | Single portfolio: All Stocks tab (ReportPfStocks), Sales tab (ReportPfSales). Menu: rename/delete/top |

### Layout

- `MainLayout` — Shell with PageHeader reference. Manages custom menu items, report state, speed operation buttons
- `PageHeader` — Central control: portfolio dropdown, stock fetch status, report filters, setup wizard auto-launch
- `NavMenu` — Navigation sidebar

### Component Areas

| Area | Key Components | Purpose |
|------|---------------|---------|
| `Overview/` | Overview, OverviewGroups, OverviewStocks | Dashboard summary views. Stock grid with configurable columns |
| `Reports/` | ReportPfStocks, ReportWeight, ReportInvested, ReportDivident, ReportTracking, ReportPfSales | Report display with sorting/filtering |
| `Reports/Cells/` | RCellEod, RCellTotalGrowth, RCellAlarm, RCellDivident, etc. | Reusable display cells with color coding and links |
| `Dialogs/` | DlgSetupWizard, DlgTerminal, DlgLogin, DlgStockMeta, etc. | General-purpose modals |
| `Dialogs/Stalker/` | DlgPortfolioEdit, DlgHoldingsEdit, DlgOrderEdit, DlgAlarmEdit, DlgSale, DlgDividentAdd, DlgStockSelect | Domain CRUD dialogs |
| `StockMgmt/` | StockMgmtDlg (5 tabs: Note, History, Alarms, Orders, Holdings) | Detailed stock management |
| `Settings/` | SettMainTab, SettMarkets, SettProviders, SettFetch, SettSectors | Configuration UI |
| `Import/` | DlgImport, ImportTransactions | Backup restore, bank CSV import |
| `Export/` | ReportExpHoldings, ReportExpSales, ReportExpDividents | CSV/HTML export with column selection |
| `Widgets/` | WidgSelSymbol, WidgStockSectors | Smart stock search, sector selection |

### UI Patterns

- **Code-behind**: `.razor` + `.razor.cs` throughout
- **Parent-child**: Parents hold `_child` refs, call public methods; children emit `EventCallback<T>`
- **Dialogs**: Return data via `MudDialog.Close(DialogResult.Ok(data))`
- **Reports**: `ByOwner_ReloadReport()` for external refresh triggers
- **State mutations**: All go through `IFEStalker.DoAction(cmd)` text commands

---

## PfsExtTransactions — Bank CSV Import

| Class | Role |
|-------|------|
| `BtParser` | Base CSV parser. Defines `BtMap[]` header-to-field mappings. Converts CSV lines to `Transaction` objects |
| `BtNordnet` | Nordnet (Finnish broker) specific parser. Maps Finnish column headers to transaction fields |

---

## PfsCmdLine — Console App

Standalone .NET 8.0 console application. Minimal implementation, mostly scaffolding. Intended for CLI-based configuration of fetch rules and provider keys.

---

## Key Architectural Patterns

1. **Multi-Interface DI**: Services implement multiple interfaces, registered via lambda factories
2. **IDataOwner Lifecycle**: All persistent components participate in localStorage load/save/backup cycle
3. **ICmdHandler Routing**: Text commands dispatched to registered handlers by prefix
4. **Command Pattern**: `StalkerDoCmd.DoAction("Add-Portfolio PfName=[...]")` — all domain mutations via parsed text commands
5. **Report Pipeline**: ReportPreCalc → RepGen* → RepData* → UI rendering
6. **Provider Adapter**: IExtDataProvider implementations wrap each external API
7. **Polymorphic Serialization**: SAlarm types use custom JSON converter; domain data uses XML
