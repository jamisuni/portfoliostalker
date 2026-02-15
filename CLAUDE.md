# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Portfolio Stalker (PFS) is a Blazor WebAssembly (.NET 10.0) investment portfolio management application. It runs entirely client-side in the browser, using localStorage for persistence. Deployed as a static site to GitHub Pages.

## Build & Run Commands

```bash
# Build entire solution
dotnet build PortfolioStalker.sln

# Run locally (Blazor WASM dev server)
dotnet watch run --project PfsUI

# Run tests
dotnet test --verbosity diagnostic

# Publish for deployment
dotnet publish PortfolioStalker.sln -c Release -o release --nologo
```

CI runs on push to `main` via `.github/workflows/main.yml`: test → publish → deploy to GitHub Pages.

## Architecture

For detailed architecture documentation, see [architecture.md](architecture.md).

### Project Dependency Graph

```
PfsUI (Blazor WASM frontend - MudBlazor)
└── Client (backend logic running in WASM)
    ├── PfsReports (report generation)
    ├── PfsExtFetch (EOD fetch orchestration)
    │   └── PfsExtProviders (9 stock data API adapters)
    ├── PfsData (persistence, Stalker domain model)
    │   └── PfsHelpers (utilities)
    └── PfsConfig (market/provider/fetch configuration)
        └── PfsHelpers
All above reference → PfsTypes (core domain types, interfaces, enums)

PfsExtTransactions (bank CSV import) → PfsTypes
PfsCmdLine (net8.0 console, standalone) → PfsConfig, PfsTypes
```

### Key Architectural Patterns

- **DI with multi-interface registration** in `PfsUI/Program.cs` ([Andrew Lock pattern](https://andrewlock.net/how-to-register-a-service-with-multiple-interfaces-for-in-asp-net-core-di/))
- **IDataOwner** — localStorage persistence lifecycle (`OnLoadStorage()`/`OnSaveStorage()`)
- **ICmdHandler** — text command routing via `GetCmdPrefixes()`/`CmdAsync()`
- **IExtDataProvider** — provider adapter pattern for external stock APIs
- **Command Pattern** — all domain mutations via `StalkerDoCmd.DoAction("Add-Portfolio PfName=[...]")`
- **Report Pipeline** — `ReportPreCalc` → `RepGen*` → `RepData*` → UI rendering

### Naming Conventions

- **S-prefix:** Stalker domain entities (`SPortfolio`, `SStock`, `SHolding`, `SOrder`, `SAlarm`, `SSector`)
- **FE-prefix:** Frontend service implementations (`FEAccount`, `FEConfig`, `FEStalker`, `FEReport`, `FEEod`)
- **Store-prefix:** Data persistence classes (`StoreLatestEod`, `StoreStockMeta`, `StoreNotes`)
- **Ext-prefix:** External provider integrations (`ExtAlphaVantage`, `ExtEodHD`)
- **RC-prefix:** Report cell types in `PfsTypes/Reports/`
- **RepGen-prefix:** Report generators in `PfsReports/`
- **RepData-prefix:** Report data containers in `PfsTypes/Reports/`

### Code Markers

The codebase uses these comment markers: `// !!!TODO!!!`, `// !!!CODE!!!`, `// !!!THINK!!!`

### Key Entry Points

- **DI setup:** `PfsUI/Program.cs` — all singleton service registrations
- **Client facade:** `PFS/Client/Client.cs` (implements `IFEClient`)
- **Domain model:** `PFS/PfsData/Stalker/StalkerData.cs` — portfolio data with XML serialization
- **UI root:** `PfsUI/App.razor`, main layout at `PfsUI/Layout/MainLayout.razor`

### Data Flow

All data persists to browser localStorage via `Blazored.LocalStorage`. Domain data serializes to XML (`StalkerXML.cs`). Stock prices fetched from configurable external providers, with fetch orchestration in `PfsExtFetch/`. Currency conversion via `ExtCurrencyApi`.

### Live Data Export

Jami's personal PFS instance export is available at `/home/jami/pfs/export/`. These are the actual localStorage text files from a running instance:

| File | IDataOwner class | Content |
|------|-----------------|---------|
| `stalker.txt` | `ClientStalker` | Portfolios, stocks, holdings, orders, alarms (XML) |
| `eod.txt` | `StoreLatestEod` | Latest end-of-day prices |
| `stockmeta.txt` | `StoreStockMeta` | Stock metadata (ISIN, company names) |
| `stockhist.txt` | `StoreStockMetaHist` | Historical stock metadata |
| `notes.txt` | `StoreNotes` | Stock notes |
| `cfgmarket.txt` | `MarketConfig` | Market definitions |
| `cfgfetch.txt` | `FetchConfig` | Fetch settings |
| `cfgprov.txt` | `ProvConfig` | Provider configuration |
| `cfgapp.txt` | `AppConfig` | App settings |
| `fetcheod.txt` | `FetchEod` | Pending fetch state |
| `rates.txt` | `StoreLatesRates` | Currency exchange rates |
| `filters.txt` | `StoreReportFilters` | Report filter presets |

Use this data to understand real-world data formats when modifying serialization, reports, or domain logic.

### UI Framework

Uses **MudBlazor** component library. Components follow the `.razor` + `.razor.cs` code-behind pattern throughout.

## Testing

For test coverage details, test plan, and coverage gaps, see [testplan.md](testplan.md).

- **Run tests:** `dotnet test --verbosity diagnostic`
- **Test project:** `PFS/PfsData.Tests/` — 112 xUnit tests covering Stalker domain model
- **CI:** Tests run automatically on push to `main`

## Work Rules

### Documentation Updates

When making code changes, update the relevant documentation files:
- **[architecture.md](architecture.md)** — Update if changes affect project structure, new classes/interfaces, DI registrations, data flow, or architectural patterns.
- **[testplan.md](testplan.md)** — Update if changes add/remove tests, change coverage, or affect the test infrastructure.

### Job Tracking

All work is tracked in **[claudejobs.md](claudejobs.md)**:
- **When starting a task**, add an entry under "In Progress" with a TASK-N ID, description, and status before writing any code.
- **When finishing**, move the entry to "Fixed" with the completion date.
- **If interrupted**, the entry stays in "In Progress" with enough detail to resume later.
- **On session start**, read `claudejobs.md` and present any open items (In Progress + Open) as single-line summaries so Jami knows what's pending.

Fixed items are cleared after 30 days.
