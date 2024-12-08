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

namespace Pfs.Config;

// Fetch configs those define what provider is used for what symbol for market.
public class FetchConfig : IPfsFetchConfig, ICmdHandler, IDataOwner // identical XML on backup & local storage
{
    protected const string _componentName = "cfgfetch";

    protected IPfsPlatform _platform;
    protected IPfsProvConfig _provConfigs;

    protected ProvFetchCfg[] _fetchCfg;
    protected RatesFetchCfg _ratesCfg;

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list"
        // 2024-Apr: Did delete most as UI works now ok.. add later more if really need
    ];

    /* All fetch rules target to single market on time, or just specific stocks on it!
     * 
     * Supports two different type fetch rules, those control what stock gets fetched by what provider:
     * - Detailed listing of Symbol(s) those specific rule defines is allowing to be used only 
     *   for single provider. This is priority rule, thats symbol always fetch before un-ruled ones.
     * - Market specific rules that tells to fetch all those symbols those dont have dedicated rule 
     *   with specified one or many providers. What one of these providers is used is semi random.
     *   
     * Note! Any duplicate rules for symbol or for market are to be ignored, first rule does rule!
     * 
     * Decision: Only "Minimum Mins Before Fetch After Market Close" ala MinFetchMins limit is done to 
     *           market itself, and thats how it stays... if stock comes here for fetching all rules
     *           and extproviders apply.. no further time limits are to be used!
     * 
     * Decision: No portfolios are to be included rules, doesnt not solve any problem.. not even problem 
     *           of what to be fetched...if wants something portfolio base priority that must be done on
     *           fetch button side that allows to fetch specific portfolios only from dropdown menu
     *           
     * Decision: There is not going to be negative rules or symbols, like !MSFT that would prevent 
     *           specific provider to be used for it... just do problem cases w one provider that works!
     * 
     * !!!LATER!!! "abcd=abc.d Tiingo" -> calls Tiingo but uses different symbol for that provider
     * 
     * !!!SOON!!! Add here fetch rule priorities, and improve 'GetUsedProvForStock' to return per those
     */

    public FetchConfig(IPfsPlatform platform, IPfsProvConfig provConfigs)
    {
        _platform = platform;
        _provConfigs = provConfigs;

        Init();
    }

    protected void Init()
    {
        _fetchCfg = Array.Empty<ProvFetchCfg>();
        _ratesCfg = new RatesFetchCfg(ExtProviderId.Unknown);
    }

    public IReadOnlyCollection<ProvFetchCfg> GetFetchCfg()
    {
        return _fetchCfg.AsReadOnly();
    }

    public void SetFetchCfg(ProvFetchCfg[] allCfgs)
    {
        _fetchCfg = allCfgs;

        EventNewUnsavedContent?.Invoke(this, _componentName);
    }

    public RatesFetchCfg GetRatesCfg()
    {
        return _ratesCfg;
    }

    public Result SetRatesCfg(RatesFetchCfg cfg)
    {
        if (string.IsNullOrWhiteSpace(_provConfigs.GetPrivateKey(cfg.provider)) == true)
            return new FailResult("Cant activate as provider key is not set!");

        _ratesCfg = cfg;

        EventNewUnsavedContent?.Invoke(this, _componentName);

        return new OkResult();
    }

    public ExtProviderId[] GetAvailableRatesProviders()
    {
        List<ExtProviderId> ret = new();

        foreach ( ExtProviderId provId in Enum.GetValues(typeof(ExtProviderId)))
        {
            if (provId.SupportsRates() == false)
                continue;

            if (string.IsNullOrWhiteSpace(_provConfigs.GetPrivateKey(provId)) == true)
                continue;

            ret.Add(provId);
        }
        return ret.ToArray();
    }

    public ExtProviderId GetRatesProv()                                                             // IPfsFetchConfig
    {
        return _ratesCfg.provider;
    }

    public ExtProviderId GetDedicatedProviderForSymbol(MarketId market, string symbol)              // IPfsFetchConfig
    {
        foreach (ProvFetchCfg cfg in _fetchCfg)
        {
            if (cfg.market != market || IsSymbol(symbol, cfg.symbols) == false)
                continue;

            return cfg.providers[0];
        }
        return ExtProviderId.Unknown;
    }

    protected bool IsSymbol(string symbol, string symbols)
    {
        if (string.IsNullOrWhiteSpace(symbols) == true)
            return false;

        return symbols.Split(',').Contains(symbol);
    }

    protected void RemoveDedicatedRule(MarketId market, string symbol)
    {
        for ( int p = _fetchCfg.Count() - 1; p >= 0; p--) 
        {
            if (_fetchCfg[p].market != market || IsSymbol(symbol, _fetchCfg[p].symbols) == false)
                continue;

            List<string> symbols = _fetchCfg[p].symbols.Split(',').ToList();
            symbols.Remove(symbol);
            _fetchCfg[p] = _fetchCfg[p] with { symbols = string.Join(',', symbols) };
        }
        // Later! This leaves potentially now empty rule itself theer
    }

    public void SetDedicatedProviderForSymbol(MarketId market, string symbol, ExtProviderId providerId)
    {
        // Make sure this is not duplicate call
        if (GetDedicatedProviderForSymbol(market, symbol) == providerId)
            return;

        // And get rid of potential old dedicated rule
        RemoveDedicatedRule(market, symbol);

        for (int p = 0; p < _fetchCfg.Count(); p++)
        {
            if (_fetchCfg[p].market != market ||                        // wrong market
                _fetchCfg[p].providers.Count() != 1 ||                  // not single provider rule
                _fetchCfg[p].providers[0] != providerId ||              // not correct provider
                string.IsNullOrEmpty(_fetchCfg[p].symbols) )            // not dedicated rule
                continue;

            // this provider has already dedicated list for this market, so just add to that
            List<string> symbols = _fetchCfg[p].symbols.Split(',').ToList();
            symbols.Add(symbol);
            _fetchCfg[p] = _fetchCfg[p] with { symbols = string.Join(',', symbols) };
            EventNewUnsavedContent?.Invoke(this, _componentName);
            return;
        }

        // Needs new dedicated rule entry
        List<ProvFetchCfg> cfg = _fetchCfg.ToList();
        cfg.Add(new ProvFetchCfg(market, symbol, [providerId]));
        _fetchCfg = cfg.ToArray();
        EventNewUnsavedContent?.Invoke(this, _componentName);
    }

    // Returns all those market's that this provider is set as one of default fetch providers
    public MarketId[] GetMarketsPerRulesForProvider(ExtProviderId providerId)                       // IPfsFetchConfig
    {
        List<MarketId> ret = new();

        foreach (ProvFetchCfg cfg in _fetchCfg)
        {
            if (cfg.providers.Contains(providerId) == false || string.IsNullOrEmpty(cfg.symbols) == false)
                continue;

            ret.Add(cfg.market);
        }
        return ret.Distinct().ToArray();
    }

    public ExtProviderId[] GetUsedProvForStock(MarketId market, string symbol)                      // IPfsFetchConfig
    {   // Mainly UI purposes to show under tracking view what providers may get used to fetch data
        ExtProviderId dedicated = GetDedicatedProviderForSymbol(market, symbol);

        if (dedicated != ExtProviderId.Unknown)
            return [dedicated];

        // Get all those rules that effect to this market, and are pure market rules wo dedicated symbols
        List<ProvFetchCfg> marketCfgs = GetFetchCfg().Where(c => c.market == market && string.IsNullOrEmpty(c.symbols)).ToList();

        // Only takes actives, not enforcing cross checking on settings but here limit off non-actives
        List<ExtProviderId> ret = new();

        foreach ( ExtProviderId prov in _provConfigs.GetActiveProviders())
        {
            if (marketCfgs.Any(c => c.providers.Contains(prov)))
                ret.Add(prov);
        }
        return ret.ToArray();
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
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

    public void OnSaveStorage() { BackupToStorage(); }

    public string CreateBackup()
    {
        return ExportXml();
    }
    
    public string CreatePartialBackup(List<string> symbols)
    {
        return string.Empty;
    }

    public List<string> RestoreBackup(string content)
    {
        return ImportXml(content);
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, ExportXml());
    }

    public string GetCmdPrefixes() { return _componentName; }                                       // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                                          // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                {
                    int i = 0;
                    StringBuilder sb = new();
                    sb.AppendLine("Provider Fetch Configs:");
                    foreach (ProvFetchCfg rule in _fetchCfg)
                    {
                        sb.AppendLine($">{i} {rule.market} for [{rule.symbols}] use [{string.Join(',', rule.providers)}]");
                        i++;
                    }

                    sb.AppendLine("Rate Fetch Configs:");

                    if ( _ratesCfg.provider != ExtProviderId.Unknown )
                        sb.AppendLine($">prov: {_ratesCfg.provider}");

                    return new OkResult<string>(sb.ToString());
                }
        }
        throw new NotImplementedException($"FetchConfig.CmdAsync missing {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                                       // ICmdHandler
    {
        await Task.CompletedTask;

        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }

    protected string ExportXml()
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement fetchElem = new XElement("Fetch");
        rootPFS.Add(fetchElem);

        foreach (ProvFetchCfg fr in _fetchCfg)
        {
            XElement frElem = new XElement(fr.market.ToString());
            frElem.SetAttributeValue("Providers", string.Join(',', fr.providers));
            
            if ( string.IsNullOrWhiteSpace(fr.symbols) == false )
                frElem.SetAttributeValue("Stocks", fr.symbols);

            fetchElem.Add(frElem);
        }

        XElement ratesElem = new XElement("Rates");
        ratesElem.SetAttributeValue("Provider", _ratesCfg.provider.ToString());
        rootPFS.Add(ratesElem);

        return rootPFS.ToString();
    }

    protected List<string> ImportXml(string xml)
    {
        List<string> warnings = new();
        List<ProvFetchCfg> fetchRules = new();
        RatesFetchCfg ratesFetchCfg = new RatesFetchCfg(ExtProviderId.Unknown);
        try
        {
            XDocument xmlDoc = XDocument.Parse(xml);
            XElement rootPFS = xmlDoc.Element("PFS");

            XElement fetchTopElem = rootPFS.Element("Fetch");
            if (fetchTopElem != null && fetchTopElem.HasElements)
            {
                foreach (XElement frElem in fetchTopElem.Elements())
                {
                    string marketName = string.Empty;

                    try
                    {
                        marketName = frElem.Name.ToString();

                        MarketId marketId = (MarketId)Enum.Parse(typeof(MarketId), marketName);
                        List<ExtProviderId> providers = new();
                        foreach (string prov in ((string)frElem.Attribute("Providers")).Split(','))
                            providers.Add((ExtProviderId)Enum.Parse(typeof(ExtProviderId), prov));

                        if (frElem.Attribute("Stocks") != null)
                            fetchRules.Add(new ProvFetchCfg(marketId, (string)frElem.Attribute("Stocks"), providers.ToArray()));
                        else
                            fetchRules.Add(new ProvFetchCfg(marketId, string.Empty, providers.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        string wrnmsg = $"{_componentName}, failed to load fetch rule for {marketName} w exception [{ex.Message}]";
                        warnings.Add(wrnmsg);
                        Log.Warning(wrnmsg);
                    }
                }
            }

            ExtProviderId ratesProvider = ExtProviderId.Unknown;

            XElement ratesElem = rootPFS.Element("Rates");

            if (ratesElem != null)
            {
                ratesProvider = (ExtProviderId)Enum.Parse(typeof(ExtProviderId), (string)ratesElem.Attribute("Provider"));
                ratesFetchCfg = new RatesFetchCfg(ratesProvider);
            }
        }
        catch (Exception ex) {
            string wrnmsg = $"{_componentName}, failed to load existing configs w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }

        _ratesCfg = ratesFetchCfg;
        _fetchCfg = fetchRules.ToArray();
        return warnings;
    }
}
