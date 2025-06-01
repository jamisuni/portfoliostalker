/*
 * Copyright (C) 2024 Jami Suni
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/gpl-3.0.en.html>.
 */

using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;

using Serilog;

using Pfs.Helpers;
using Pfs.Types;

namespace Pfs.Data;

// All stock meta, meaning market+symbol+companyName+ISIN is handled here and atm user created
public class StoreStockMeta : IStockMeta, IStockMetaUpdate, ICmdHandler, IDataOwner // identical XML on backup & local storage
{
    protected const string _componentName = "stockmeta";
    protected readonly IPfsPlatform _platform;
    protected StoreStockMetaHist _stockMetaHist;

    protected StockMeta[] _stockMeta; // Only place where market+symbol finds it companyname

    protected record MC(MarketId market, CurrencyId currency);

    protected readonly MC[] _marketCurrencies; // Do not reset this! Its always same, survives same over restore backups etc, not stored either!

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list",
        "add <market> symbol company",
        "updatename <market> symbol newname",
        "remove <market> symbol",                   // Can allow this happen, if manually done wrong by user, then meta just comes back from stalker as "missing" later
        "get <market> symbol"                       // => ""marketId symbol what ever companyname is""
    ];

    public StoreStockMeta(StoreStockMetaHist stockMetaHist, IPfsPlatform pfsPlatform, IMarketMeta marketMetaProv)
    {
        _platform = pfsPlatform;
        _stockMetaHist = stockMetaHist;

        var enums = Enum.GetValues(typeof(MarketId));

        _marketCurrencies = new MC[enums.Length - 1];

        int p = 0;
        foreach (MarketId marketId in Enum.GetValues(typeof(MarketId)))
        {
            if (marketId == MarketId.Unknown)
                continue;

            if (marketId == MarketId.CLOSED)
                _marketCurrencies[p++] = new MC(marketId, CurrencyId.Unknown);
            else
                _marketCurrencies[p++] = new MC(marketId, marketMetaProv.Get(marketId).Currency);
        }

        Init();
    }

    protected void Init()
    {
        _stockMeta = Array.Empty<StockMeta>();
    }

    public IEnumerable<StockMeta> GetAll(MarketId marketId = MarketId.Unknown)                      // IStockMeta
    {
        if (marketId == MarketId.Unknown)
            return _stockMeta;

        return _stockMeta.Where(s => s.marketId == marketId).ToList();
    }

    public StockMeta Get(MarketId marketId, string symbol)                                          // IStockMeta
    {
        StockMeta sm = _stockMeta.FirstOrDefault(m => m.marketId == marketId && string.Equals(m.symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (sm == null && marketId == MarketId.CLOSED)
        {
            string mappedSRef = _stockMetaHist.GetMapping(symbol);

            if (string.IsNullOrWhiteSpace(mappedSRef) == false)
            {
                var map = StockMeta.ParseSRef(mappedSRef);
                sm = _stockMeta.FirstOrDefault(m => m.marketId == map.marketId && m.symbol == map.symbol);
            }
        }
        return sm;
    }

    public StockMeta Get(string sRef)                                                               // IStockMeta
    {
        var s = StockMeta.ParseSRef(sRef);

        return Get(s.marketId, s.symbol);
    }

    public StockMeta GetByISIN(string ISIN)                                                         // IStockMeta
    {
        return _stockMeta.FirstOrDefault(m => m.ISIN == ISIN);
    }

    public StockMeta AddUnknown(string sRef)                                                        // IStockMeta
    {   // Allows consumers of StockMeta to report from SRef they gave that seams missing StockMeta
        (MarketId marketId, string symbol) = StockMeta.ParseSRef(sRef);

        if (Get(sRef) != null)
            return Get(sRef);

        AddStock(marketId, symbol, "Missing!", string.Empty);

        return Get(sRef);
    }

    public bool AddStock(MarketId marketId, string symbol, string companyName, string ISIN)         // IStockMetaUpdate
    {
        if (_stockMeta.FirstOrDefault(m => m.marketId == marketId && string.Equals(m.symbol, symbol, StringComparison.OrdinalIgnoreCase)) != null)
            return true; // has already, just return true

        _stockMeta = _stockMeta.Append(new StockMeta(marketId, symbol.ToUpper(), companyName, _marketCurrencies.First(c => c.market == marketId).currency, ISIN)).ToArray();

        _stockMetaHist.AppendNewStock(marketId, symbol, companyName, _platform.GetCurrentLocalDate());

        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public bool RemoveStock(MarketId marketId, string symbol)                                       // IStockMetaUpdate
    {   // Note! This is not critical, as consumers of meta are supposed to just do 'UnknownFound' to re-add if they can find one they know should be here
        StockMeta sm = _stockMeta.FirstOrDefault(m => m.marketId == marketId && string.Equals(m.symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (sm == null)
            return false;

        List<StockMeta> tempList = _stockMeta.ToList();
        tempList.Remove(sm);
        _stockMeta = tempList.ToArray();

        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public void AddSymbolSearchMapping(string fromSymbol, MarketId toMarketId, string toSymbol, string comment)
    {
        _stockMetaHist.AppendMapping(fromSymbol, toMarketId, toSymbol, comment);
        EventNewUnsavedContent?.Invoke(this, _componentName);
    }

    public bool UpdateCompanyName(MarketId marketId, string symbol, DateOnly date, string newCompanyName)   // IStockMetaUpdate
    {
        int pos = Array.FindIndex(_stockMeta, m => m.marketId == marketId && string.Equals(m.symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (pos < 0)
            return false;

        _stockMeta[pos] = _stockMeta[pos] with { name = newCompanyName };

        _stockMetaHist.AppendUpdateName(marketId, symbol, date, newCompanyName);

        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public bool UpdateIsin(MarketId marketId, string symbol, DateOnly date, string newISIN)     // IStockMetaUpdate
    {
        int pos = Array.FindIndex(_stockMeta, m => m.marketId == marketId && string.Equals(m.symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (pos < 0)
            return false;

        _stockMeta[pos] = _stockMeta[pos] with { ISIN = newISIN };
        _stockMetaHist.AppendUpdateISIN(marketId, symbol, date, newISIN);

        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public bool UpdateFullMeta(string updSRef, string oldSRef, string companyName, DateOnly date, string comment)   // IStockMetaUpdate
    {
        var old = StockMeta.ParseSRef(oldSRef);
        var upd = StockMeta.ParseSRef(updSRef);

        int pos = Array.FindIndex(_stockMeta, m => m.marketId == old.marketId && m.symbol == old.symbol);

        if ( pos < 0) 
            return false;

        _stockMeta[pos] = new StockMeta(upd.marketId, upd.symbol.ToUpper(), companyName, _marketCurrencies.First(c => c.market == upd.marketId).currency, _stockMeta[pos].ISIN);

        _stockMetaHist.AppendUpdateSRef(upd.marketId, upd.symbol, companyName, old.marketId, old.symbol, date, comment);
    
        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public bool SplitStock(string sRef, DateOnly date, string comment)                                        // IStockMetaUpdate
    {
        var stock = StockMeta.ParseSRef(sRef);
        int pos = Array.FindIndex(_stockMeta, m => m.marketId == stock.marketId && m.symbol == stock.symbol);
        if (pos < 0)
            return false;

        _stockMetaHist.AppendSplitStock(stock.marketId, stock.symbol, date, comment);
        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public bool CloseStock(string sRef, DateOnly date, string comment)                                              // IStockMetaUpdate
    {
        var stock = StockMeta.ParseSRef(sRef);

        int pos = Array.FindIndex(_stockMeta, m => m.marketId == stock.marketId && m.symbol == stock.symbol);

        if (pos < 0)
            return false;

        _stockMeta[pos] = new StockMeta(MarketId.CLOSED, stock.symbol, _stockMeta[pos].name, CurrencyId.Unknown, _stockMeta[pos].ISIN);

        _stockMetaHist.AppendCloseStock(stock.marketId, stock.symbol, date, comment);

        EventNewUnsavedContent?.Invoke(this, _componentName);
        return true;
    }

    public void DestroyStock(string sRef)
    {   // Everything, even history.. gets deleted.. no trace left from stock...
        _stockMeta = _stockMeta.Where(s => s.GetSRef() != sRef).ToArray();
        _stockMetaHist.DestroyStock(sRef);
    }

    public event EventHandler<string> EventNewUnsavedContent;                                                       // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnInitDefaults() { Init(); }

    public List<string> OnLoadStorage()
    {
        List<string> warnings = new();

        try
        {
            Init();

            string content = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(content))
                return new();

            warnings = ImportXml(content);
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, OnLoadStorage failed w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        return warnings;
    }

    public void OnSaveStorage() {
        _platform.PermWrite(_componentName, ExportXml());
    }

    public string CreateBackup()
    {
        return ExportXml();
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return ExportXml(symbols);
    }

    public List<string> RestoreBackup(string content)
    {
        return ImportXml(content);
    }

    public string GetCmdPrefixes() { return _componentName; }                                        // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                                          // ICmdHandler
    {
        await Task.CompletedTask;

        MarketId marketId;
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                {
                    int i = 0;
                    StringBuilder sb = new();
                    foreach (StockMeta sm in _stockMeta)
                    {
                        sb.AppendLine($"{i} {sm.marketId}${sm.symbol} [{sm.name}]");
                        i++;
                    }

                    if (i == 0)
                        return new OkResult<string>("--empty--");
                    else
                        return new OkResult<string>(sb.ToString());
                }

            case "add": // "add <market> symbol company"
                {
                    marketId = Enum.Parse<MarketId>(parseResp.Data["<market>"]);
                    if (AddStock(marketId, parseResp.Data["symbol"].ToUpper(), parseResp.Data["company"], string.Empty))
                        return new OkResult<string>($"Stock added to meta!");
                    else
                        return new FailResult<string>($"Failed to add!");
                }

            case "updatename": // "updatename <market> symbol date newname"
                {
                    marketId = Enum.Parse<MarketId>(parseResp.Data["<market>"]);
                    StockMeta sm = Get(marketId, parseResp.Data["symbol"].ToUpper());
                    DateOnly date = DateOnlyExtensions.ParseYMD(parseResp.Data["date"]);
                    if (UpdateCompanyName(marketId, parseResp.Data["symbol"].ToUpper(), date, parseResp.Data["newname"]))
                        return new OkResult<string>($"Company name updated to meta!");
                    else
                        return new FailResult<string>($"Failed to find!");
                }

            case "remove": // "remove <market> symbol"
                {
                    marketId = Enum.Parse<MarketId>(parseResp.Data["<market>"]);
                    StockMeta sm = _stockMeta.SingleOrDefault(s => s.marketId == marketId && string.Equals(s.symbol, parseResp.Data["symbol"], StringComparison.OrdinalIgnoreCase));

                    if (sm == null)
                        return new FailResult<string>($"Not found!");

                    RemoveStock(marketId, sm.symbol);

                    return new OkResult<string>($"{sm.marketId} {sm.symbol} {sm.name}");
                }

            case "get": //"get <market> symbol" => ""marketId symbol what ever companyname is""
                {
                    marketId = Enum.Parse<MarketId>(parseResp.Data["<market>"]);
                    StockMeta sm = _stockMeta.SingleOrDefault(s => s.marketId == marketId && string.Equals(s.symbol, parseResp.Data["symbol"], StringComparison.OrdinalIgnoreCase));

                    if (sm != null)
                        return new OkResult<string>($"{sm.marketId} {sm.symbol} {sm.name}");
                    else
                        return new FailResult<string>($"Not found!");
                }
        }
        return new FailResult<string>($"StoreStockMeta unknown command: {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                                       // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }

    protected string ExportXml(List<string> symbols = null)
    {
        XElement rootPFS = new XElement("PFS");
        XElement stockMetaElem = new XElement("StockMeta");
        rootPFS.Add(stockMetaElem);

        foreach (StockMeta sm in _stockMeta)
        {
            if (symbols != null && symbols.Contains(sm.symbol) == false)
                continue;

            XElement smElem = new XElement("Stock");
            smElem.SetAttributeValue("SRef", $"{sm.marketId}${sm.symbol}");
            smElem.SetAttributeValue("Name", sm.name);
            smElem.SetAttributeValue("ISIN", sm.ISIN);
            stockMetaElem.Add(smElem);
        }
        return rootPFS.ToString();
    }

    protected List<string> ImportXml(string xml)
    {
        List<string> warnings = new();
        List<StockMeta> cfgs = new();

        try
        {
            XDocument xmlDoc = XDocument.Parse(xml);
            XElement rootPFS = xmlDoc.Descendants("PFS").First();
            XElement stockMetaElem = rootPFS.Element("StockMeta");

            foreach (XElement smElem in stockMetaElem.Descendants())
            {
                if (smElem.Name.ToString() == "Stock")
                {
                    string sRef = string.Empty;

                    try
                    {
                        sRef = (string)smElem.Attribute("SRef");
                        (MarketId marketId, string symbol) = StockMeta.ParseSRef(sRef);
                        string name = (string)smElem.Attribute("Name");
                        string ISIN = (string)smElem.Attribute("ISIN");
                        cfgs.Add(new StockMeta(marketId, symbol, name, _marketCurrencies.First(c => c.market == marketId).currency, ISIN));
                    }
                    catch (Exception ex)
                    {
                        string wrnmsg = $"{_componentName}, failed to load {sRef} meta w exception [{ex.Message}]";
                        warnings.Add(wrnmsg);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, failed to load metas w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        _stockMeta = cfgs.ToArray();
        return warnings;
    }
}
