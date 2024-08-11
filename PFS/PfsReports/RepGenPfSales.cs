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

using Pfs.Data.Stalker;
using Pfs.Types;

namespace Pfs.Reports;

public class RepGenPfSales
{
    static public List<RepDataPfSales> GenerateReport(IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData)
    {
        Dictionary<string, RepDataPfSales> ret = new(); // Create as TradeId key'd to simplify merges.. but only return values on end
        RepDataInvestedHeader header = new();

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return null;

        // Note! TradeId can be over many holdings, but always unique... and all holdings of trade always under of PF

        foreach (RCStock stock in reportStocks)
        {
            if (stock.Trades.Count() == 0)
                continue;

            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            foreach (RCTrade rcTrade in stock.Trades)
            {
                RepDataPfSales retTrade;

                if (ret.ContainsKey(rcTrade.ST.Sold.TradeId))
                    retTrade = ret[rcTrade.ST.Sold.TradeId];
                else
                {
                    retTrade = new();

                    retTrade.StockMeta = sm;
                    retTrade.TradeId = rcTrade.ST.Sold.TradeId;
                    retTrade.TradeNote = rcTrade.ST.Sold.TradeNote;
                    retTrade.SaleDate = rcTrade.ST.Sold.SaleDate;
                    retTrade.McSoldUnitPriceWithFee = rcTrade.ST.Sold.McPriceWithFeePerUnit;
                    retTrade.HcSoldUnitPriceWithFee = rcTrade.ST.Sold.HcPriceWithFeePerUnit;

                    ret.Add(rcTrade.ST.Sold.TradeId, retTrade);
                };

                // Add this rcTrade to potentially over existing sold holdings under this TradeId

                ReportTradeHoldings sub = new ReportTradeHoldings()
                {
                    Holding = rcTrade.ST,
                    Growth = new RCGrowth(rcTrade.ST.Units, rcTrade.ST.McInvested, rcTrade.ST.HcInvested, rcTrade.ST.Sold.McPriceWithFeePerUnit, rcTrade.ST.Sold.HcPriceWithFeePerUnit),
                    Divident = rcTrade.ST.AnyDividents() ? new RRTotalDivident(rcTrade.ST) : null,
                };

                retTrade.Holdings.Add(sub);


                retTrade.SoldTotalUnits += rcTrade.ST.Units;
            }
        }

        // As trades can be any order, need to do one loop first above to process them to Dictionary, and here 
        // second loop to calculate some totals over tradeId

        foreach (KeyValuePair<string, RepDataPfSales> kvp in ret)
        {
            decimal hcInv = kvp.Value.Holdings.Select(h => h.Growth.HcInvested).Sum();
            decimal mcInv = kvp.Value.Holdings.Select(h => h.Growth.McInvested).Sum();

            kvp.Value.TotalGrowth = new RCGrowth(kvp.Value.SoldTotalUnits, mcInv, hcInv, kvp.Value.McSoldUnitPriceWithFee, kvp.Value.HcSoldUnitPriceWithFee);
            List<RRDivident> divList = kvp.Value.Holdings.Where(h => h.Divident != null).Select(h => h.Divident).ToList();

            if ( divList.Count > 0)
                kvp.Value.TotalDivident = new RRTotalDivident(divList);
        }

        return ret.Values.OrderByDescending(s => s.SaleDate).ToList();
    }
}
