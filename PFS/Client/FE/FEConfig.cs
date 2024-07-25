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

using Pfs.Config;
using Pfs.ExtFetch;
using Pfs.Shared;
using Pfs.Types;

namespace Pfs.Client;

public class FEConfig : IFEConfig
{
    protected IPfsPlatform _pfsPlatform;
    protected IPfsStatus _pfsStatus;
    protected ClientData _clientData;
    protected ClientStalker _clientStalker;
    protected IMarketMeta _marketMetaProv;
    protected IStockMeta _stockMetaProv;
    protected ILatestEod _latestEodProv;
    protected IFetchEod _fetchEod;
    protected IFetchRates _fetchRates;
    protected FetchConfig _fetchConfig;
    protected ILatestRates _latestRatesProv;
    protected StoreUserEvents _storeUserEvents;
    protected IPfsSetMarketConfig _marketConfig;
    protected StoreNotes _stockNotes;
    protected ProvConfig _provConfig;

    public FEConfig(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, ClientData clientData, ClientStalker clientStalker, IMarketMeta marketMetaProv, IStockMeta stockMetaProv, ILatestEod latestEodProv, 
                     IFetchEod fetchEod, IFetchRates fetchRates, FetchConfig fetchConfig, ILatestRates latestRatesProv, StoreUserEvents storeUserEvents, IPfsSetMarketConfig marketConfig,
                     StoreNotes stockNotes, ProvConfig provConfig)
    {
        _pfsPlatform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _clientData = clientData;
        _clientStalker = clientStalker;
        _marketMetaProv = marketMetaProv;
        _stockMetaProv = stockMetaProv;
        _latestEodProv = latestEodProv;
        _fetchEod = fetchEod;
        _fetchRates = fetchRates;
        _fetchConfig = fetchConfig;
        _latestRatesProv = latestRatesProv;
        _storeUserEvents = storeUserEvents;
        _marketConfig = marketConfig;
        _stockNotes = stockNotes;
        _provConfig = provConfig;
    }

    public CurrencyId HomeCurrency { 
        get { return _latestRatesProv.HomeCurrency; } 
        set { if (_latestRatesProv.HomeCurrency == CurrencyId.Unknown)
                _latestRatesProv.HomeCurrency = value;
            }
    }

    public MarketCfg GetMarketCfg(MarketId marketId)
    {
        return _marketConfig.GetCfg(marketId);
    }

    public bool SetMarketCfg(MarketId marketId, MarketCfg marketCfg)
    {
        if ( marketCfg.Active == false )
        {   // Later? Cant deactivate market that user has stocks on?
        }
        return _marketConfig.SetCfg(marketId, marketCfg);
    }

    public Dictionary<ExtProviderId, string> GetProvPrivKeys()
    {
        Dictionary<ExtProviderId, string> ret = new();

        foreach (ExtProviderId provId in Enum.GetValues(typeof(ExtProviderId)))
        {
            if (provId == ExtProviderId.Unknown)
                continue;

            ret.Add(provId, _provConfig.GetPrivateKey(provId));
        }
        return ret;
    }

    public void SetProvPrivKey(ExtProviderId provId, string privKey)
    {
        _provConfig.SetPrivateKey(provId, privKey);
    }

    public ExtProviderId GetActiveRatesProvider()
    {
        RatesFetchCfg cfg = _fetchConfig.GetRatesCfg();

        return cfg.provider;
    }

    public Result SetActiveRatesProvider(ExtProviderId provId)
    {
        RatesFetchCfg updCfg = new RatesFetchCfg(provId);

        return _fetchConfig.SetRatesCfg(updCfg);
    }

    public ExtProviderId[] GetAvailableRatesProviders()
    {
        return _fetchConfig.GetAvailableRatesProviders();
    }

    public IEnumerable<ExtProviderId> GetActiveEodProviders(MarketId marketId)
    {
        return _fetchEod.GetActiveEodProviders(marketId);
    }


    public IReadOnlyCollection<ProvFetchCfg> GetEodFetchCfg()
    {
        return _fetchConfig.GetFetchCfg();
    }

    public void SetEodFetchCfg(ProvFetchCfg[] allCfgs)
    {   // As this is sole place to set fetch configs now, lets enforce here correctness of symbol list

        List<ProvFetchCfg> use = new(); // make own copy from all fields, just in case
        foreach (ProvFetchCfg cfg in allCfgs)
            use.Add(new ProvFetchCfg(cfg.market, Local_GetTrimmedSymbols(cfg.market, cfg.symbols), (ExtProviderId[])cfg.providers.Clone()));

        _fetchConfig.SetFetchCfg(use.ToArray());
        return;

        string Local_GetTrimmedSymbols(MarketId market, string symbols)
        {
            List<string> ret = new();

            if (string.IsNullOrWhiteSpace(symbols) == true)
                return null;

            foreach (string symbol in symbols.Split(',').Order())
            {
                string temp = symbol.Trim().ToUpper();

                if (string.IsNullOrWhiteSpace(temp))
                    continue;

                // No need any fancy format validations as making sure user has it meta is way enough
                if (_stockMetaProv.Get(market, symbol) == null)
                    continue;
                    
                ret.Add(temp);
            }
            string res = string.Join(',', ret);

            if (string.IsNullOrWhiteSpace(res) == true)
                return null;

            return string.Join(',', ret);
        }
    }
}
