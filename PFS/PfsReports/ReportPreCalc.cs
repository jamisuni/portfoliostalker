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

using Pfs.Data;
using Pfs.Data.Stalker;
using Pfs.Types;

namespace Pfs.Reports;

// Pre-calculated stock oriented data that majority of reports use
public class ReportPreCalc : IReportPreCalc
{
    protected List<RCStock> _retStocks = new();

    public ReportPreCalc(string limitSinglePfName, IReportFilters reportFilters, IPfsPlatform pfsPlatform, ILatestEod latestEodProv, IStockMeta stockMetaProv, IMarketMeta marketMetaProv, ILatestRates latestRatesProv, StalkerData stalkerData)
    {
        _retStocks = new();

        List<ExpiredStocks.Expired> expired = ExpiredStocks.GetExpiredEods(pfsPlatform.GetCurrentUtcTime(), stockMetaProv, latestEodProv, marketMetaProv).expired; // can be cached later under expired!

        foreach (SPortfolio pf in stalkerData.Portfolios())
        {
            if (string.IsNullOrEmpty(limitSinglePfName) == false)
            {   // under portfolio reports define this to overwrite any filter PF selections, w just that PF
                if (pf.Name != limitSinglePfName)
                    continue;
            }
            else if (reportFilters.AllowPF(pf.Name) == false)
                continue;

            foreach (string sRef in pf.SRefs)
                // Enforces information that this portfolio refers to this stock
                GetOrAdd(sRef, pf.Name);

            foreach (SHolding holding in pf.StockHoldings)
            {
                RCStock rcs = GetOrAdd(holding.SRef, pf.Name);

                rcs.Holdings.Add(new RCHolding(holding, pf.Name));

                foreach (SHolding.Divident div in holding.Dividents)
                    rcs.AddHoldingDivident(holding.Units, div);
            }

            foreach (SHolding trade in pf.StockTrades)
            {
                RCStock rcs = GetOrAdd(trade.SRef, pf.Name);

                rcs.Trades.Add(new RCTrade(trade, pf.Name));

                foreach (SHolding.Divident div in trade.Dividents)
                    rcs.AddTradeDivident(trade.Units, div);
            }

            foreach (SOrder order in pf.StockOrders)
            {
                RCStock rcs = GetOrAdd(order.SRef, pf.Name);

                if (rcs.RCEod == null)
                    continue;

                RCOrder rcOrder = rcs.Orders.FirstOrDefault(o => o.PfName == pf.Name);

                if (rcOrder == null)
                {   // There is no existing order under this PF for this stock -> so add this
                    rcs.Orders.Add(new RCOrder(order, pf.Name));
                    continue;
                }

                if (rcOrder.SO.FillDate != null || rcOrder.SO.TriggerDistP(rcs.RCEod.fullEOD.Close) > order.TriggerDistP(rcs.RCEod.fullEOD.Close))
                    // Has already triggered order, or is closer  to trigger so we keep what we have...
                    continue;

                // Lets replace existing one w this closer one..
                rcOrder.SO = order;
            }
        }

        foreach (RCStock s in _retStocks)
        {   // After everything from Stalker is in, can start do some calculations
            s.RecalculateTotals();

            s.Holdings = s.Holdings.OrderByDescending(h => h.SH.PurhaceDate).ToList();    // newest purhace first
            s.Trades = s.Trades.OrderByDescending(t => t.ST.Sold.SaleDate).ToList();      // latest sale first
        }

        return;

        RCStock GetOrAdd(string sRef, string pfName)
        {
            RCStock rcs = _retStocks.FirstOrDefault(s => s.Stock.SRef == sRef);

            if (rcs != null)
                return rcs;

            StockMeta stockMeta = stockMetaProv.Get(sRef);

            if (stockMeta == null)
                stockMeta = stockMetaProv.AddUnknown(sRef);

            SStock sStock = stalkerData.StockRef(sRef);

            if ( sStock == null )
                sStock = new SStock(sRef);

            rcs = new(sStock, stockMeta);
            _retStocks.Add(rcs);

            if (string.IsNullOrWhiteSpace(pfName) == false && rcs.PFs.Contains(pfName) == false)
                rcs.PFs.Add(pfName);

            FullEOD fullEod = latestEodProv.GetFullEOD(rcs.StockMeta.marketId, rcs.StockMeta.symbol);

            if (fullEod != null)
            {   // As EOD is available then going to create RCEod that contains expiration info also
                ExpiredStocks.Expired exp = expired.FirstOrDefault(e => e.SRef == sStock.SRef);
                DateOnly lastMarketClosing;

                if (exp != null)
                    lastMarketClosing = exp.MarketLastLocalDate;
                else // If not expired then last market closing must be one on stock
                    lastMarketClosing = fullEod.Date;

                decimal latestConversionRate = latestRatesProv.GetLatest(rcs.StockMeta.marketCurrency);

                rcs.RCEod = new RCEod(fullEod, rcs.StockMeta.marketCurrency, latestConversionRate, lastMarketClosing);
            }
            return rcs;
        }
    }

    public IEnumerable<RCStock> GetStocks(IReportFilters reportFilters, StalkerData stalkerData)      // IReportCollector
    {
        foreach (RCStock s in _retStocks)
        {
            string[] sect = stalkerData.GetStockSectors(s.StockMeta.GetSRef());

            if (reportFilters.AllowSector(0, sect[0]) == false ||
                reportFilters.AllowSector(1, sect[1]) == false ||
                reportFilters.AllowSector(2, sect[2]) == false)
                // Enforcing sectors on here, so list itself has all stocks
                continue;

            if (reportFilters.AllowMarket(s.StockMeta.marketId) == false)
                continue;

            // Hmm... how to spin this opposite... 
            if (s.Holdings.Count > 0 && reportFilters.AllowOwning(ReportOwningFilter.Holding) ||
                s.Trades.Count > 0 && reportFilters.AllowOwning(ReportOwningFilter.Trade) ||
                s.Holdings.Count == 0 && s.Trades.Count == 0 && reportFilters.AllowOwning(ReportOwningFilter.Tracking))
                yield return s;
        }
    }
}
