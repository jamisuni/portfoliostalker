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
using Pfs.Shared.Stalker;
using Pfs.Types;

namespace Pfs.Reports;

// This is very basic main level report that shows all stock meta user is tracking and stocks high level usage information
public class RepGenTracking
{
    static public List<RepDataTracking> GenerateReport(StalkerData stalkerData, IMarketMeta marketMetaProv, IStockMeta stockMetaProv, ILatestEod latestEodProv, IPfsFetchConfig fetchConfig)
    {
        Dictionary<string, RepDataTracking> report = new();

        List<MarketId> activeMarkets = marketMetaProv.GetActives().Select(m => m.ID).ToList();

        foreach ( StockMeta sm in stockMetaProv.GetAll())
            // Lets start by adding all known stock's per StockMeta to report (should contain all stocks)
            LocalAddNewEntry(sm);

        // Loop all Portfolio's Holdings & Trades -> track names of PFs those own(ed) this stock

        foreach ( SPortfolio myPf in stalkerData.Portfolios())
        {
            foreach (string sRef in myPf.SRefs)
            {
                RepDataTracking sd;

                if (report.ContainsKey(sRef))
                    sd = report[sRef];
                else
                    sd = LocalAddNewEntry(stockMetaProv.AddUnknown(sRef));

                if (sd.AnyPfTracking.Contains(myPf.Name) == false)
                    sd.AnyPfTracking.Add(myPf.Name);
            }

            foreach (SHolding holding in myPf.StockHoldings )
            {
                RepDataTracking sd;

                if (report.ContainsKey(holding.SRef))
                    sd = report[holding.SRef];
                else
                    sd = LocalAddNewEntry(stockMetaProv.AddUnknown(holding.SRef));

                if ( sd.AnyPfHoldings.Contains(myPf.Name) == false)
                    sd.AnyPfHoldings.Add(myPf.Name);
            }

            foreach (SHolding trade in myPf.StockTrades)
            {
                RepDataTracking sd;

                if (report.ContainsKey(trade.SRef))
                    sd = report[trade.SRef];
                else
                    sd = LocalAddNewEntry(stockMetaProv.AddUnknown(trade.SRef));

                if (sd.AnyPfTrades.Contains(myPf.Name) == false)
                    sd.AnyPfTrades.Add(myPf.Name);
            }
        }

        return report.Values.OrderBy(o => o.Stock.marketId == MarketId.CLOSED).ThenBy(s => s.Stock.name).ToList();

        RepDataTracking LocalAddNewEntry(StockMeta sm)
        {
            var entry = new RepDataTracking()
            {
                Stock = sm,
                IsMarketActive = activeMarkets.Contains(sm.marketId),
            };

            if (entry.IsMarketActive)
            {
                var marketClosing = marketMetaProv.LastClosing(sm.marketId);
                FullEOD fullEod = latestEodProv.GetFullEOD(sm.marketId, sm.symbol);

                if (fullEod != null)
                    entry.RCEod = new(fullEod, sm.marketCurrency, 1/*LastConversionRate==not used this report*/, marketClosing.localDate);

                ExtProviderId[] temp = fetchConfig.GetUsedProvForStock(sm.marketId, sm.symbol);

                if (temp != null && temp.Count() > 0)
                    entry.FetchProvider = temp;
            }

            report.Add(sm.GetSRef(), entry);

            return entry;
        }
    }
}
