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

public class RepGenInvested
{
    static public (RepDataInvestedHeader header, List<RepDataInvested> stocks) GenerateReport(
           IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData)
    {
        List<RepDataInvested> ret = new();
        RepDataInvestedHeader header = new();

        decimal hcTotalDiv = 0;

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return (null, null);

        foreach (RCStock stock in collector.GetStocks(reportParams, stalkerData))
        {
            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            if (stock.Holdings == null || stock.Holdings.Count == 0)
                continue; // Only stocks user is currently invested on

            if (stock.RCEod == null)
                continue; // And stock needs to have EOD or its ignored

            RepDataInvested entry = new()
            {
                StockMeta = sm,
                RCEod = stock.RCEod,
                RCTotalHold = stock.RCTotalHold,
                SubHoldings = new(),
            };

            if (stock.RCHoldingsTotalDivident != null)
                entry.RRTotalDivident = new(stock.RCHoldingsTotalDivident);

            entry.HcGain = 0;
            if (stock.RCTotalHold != null)
                entry.HcGain += stock.RCTotalHold.HcGrowthAmount;
            if (stock.RCHoldingsTotalDivident != null)
                entry.HcGain += stock.RCHoldingsTotalDivident.HcDiv;

            if (entry.HcGain != 0)
                entry.HcGainP = (int)(entry.HcGain / entry.RCTotalHold.HcInvested * 100);
            else
                entry.HcGainP = 0;

            ret.Add(entry);

            header.HcTotalInvested += stock.RCTotalHold.HcInvested;
            header.HcTotalValuation += stock.RCTotalHold.HcValuation;

            hcTotalDiv += stock.RCHoldingsTotalDivident.HcDiv;

            // DropDown with row for each separate holding
            foreach (RCHolding rch in stock.Holdings)
            {
                RepDataInvestedSub sub = new()
                {
                    RCHolding = rch,
                    RCTotalHold = new RCGrowth(rch.SH, stock.RCEod.fullEOD.Close, stock.RCEod.LatestConversionRate),
                };

                if (rch.SH.AnyDividents())
                    sub.RRHoldingsTotalDiv = new RRTotalDivident(rch.SH);

                entry.SubHoldings.Add(sub);
            }
        }

        if ( ret.Count == 0 )
            return (null, null);

        header.HcTotalDivident = new RRTotalDivident(hcTotalDiv, header.HcTotalInvested);

        header.HcGrowthP = (int)((header.HcTotalValuation - header.HcTotalInvested) / header.HcTotalInvested * 100);

        header.HcTotalGain = header.HcTotalValuation - header.HcTotalInvested + hcTotalDiv;
        if (header.HcTotalGain != 0)
            header.HcTotalGainP = (int)(header.HcTotalGain / header.HcTotalInvested * 100);

        foreach (RepDataInvested stock in ret )
        {   // Need to do second round to update some % etc valuations those depend from header's totals
            stock.HcInvestedOfTotalP = (stock.RCTotalHold.HcInvested / header.HcTotalInvested) * 100;
            stock.HcValuationOfTotalP = (stock.RCTotalHold.HcValuation / header.HcTotalValuation) * 100;
        }

        return (header, stocks: ret.OrderBy(s => s.StockMeta.name).ToList());
    }
}
