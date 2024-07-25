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

public class RepGenExpSales
{
    /* This report is to contain all information from sale of partial/full holding. Main target is yearly tax etc type verification / reports.
        * Because of this focus doesnt NOT include any divident information, but focuses just tell as well as possible holdings purhace situation,
        * and sale situation with straight sale profits on both Home/Market currencies. Nothing fancy calculation is need here, nor welcomed!
        * If wants to look how wonderfull investor you are there is reports for that under UI, like ReportTrades w % details and dividents included!
        * 
        * !!!THINK!!! could even add colum for conversion rate from market currency -> home currency, as may help tax checkings
        */

    static public List<RepDataExpSales> GenerateReport(
                                IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData)
    {
        List<RepDataExpSales> ret = new();

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return null;

        foreach (RCStock stock in reportStocks)
        {
            if (stock.Trades == null || stock.Trades.Count == 0)
                continue;

            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            foreach (RCTrade trade in stock.Trades)
            {
                RepDataExpSales data = new()
                {
                    StockMeta = sm,
                    SectorDef = stalkerData.GetStockSectors(stock.Stock.SRef)[0],
                    Holding = new RCHolding(trade.ST, trade.PfName)
                };

                ret.Add(data);
            }
        }
        return ret;
    }
}
