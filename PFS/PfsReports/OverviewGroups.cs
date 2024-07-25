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

public class ReportOverviewGroups
{
    public static Result<List<OverviewGroupsData>> GenerateReport(IReportFilters reportParams, IReportPreCalc collector, StalkerData stalkerData, ILatestRates ratesProv)
    {
        List<OverviewGroupsData> ret = new();

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData).Where(s => s.RCEod != null);

        // Start by creating default groups

        OverviewGroupsData alarmsAll = new()
        {
            Name = "All Alarms",
        };
        ret.Add(alarmsAll);
        OverviewGroupsData stockHoldings = new()
        {
            Name = "Investments",
        };
        ret.Add(stockHoldings);
        OverviewGroupsData stockOldies = new()
        {
            Name = "Oldies",
        };
        ret.Add(stockOldies);
        OverviewGroupsData stockOthers = new()
        {
            Name = "Tracking's",
        };
        ret.Add(stockOthers);

        foreach (RCStock stock in reportStocks)
        {
            if ( stalkerData.StockAlarms(stock.Stock.SRef).Count() > 0 )
                Local_AddStock(alarmsAll, stock);

            if (stock.Holdings.Count > 0)
                Local_AddStock(stockHoldings, stock);
            else if (stock.Holdings.Count == 0 && stock.Trades.Count > 0)
                Local_AddStock(stockOldies, stock);
            else
                Local_AddStock(stockOthers, stock);
        }

        // Then add each portfolio separately

        foreach (SPortfolio portfolio in stalkerData.Portfolios())
        {
            OverviewGroupsData pfGroup = new()
            {
                Name = $"PF: {portfolio.Name}",
                LimitSinglePf = portfolio.Name, // effects to what order is shown
                SRefs = portfolio.SRefs.Distinct().ToList(), // <= creates duplicate list but not items
            };                                               // as below has adds so that doesnt mess stalker

            foreach (RCStock stock in reportStocks)
            {
                if (portfolio.SRefs.Contains(stock.StockMeta.GetSRef()) == false)
                    continue;
                
                pfGroup.SRefs.Add(stock.StockMeta.GetSRef());

                foreach ( RCHolding holding in stock.Holdings.Where(h => h.PfName == portfolio.Name) )
                {
                    pfGroup.HcTotalInvested += holding.SH.HcInvested;
                    pfGroup.HcTotalValuation += holding.SH.Units * stock.RCEod.HcClose;
                    // Note! GetLatest cant fail or RCEod would not be here
                    pfGroup.HcPrevValuation += holding.SH.Units * stock.RCEod.fullEOD.PrevClose * ratesProv.GetLatest(stock.StockMeta.marketCurrency);
                }
            }
            ret.Add(pfGroup);
        }

        // Finally calculate some total valuations

        foreach (OverviewGroupsData gd in ret)
        {
            gd.HcTotalInvested = decimal.Round(gd.HcTotalInvested, 0);
            gd.HcTotalValuation = decimal.Round(gd.HcTotalValuation, 0);
            gd.HcPrevValuation = decimal.Round(gd.HcPrevValuation, 0);
            if (gd.HcTotalValuation > 0 && gd.HcTotalInvested > 0)
                gd.HcGrowthP = decimal.Round((gd.HcTotalValuation - gd.HcTotalInvested) / gd.HcTotalInvested * 100, 1);

            if (gd.HcPrevValuation > 0)
                gd.HcPrevValP = decimal.Round((gd.HcTotalValuation - gd.HcPrevValuation) / gd.HcPrevValuation * 100, 1);
        }

        return new OkResult<List<OverviewGroupsData>>(ret);


        void Local_AddStock(OverviewGroupsData group, RCStock stock)
        {
            group.SRefs.Add(stock.StockMeta.GetSRef());

            if (stock.RCTotalHold != null)
            {
                group.HcTotalInvested += stock.RCTotalHold.HcInvested;
                group.HcTotalValuation += stock.RCTotalHold.HcValuation;

                foreach (RCHolding holding in stock.Holdings)
                    group.HcPrevValuation += holding.SH.Units * stock.RCEod.fullEOD.PrevClose * ratesProv.GetLatest(stock.StockMeta.marketCurrency);
            }
        }
    }
}
