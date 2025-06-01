using Pfs.Data.Stalker;
using Pfs.Types;
using System.Collections.ObjectModel;

namespace Pfs.Client;

public interface IFEStalker
{
    Result DoAction(string cmd);

    StalkerDoCmd GetCopyOfStalker();

    Result DoActionSet(List<string> actionSet);

    StockMeta GetStockMeta(MarketId marketId, string symbol);

    StockMeta CloseStock(MarketId marketId, string symbol, DateOnly date, string comment);

    Result SplitStock(MarketId marketId, string symbol, DateOnly date, decimal splitFactor, string comment);

    StockMeta UpdateStockMeta(MarketId marketId, string symbol, MarketId updMarketId, string updSymbol, string updName, DateOnly date, string comment);

    StockMeta FindStock(string symbol, CurrencyId optMarketCurrency = CurrencyId.Unknown, string optISIN = null);

    // This looks any match, partial symbol or even part of name.. target to propose long list where user selects
    IReadOnlyCollection<StockMeta> FindStocksList(string search, MarketId marketId = MarketId.Unknown);

    Task<StockMeta[]> FindStockExtAsync(string symbol, MarketId optMarketId = MarketId.Unknown, CurrencyId optMarketCurrency = CurrencyId.Unknown);

    StockMeta AddNewStockMeta(MarketId marketId, string symbol, string companyName, string ISIN = "");

    StockMeta UpdateCompanyNameIsin(MarketId marketId, string symbol, DateOnly date, string companyName, string ISIN = "");

    void AddSymbolSearchMapping(string fromSymbol, MarketId toMarketId, string toSymbol, string comment);

    ReadOnlyCollection<SAlarm> StockAlarmList(MarketId marketId, string symbol);

    ReadOnlyCollection<SOrder> StockOrderList(string pfName, MarketId marketId, string symbol);

    IReadOnlyCollection<SPortfolio> GetPortfolios();

    string[] GetSectorNames();
    string[] GetSectorFieldNames(int sectorId);
    public string[] GetStockSectorFields(string sRef);
}
