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

public class RepGenStMgHoldings
{
    // Specialised for StockMgmt, so single stock on time, to show all its current holdings => No need for filters nor collectors
    public static Result<List<RepDataStMgHoldings>> GenerateReport(string sRef, StalkerData stalkerData, IStockMeta stockMetaProv, ILatestEod latestEodProv, ILatestRates latestRatesProv)
    {
        List<RepDataStMgHoldings> report = new();

        StockMeta stockMeta = stockMetaProv.Get(sRef);
        FullEOD fullEod = latestEodProv.GetFullEOD(sRef);
        decimal latestConversionRate = latestRatesProv.GetLatest(stockMeta.marketCurrency);

        if (fullEod == null || latestConversionRate == 0)
            return new FailResult<List<RepDataStMgHoldings>>($"{sRef} no EOD available");

        RCEod rcEod = new RCEod(fullEod, stockMeta.marketCurrency, latestConversionRate, fullEod.Date); // Note! Here using date of last available, not always latest

        foreach (SPortfolio myPf in stalkerData.Portfolios())
        {
            foreach (SHolding holding in myPf.StockHoldings)
            {
                if ( holding.SRef != sRef) continue;

                RepDataStMgHoldings hldn = new RepDataStMgHoldings()
                {
                    PfName = myPf.Name,
                    Holding = holding,
                    RCEod = rcEod,
                    RRTotalHold = new RCGrowth(holding, fullEod.Close, latestConversionRate),
                    Divident = null,
                    TotalHoldingDivident = null,
                    DividentCurrency = stockMeta.marketCurrency,
                };

                if (holding.AnyDividents())
                {
                    hldn.Divident = RRHoldingDivident.Create(holding); // list of all
                    hldn.TotalHoldingDivident = new RRTotalDivident(holding); // total

                    if (holding.Dividents.Last().Currency != hldn.DividentCurrency)
                        hldn.DividentCurrency = holding.Dividents.Last().Currency;
                }
                report.Add(hldn);
            }
        }
        return new OkResult<List<RepDataStMgHoldings>>(report.OrderBy(r => r.Holding.PurhaceDate).ToList());
    }
}
