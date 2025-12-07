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

public class RepGenWeight
{
    static public (RepDataWeightHeader header, List<RepDataWeight> stocks) GenerateReport(
           IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData, IStockNotes stockNotes,
           IPfsStatus pfsStatus)
    {
        List<RepDataWeight> ret = new();
        RepDataWeightHeader header = new();

        decimal hcTotalDiv = 0;

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return (null, null);

        int weightSector = Local_GetWeightSectorId();

        foreach (RCStock stock in collector.GetStocks(reportParams, stalkerData))
        {
            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            if (stock.Holdings == null || stock.Holdings.Count == 0)
                continue; // Only stocks user is currently invested on

            if (stock.RCEod == null)
                continue; // And stock needs to have EOD or its ignored

            RepDataWeight entry = new()
            {
                StockMeta = sm,
                RCEod = stock.RCEod,
                RCTotalHold = stock.RCTotalHold,
                SubHoldings = new(),
                NoteHeader = stockNotes.GetHeader(stock.Stock.SRef),
            };


            entry.TargetP = stalkerData.GetStockSectors(stock.Stock.SRef)[weightSector];

            decimal target = 2.0m;

            if (string.IsNullOrEmpty(entry.TargetP) == false && decimal.TryParse(entry.TargetP.Split('%')[0], out decimal tval))
                target = tval;

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
                RepDataWeightSub sub = new()
                {
                    RCHolding = rch,
                    RCTotalHold = new RCGrowth(rch.SH, stock.RCEod.fullEOD.Close, stock.RCEod.LatestConversionRate),
                };

                if (rch.SH.AnyDividents())
                    sub.RRHoldingsTotalDiv = new RRTotalDivident(rch.SH);

                entry.SubHoldings.Add(sub);
            }
        }

        // context.d.RCTotalHold.HcValuation

        if ( ret.Count == 0 )
            return (null, null);

        decimal totalOwning = header.HcIOwn = pfsStatus.GetAppCfg(AppCfgId.IOwn);

        if (totalOwning < 1000)
            totalOwning = header.HcTotalValuation;

        header.HcTotalDivident = new RRTotalDivident(hcTotalDiv, header.HcTotalInvested);

        header.HcGrowthP = (int)((header.HcTotalValuation - header.HcTotalInvested) / header.HcTotalInvested * 100);

        header.HcTotalGain = header.HcTotalValuation - header.HcTotalInvested + hcTotalDiv;
        if (header.HcTotalGain != 0)
            header.HcTotalGainP = (int)(header.HcTotalGain / header.HcTotalInvested * 100);

        foreach (RepDataWeight stock in ret )
        {
            stock.CurrentP = (stock.RCTotalHold.HcValuation / totalOwning) * 100;

            header.TotalCurrentP += stock.CurrentP;


            stock.HcInvestedOfTotalP = (stock.RCTotalHold.HcInvested / header.HcTotalInvested) * 100;
        }

        return (header, stocks: ret.OrderBy(s => s.StockMeta.name).ToList());


        int Local_GetWeightSectorId()
        {
            for (int i = 0; i < 3; i++ )
            {
                if (stalkerData.GetSector(i).sectorName == "Weight")
                    return i;
            }
            return 0;
        }
    }
}
