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
    static public (RepDataWeightHeader header, List<RepDataWeight> stocks) GenerateReport(DateOnly today, 
           IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData, IStockNotes stockNotes,
           IPfsStatus pfsStatus)
    {
        List<RepDataWeight> ret = new();
        RepDataWeightHeader header = new();

        decimal hcTotalValuation = 0;

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return (null, null);

        foreach (RCStock stock in collector.GetStocks(reportParams, stalkerData))
        {
            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            if (stock.Holdings == null || stock.Holdings.Count == 0)            // ??Maybe include old ones also if has weight set??
                continue; // Only stocks user is currently invested on

            if (stock.RCEod == null)
                continue; // And stock needs to have EOD or its ignored

            RepDataWeight entry = new()
            {
                StockMeta = sm,
                RCEod = stock.RCEod,
                RCTotalHold = stock.RCTotalHold,
                NoteHeader = stockNotes.GetHeader(stock.Stock.SRef),
            };

            if (stock.RCHoldingsTotalDivident != null)
                entry.RRTotalDivident = new(stock.RCHoldingsTotalDivident);

            ret.Add(entry);

            hcTotalValuation += stock.RCTotalHold.HcValuation;

            // Idea here is to count each E or $ how long its invested, then divided by total units gets avrg owning day
            long totalHcDays = 0;
            foreach (RCHolding holding in stock.Holdings)
                totalHcDays += (long)((today.DayNumber - holding.SH.PurhaceDate.DayNumber) * holding.SH.Units);

            if (totalHcDays > 0)
                entry.AvrgTimeAsMonths = (totalHcDays / stock.RCTotalHold.Units) / 30.437m;

            // Also keep track of historical sell gains/looses for the stock

            foreach (RCTrade rct in stock.Trades)
            {
                entry.HcTradeProfits += (rct.ST.Sold.HcPriceWithFeePerUnit -  rct.ST.HcPriceWithFeePerUnit) * rct.ST.Units;

                RepDataWeightTradeSub sub = new()
                {
                    RCTrade = rct,
                };

                sub.HcTradeProfit = rct.ST.HcSoldProfit;
                sub.HcTradeDividents = rct.ST.HcTotalDividents;

                entry.SubTrades.Add(sub);
            }

            entry.HcHistoryDivident = stock.HcTotalTradeDividents;

            // DropDown with row for each separate holding
            foreach (RCHolding rch in stock.Holdings)
            {
                RepDataWeightHoldingSub sub = new()
                {
                    RCHolding = rch,
                    RCTotalHold = new RCGrowth(rch.SH, stock.RCEod.fullEOD.Close, stock.RCEod.LatestConversionRate),
                    YearlyDivPForHcHolding = rch.YearlyDivPForHcHolding,
                };

                if (rch.SH.AnyDividents())
                    sub.RRHoldingsTotalDiv = new RRTotalDivident(rch.SH);

                entry.SubHoldings.Add(sub);
            }

            decimal totalTaken = entry.HcHistoryDivident +      // Dividents paid to already sold/traded positions
                                 entry.RRTotalDivident.HcDiv +  // Dividents of current holdings
                                 entry.HcTradeProfits;          // Trade profits from already sold positions

            if (totalTaken != 0 && stock.RCTotalHold.HcValuation > 0)
                entry.HcTakenAgainstVal = totalTaken / stock.RCTotalHold.HcValuation;

            if (totalTaken != 0 && stock.RCTotalHold.HcInvested > 0)
                entry.HcTakenAgainstInv = totalTaken / stock.RCTotalHold.HcInvested;

            entry.YearlyDivPForHcHolding = stock.YearlyDivPForHcHolding;
        }

        if ( ret.Count == 0 )
            return (null, null);

        int weightSector = Local_GetWeightSectorId();

        decimal totalOwning = pfsStatus.GetAppCfg(AppCfgId.IOwn);

        if (totalOwning < 1000)
            totalOwning = hcTotalValuation;

        foreach (RepDataWeight stock in ret )
        {   // Current weight % for stock
            stock.CurrentP = (stock.RCTotalHold.HcValuation / totalOwning) * 100;
            header.TotalCurrentP += stock.CurrentP;

            // Target weight % for stock
            stock.TargetP = stalkerData.GetStockSectors(stock.StockMeta.GetSRef())[weightSector];

            if (string.IsNullOrEmpty(stock.TargetP) == false && decimal.TryParse(stock.TargetP.Split('%')[0], out decimal tval))
                header.TotalPlannedP += tval;
            else // if target not set, assume current weight as target
                header.TotalPlannedP += stock.CurrentP;

            foreach ( RepDataWeightHoldingSub sub in stock.SubHoldings)
            {
                sub.CurrentP = (sub.RCTotalHold.HcValuation / totalOwning) * 100;
            }
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
