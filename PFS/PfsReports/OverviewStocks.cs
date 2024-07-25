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

using Pfs.Types;
using Pfs.Shared.Stalker;

namespace Pfs.Reports;

public class ReportOverviewStocks
{
    public static List<OverviewStocksData> GenerateReport(IReportFilters reportParams, IReportPreCalc collector, IPfsStatus pfsStatus, StalkerData stalkerData, IStockMeta stockMetaProv, IMarketMeta marketMetaProv, IExtraColumns extraColumns)
    {
        List<OverviewStocksData> ret = new();

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        IEnumerable<MarketMeta> activeMarkets = marketMetaProv.GetActives();

        if (reportStocks.Count() == 0)
            return new();

        ExtraColumnId[] columnId = new ExtraColumnId[IExtraColumns.MaxCol];
        for (int c = 0; c < IExtraColumns.MaxCol; c++)
            columnId[c] = (ExtraColumnId)pfsStatus.GetAppCfg($"ExtraColumn{c}");

        foreach (RCStock stock in collector.GetStocks(reportParams, stalkerData))
        {
            if (stock.RCEod == null)
                continue; // these are, and stay as not included ones...

            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            if (activeMarkets.Any(m => m.ID == sm.marketId) == false)
                // Not including CLOSER or DISABLED markets
                continue;

            OverviewStocksData entry = new()
            {
                StockMeta = sm,
                RCEod = stock.RCEod,
                RCTotalHold = stock.RCTotalHold,
            };

            if (stock.Stock.Alarms.Count > 0)
                entry.RRAlarm = new RRAlarm(stock.RCEod, stock.Stock.Alarms);

            foreach (RCOrder order in stock.Orders)
            {
                entry.PfOrder.Add(order);

                if (entry.BestOrder == null || order.SO.TriggerDistP(stock.RCEod.fullEOD.Close) > entry.BestOrder.SO.TriggerDistP(stock.RCEod.fullEOD.Close))
                    entry.BestOrder = order;
            }

            for (int x = 0; x < IExtraColumns.MaxCol; x++)
            {
                if (columnId[x] == ExtraColumnId.Unknown)
                    // Need to pass these also, as way to inform UI that its unused column
                    entry.ExCol[x] = new RCExtraColumn(ExtraColumnId.Unknown);
                else
                {
                    entry.ExCol[x] = extraColumns.Get(x, stock.Stock.SRef);

                    if (entry.ExCol[x] == null)
                        // If specific stock doesnt have requested info we replace its null w empty to mark empty but keep columns proper type Id
                        entry.ExCol[x] = new RCExtraColumn(columnId[x]);
                }
            }
            ret.Add(entry);
        }
        return ret;
    }
}
