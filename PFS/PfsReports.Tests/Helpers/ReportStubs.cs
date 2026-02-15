using Pfs.Config;
using Pfs.Types;

namespace PfsReports.Tests.Helpers;

public class StubStockMeta : IStockMeta
{
    private readonly Dictionary<string, StockMeta> _stocks = new();

    public StubStockMeta Add(MarketId market, string symbol, string name, CurrencyId currency)
    {
        var sm = new StockMeta(market, symbol, name, currency);
        _stocks[sm.GetSRef()] = sm;
        return this;
    }

    public StockMeta Get(string sRef) => _stocks.GetValueOrDefault(sRef);

    public StockMeta Get(MarketId marketId, string symbol) => Get($"{marketId}${symbol}");

    public IEnumerable<StockMeta> GetAll(MarketId marketId = MarketId.Unknown)
    {
        if (marketId == MarketId.Unknown)
            return _stocks.Values;
        return _stocks.Values.Where(s => s.marketId == marketId);
    }

    public StockMeta GetByISIN(string ISIN) => _stocks.Values.FirstOrDefault(s => s.ISIN == ISIN);

    public StockMeta AddUnknown(string sRef)
    {
        var (marketId, symbol) = StockMeta.ParseSRef(sRef);
        var sm = new StockMeta(marketId, symbol, "Unknown", CurrencyId.USD);
        _stocks[sRef] = sm;
        return sm;
    }
}

public class StubEodLatest : IEodLatest
{
    private readonly Dictionary<string, FullEOD> _eods = new();

    public StubEodLatest Add(string sRef, decimal close, decimal prevClose, DateOnly date)
    {
        _eods[sRef] = new FullEOD { Close = close, PrevClose = prevClose, Date = date };
        return this;
    }

    public FullEOD GetFullEOD(string sRef) => _eods.GetValueOrDefault(sRef);

    public FullEOD GetFullEOD(MarketId marketId, string symbol) => GetFullEOD($"{marketId}${symbol}");
}

public class StubLatestRates : ILatestRates
{
    private readonly Dictionary<CurrencyId, decimal> _rates = new();

    public CurrencyId HomeCurrency { get; set; } = CurrencyId.EUR;

    public StubLatestRates Add(CurrencyId currency, decimal rate)
    {
        _rates[currency] = rate;
        return this;
    }

    public decimal GetLatest(CurrencyId currency) => _rates.GetValueOrDefault(currency, 0m);

    public (DateOnly date, CurrencyRate[] rates) GetLatestInfo()
    {
        return (DateOnly.FromDateTime(DateTime.UtcNow),
                _rates.Select(kvp => new CurrencyRate(kvp.Key, kvp.Value)).ToArray());
    }
}

public class StubMarketMeta : IMarketMeta
{
    private readonly List<MarketMeta> _markets = new();
    private readonly Dictionary<MarketId, (DateOnly localDate, DateTime utcTime)> _closings = new();

    public StubMarketMeta Add(MarketId id, string mic, string name, CurrencyId currency, DateOnly lastClosingDate)
    {
        _markets.Add(new MarketMeta(id, mic, name, currency));
        _closings[id] = (lastClosingDate, DateTime.UtcNow.AddHours(-1));
        return this;
    }

    public IEnumerable<MarketMeta> GetActives() => _markets;

    public MarketMeta Get(MarketId marketId) => _markets.FirstOrDefault(m => m.ID == marketId);

    public (DateOnly localDate, DateTime utcTime) LastClosing(MarketId marketId)
        => _closings.GetValueOrDefault(marketId, (DateOnly.MinValue, DateTime.MinValue));

    public DateTime NextClosingUtc(MarketId marketId) => DateTime.UtcNow.AddHours(8);

    public MarketStatus[] GetMarketStatus()
    {
        return _markets.Select(m =>
        {
            var (localDate, utcTime) = _closings.GetValueOrDefault(m.ID);
            return new MarketStatus(m, true, localDate, utcTime, DateTime.UtcNow.AddHours(8), 30);
        }).ToArray();
    }
}

public class StubPfsPlatform : IPfsPlatform
{
    public DateTime GetCurrentUtcTime() => new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);
    public DateOnly GetCurrentUtcDate() => new DateOnly(2026, 2, 15);
    public DateTime GetCurrentLocalTime() => GetCurrentUtcTime().AddHours(2);
    public DateOnly GetCurrentLocalDate() => GetCurrentUtcDate();
    public void PermWrite(string key, string value) { }
    public string PermRead(string key) => null;
    public void PermRemove(string key) { }
    public void PermClearAll() { }
    public List<string> PermGetKeys() => new();
    public List<ExtProviderId> GetClientProviderIDs(ExtProviderJobType jobType = ExtProviderJobType.Unknown) => new();
    public List<ExtProviderId> GetMarketSupport(MarketId marketId) => new();
}

public class StubStockNotes : IStockNotes
{
    public string GetHeader(string sRef) => null;
    public Note Get(string sRef) => null;
    public void Store(string sRef, Note note) { }
}

public class StubPfsStatus : IPfsStatus
{
    public AccountTypeId AccountType { get; set; } = AccountTypeId.Unknown;
    public bool AllowUseStorage { get; set; } = true;

    public Task SendPfsClientEvent(PfsClientEventId id, object data = null) => Task.CompletedTask;
    public event IPfsStatus.CallbackEvPfsClientArgs EvPfsClientAsync { add { } remove { } }

    public int GetAppCfg(string id) => 0;
    public int GetAppCfg(AppCfgId id) => 0;
}

public class StubReportFilters : IReportFilters
{
    public bool AllowPF(string pfName) => true;
    public bool AllowSector(int sectorId, string field) => true;
    public bool AllowMarket(MarketId marketId) => true;
    public bool AllowOwning(ReportOwningFilter owning) => true;
}

public class FilterByPfReportFilters : IReportFilters
{
    private readonly HashSet<string> _allowedPfs;

    public FilterByPfReportFilters(params string[] pfs) => _allowedPfs = new(pfs);

    public bool AllowPF(string pfName) => _allowedPfs.Contains(pfName);
    public bool AllowSector(int sectorId, string field) => true;
    public bool AllowMarket(MarketId marketId) => true;
    public bool AllowOwning(ReportOwningFilter owning) => true;
}

public class FilterBySectorReportFilters : IReportFilters
{
    private readonly int _sectorId;
    private readonly HashSet<string> _allowedFields;

    public FilterBySectorReportFilters(int sectorId, params string[] fields)
    {
        _sectorId = sectorId;
        _allowedFields = new(fields);
    }

    public bool AllowPF(string pfName) => true;
    public bool AllowSector(int sectorId, string field) =>
        sectorId != _sectorId || _allowedFields.Contains(field ?? "");
    public bool AllowMarket(MarketId marketId) => true;
    public bool AllowOwning(ReportOwningFilter owning) => true;
}

public class FilterByOwningReportFilters : IReportFilters
{
    private readonly HashSet<ReportOwningFilter> _allowed;

    public FilterByOwningReportFilters(params ReportOwningFilter[] owning) => _allowed = new(owning);

    public bool AllowPF(string pfName) => true;
    public bool AllowSector(int sectorId, string field) => true;
    public bool AllowMarket(MarketId marketId) => true;
    public bool AllowOwning(ReportOwningFilter owning) => _allowed.Contains(owning);
}

public class StubExtraColumns : IExtraColumns
{
    public RCExtraColumn Get(int col, string sRef) => null;
}

public class StubFetchConfig : Pfs.Config.IPfsFetchConfig
{
    public ExtProviderId[] GetUsedProvForStock(MarketId market, string symbol) => null;
    public ExtProviderId GetDedicatedProviderForSymbol(MarketId market, string symbol) => ExtProviderId.Unknown;
    public void SetDedicatedProviderForSymbol(MarketId market, string symbol, ExtProviderId providerId) { }
    public MarketId[] GetMarketsPerRulesForProvider(ExtProviderId providerId) => Array.Empty<MarketId>();
    public ExtProviderId GetRatesProv() => ExtProviderId.Unknown;
}
