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
using Pfs.Types;

namespace Pfs.ExtFetch;

internal class FetchEodPending
{
    /* This is place where stocks are waiting their turn to be fetched latest EOD. 
     * Stock is to be removed from this list as soon free provider able to start processing request.
     * 
     * If specific symbol to be fetched has manually defined dedicated fetch cfg rule that identifies
     * that symbol, then its seen as priority and must be fetched with provider defined on that rule. 
     * These priority symbols are added to own '_priority' list to wait specific provider.
     *
     * Rest of symbols go to market oriented general list of pending symbols those can be 
     * processed with what ever provider is available w general rule defined for that market.
     * 
     * In case fetch is failing (example getting old data for stock) a failed provider gets 
     * blocked off for that stock (for full uptime of application). This means that retry 
     * attempt is done w some other provider if any available. This effects also to priority ones.
     */
    private IPfsFetchConfig _fetchConfig;

    private Dictionary<ExtProviderId, List<string>> _priority;  // per provider a list of dedicated priority SRef's pending

    private Dictionary<MarketId, List<string>> _pending; // per market list of symbols pending for any provider

    private List<ExtProviderId> _noJobsLeft = new(); // <= "black list" of providers wo jobs left (only for optimization)

    private List<string> _cantFindProviderSRefs = new(); // (cleared per fetch) Contains those SRefs could not find provider etc, so ones didnt even get change to be fetched

    private Dictionary<ExtProviderId, List<string>> _uptimeBlockRetrySRefs = new(); // (never cleaned) So if fetch fails, same provider never reused that SRef on uptime
                                                                                    
    public FetchEodPending(IPfsFetchConfig fetchConfig)
    {
        _fetchConfig = fetchConfig;

        _pending = new();
        foreach ( MarketId marketId in Enum.GetValues(typeof(MarketId)))
        {
            if ( marketId.IsReal() == false)
                continue;

            _pending.Add(marketId, new List<string>());
        }

        _priority = new();
        foreach ( ExtProviderId provId in Enum.GetValues(typeof(ExtProviderId)))
        {
            if (provId.SupportsStocks() == false)
                continue;

            _priority.Add(provId, new());
            _uptimeBlockRetrySRefs.Add(provId, new());
        }
    }

    public void AddToPending(MarketId marketId, string symbols, ExtProviderId enforceProvider = ExtProviderId.Unknown)
    {
        _noJobsLeft = new(); // clear blacklist

        foreach ( string symbol in symbols.Split(','))
        {
            if ( enforceProvider != ExtProviderId.Unknown )
            {   // Allows fetch to enforce fetching to specific provider (tracking report)
                _priority[enforceProvider].Add($"{marketId}${symbol}");
                continue;
            }

            // See if there is any dedicated rules for this symbol
            ExtProviderId provId = _fetchConfig.GetDedicatedProviderForSymbol(marketId, symbol);
            if (provId != ExtProviderId.Unknown)
            {
                _priority[provId].Add($"{marketId}${symbol}");
                continue;
            }

            // If not then we just add it to pending under market
            _pending[marketId].Add(symbol);
        }
    }

    public int TotalPending()
    {
        return TotalPriorityPending() + _pending.Values.SelectMany(list => list).Count();
    }

    public int TotalPriorityPending()
    {
        return _priority.Values.SelectMany(l => l).Count();
    }

    public List<string> GetCantFindProviderSRefs() // For this request time...
    {
        return _cantFindProviderSRefs;
    }

    public void SetRestToFailedSRefs()
    {   // Sadly this cant be noticed inside, so owner tells when no rules left for rest
        foreach (KeyValuePair<MarketId, List<string>> kvp in _pending)
        {
            if (kvp.Value.Count == 0)
                continue;

            _cantFindProviderSRefs.AddRange(kvp.Value.Select(value => $"{kvp.Key}${value}").ToList());
        }

        foreach (KeyValuePair<ExtProviderId, List<string>> kvp in _priority)
            if (kvp.Value.Count > 0)
                _cantFindProviderSRefs.AddRange(kvp.Value);

        ClearAllPendings();
    }

    public Dictionary<MarketId, int> GetMarketPendingStats()
    {
        Dictionary<MarketId, int> ret = new();

        foreach (KeyValuePair<MarketId, List<string>> kvp in _pending)
        {
            if ( kvp.Value.Count > 0 ) 
                ret.Add(kvp.Key, kvp.Value.Count);
        }
        return ret;
    }

    public void ClearAllPendings()
    {
        foreach (KeyValuePair<MarketId, List<string>> kvp in _pending)
            if (kvp.Value.Count > 0)
                kvp.Value.Clear();

        foreach (KeyValuePair<ExtProviderId, List<string>> kvp in _priority)
            if (kvp.Value.Count > 0)
                kvp.Value.Clear();

        _cantFindProviderSRefs.Clear();
    }

    public void ClearFailed()
    {
        _cantFindProviderSRefs.Clear();
    }

    public (MarketId market, List<string> symbols) GetPending(ExtProviderId provider, int maxRet, MarketId[] markets)
    {
        if ( _noJobsLeft.Contains(provider) )
            return (MarketId.Unknown, null);

        if (_priority[provider].Count > 0 )
        {
            for (int pos = _priority[provider].Count - 1; pos >= 0; pos--)
            {
                var stock = StockMeta.ParseSRef(_priority[provider][pos]);

                if (markets.Contains(stock.marketId) == false)
                    continue;

                if (_uptimeBlockRetrySRefs[provider].Contains(_priority[provider][pos]))
                    continue;

                _priority[provider].RemoveAt(pos);
                return (stock.marketId, [stock.symbol]);
            }

            // if didnt find anything then all this providers priorities are dead/attempted already -> move normal pending
            foreach (string prioSRef in _priority[provider])
            {
                var stock = StockMeta.ParseSRef(prioSRef);
                _pending[stock.marketId].Add(stock.symbol);
            }
            _priority[provider].Clear();
        }

        //  _pending

        MarketId[] rulesForMarkets = _fetchConfig.GetMarketsPerRulesForProvider(provider);

        foreach ( KeyValuePair<MarketId, List<string>> kvp in _pending)
        {
            if (kvp.Value.Count == 0 || markets.Contains(kvp.Key) == false || rulesForMarkets.Contains(kvp.Key) == false)
                continue;

            List<string> ret = new();

            foreach (string symbol in kvp.Value)
            {
                if (_uptimeBlockRetrySRefs[provider].Contains($"{kvp.Key}${symbol}"))
                    continue;

                ret.Add(symbol);

                if (ret.Count >= maxRet)
                    break;
            }

            if (ret.Count == 0)
                continue;

            foreach (string symbol in ret) 
                kvp.Value.Remove(symbol);

            return (kvp.Key, ret);
        }

        _noJobsLeft.Add(provider);

        return (MarketId.Unknown, null);
    }

    public void FetchFailedBy(ExtProviderId provider, MarketId marketId, string symbol)
    {
        /* So pending gets info from fetch side if one of providers failed to fetch symbol w specific provider.
         * To allow more fluent retry fetch pressing, by enforcing retrys for different provider as we never 
         * allow refetch attemp w already failed provider. This is uptime preventation.
         */
        _uptimeBlockRetrySRefs[provider].Add($"{marketId}${symbol}");
    }
}
