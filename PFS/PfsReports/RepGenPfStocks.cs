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

using Pfs.Shared.Stalker;
using Pfs.Types;

namespace Pfs.Reports;

// Main view under Portfolio's is to list all stocks that user has added specific portfolio 
public class RepGenPfStocks
{
    static public List<RepDataPfStocks> GenerateReport(IReportFilters reportParams, IReportPreCalc collector, StalkerData stalkerData)
    {
        List<RepDataPfStocks> ret = new();

        foreach ( RCStock stock in collector.GetStocks(reportParams, stalkerData) )
        {
            if (stock.RCEod == null)
            {   // On this report, if doesnt have EOD for stock we just show its information and nothing more...

                ret.Add(new()
                {
                    StockMeta = stock.StockMeta,
                    FailedMsg = stock.StockMeta?.marketId == MarketId.CLOSED 
                                ? "Stock Closed (see history)"
                                : "No market data! (RCEod==null)"
                });
                continue;
            }

            RepDataPfStocks entry = new()
            {
                StockMeta = stock.StockMeta,
                RCEod = stock.RCEod,
                RRTotalHold = stock.RCTotalHold,
                Order = stock.Orders.FirstOrDefault()?.SO,
                HasTrades = stock.Trades.FirstOrDefault() != null,
            };

            if (stock.Stock.Alarms.Count > 0)
                entry.RRAlarm = new RRAlarm(stock.RCEod, stock.Stock.Alarms);

            ret.Add(entry);
        }
        return ret.OrderBy(f => string.IsNullOrWhiteSpace(f.FailedMsg) == false).ThenBy(s => s.StockMeta.name).ToList();
    }
}
