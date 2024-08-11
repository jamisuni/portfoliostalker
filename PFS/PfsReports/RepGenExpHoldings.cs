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

public class RepGenExpHoldings
{
    static public List<RepDataExpHoldings>  GenerateReport(
                                DateOnly today, IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData)
    {
        List<RepDataExpHoldings> ret = new();

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return null;

        foreach (RCStock stock in reportStocks)
        {
            if (stock.Holdings == null || stock.Holdings.Count == 0)
                continue; // Only stocks user is currently invested on

            if (stock.RCEod == null)
                continue; // And stock needs to have EOD or its ignored

            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            RepDataExpHoldings entry = new()
            {
                StockMeta = sm,
                RCEod = stock.RCEod,
                RCTotalHold = stock.RCTotalHold,
                SectorDef = stalkerData.GetStockSectors(stock.Stock.SRef)[0],
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

            // Idea here is to count each E or $ how long its invested, then divided by total units gets avrg owning day
            long totalHcDays = 0;
            foreach (RCHolding holding in stock.Holdings)
                totalHcDays += (long)((today.DayNumber - holding.SH.PurhaceDate.DayNumber) * holding.SH.Units);

            if (totalHcDays > 0)
                entry.AvrgTimeAsMonths = (totalHcDays / stock.RCTotalHold.Units) / 30.437m;

            ret.Add(entry);
        }

        if (ret.Count == 0)
            return null;

        return ret.OrderBy(s => s.StockMeta.name).ToList();
    }
}
