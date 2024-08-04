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

using Pfs.Shared;
using Pfs.Shared.Stalker;
using Pfs.Types;

namespace Pfs.Reports;

public class RepGenStMgHistory
{
    // Specialised for StockMgmt, so single stock on time, to show all its current holdings => No need for filters nor collectors
    public static Result<List<RepDataStMgHistory>> GenerateReport(string sRef, StalkerData stalkerData, IStockMeta stockMetaProv, ILatestEod latestEodProv, 
                                                                  ILatestRates latestRatesProv, StoreStockMetaHist storeStockMetaHist)
    {
        List<RepDataStMgHistory> report = new();

        StockMeta stockMeta = stockMetaProv.Get(sRef);
        FullEOD fullEod = latestEodProv.GetFullEOD(sRef);
        decimal latestConversionRate = latestRatesProv.GetLatest(stockMeta.marketCurrency);

        if (stockMeta == null)
            return new FailResult<List<RepDataStMgHistory>>($"{sRef} failed to find stock.");

        RCEod rcEod = null;
        if (fullEod != null && latestConversionRate > 0)
            rcEod = new RCEod(fullEod, stockMeta.marketCurrency, latestConversionRate, fullEod.Date); // Note! Here using date of last available, not always latest

        RepDataStMgHistory total = RepDataStMgHistory.CreateTotal(rcEod);
        report.Add(total);

        foreach (SPortfolio myPf in stalkerData.Portfolios()) // So one buy could cause => One "Own" as still partially own, One "Buy" as some sold, Many "Sold" as could sell pieces
        {
            foreach (SHolding holding in myPf.StockHoldings) // Holding has only those we still fully/partially own
            {
                if ( holding.SRef != sRef) continue;

                RepDataStMgHistory own;

                if ( holding.Units < holding.OriginalUnits )    
                {   // Parts of this is sold already, so create entry for remaining "Own" and "Buy" for sold part
                    report.Add(own = RepDataStMgHistory.CreateOwnPerHolding(myPf.Name, holding, rcEod));
                    report.Add(RepDataStMgHistory.CreateBuyPerHolding(myPf.Name, holding, rcEod));
                }
                else// Fully "Own" still
                    report.Add(own = RepDataStMgHistory.CreateOwnPerHolding(myPf.Name, holding, rcEod));

                if (fullEod != null)
                    total.Total.AddOwn(own);
            }

            // Each sold piece has own "Sold" entry, but its also tracked back to original "Buy" 
            foreach ( SHolding trade in myPf.StockTrades )
            {
                if (trade.SRef != sRef) continue;

                // Reference of sold entry is added to original buy entry (thats if need as fully sold ones doesnt exist yet)
                RepDataStMgHistory buy = report.FirstOrDefault(b => b.Buy != null && b.Buy.Holding.PurhaceId == trade.PurhaceId);

                if ( buy == null)
                    report.Add(buy = RepDataStMgHistory.CreateBuyPerHolding(myPf.Name, trade, rcEod));

                buy.Buy.Sales.Add(trade);

                RepDataStMgHistory sold;

                // Report entry from each "Sold" items is shown 
                report.Add(sold = RepDataStMgHistory.CreateSoldPerTrade(myPf.Name, trade, rcEod));

                total.Total.AddSold(sold);
            }
        }

        foreach (StockMetaHist hist in storeStockMetaHist.GetHistory(sRef))
            // Brings in StoreStockMetaHist entries, so symbol/name changes, closings etc
            report.Add(RepDataStMgHistory.CreateHistory(hist));

        foreach (RepDataStMgHistory entry in report)
        {
            if ( entry.Buy != null )
            {
                List<RRDivident> allDivs = new();
                 
                foreach (SHolding sale in entry.Buy.Sales )
                    foreach ( var div in sale.Dividents ) 
                        allDivs.Add(new RRHoldingDivident(sale, div));

                if (allDivs.Count > 0 ) 
                    entry.TotalDivident = new RRTotalDivident(allDivs);
            }

            if ( entry.Sold != null )
            {
                if (entry.Sold.Holding.AnyDividents())
                    entry.TotalDivident = new RRTotalDivident(entry.Sold.Holding);
            }
        }

        return new OkResult<List<RepDataStMgHistory>>(report.OrderBy(r => r.Date).ToList());
    }
}
