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

public class RepGenDivident
{
    public static Result<RepDataDivident> GenerateReport(DateOnly today, IReportFilters reportParams, IReportPreCalc collector, StalkerData stalkerData, IStockMeta stockMetaProv)
    {
        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return new FailResult<RepDataDivident>("No stock found");

        RepDataDivident ret = new();

        foreach (RCStock stock in reportStocks)
        {
            if (stock.RCTotalHold != null)
            {
                ret.HcTotalInvested += stock.RCTotalHold.HcInvested;
                ret.HcTotalValuation += stock.RCTotalHold.HcValuation;
            }

            StockMeta stockMeta = stockMetaProv.Get(stock.Stock.SRef);

            // Dividents under RCStock are on dictionary w PaymentDate as key, and that stocks that day dividents on Value
            foreach ( KeyValuePair<DateOnly,RCDivident> kvp in stock.Dividents)
            {
                DateOnly month = new DateOnly(kvp.Key.Year, kvp.Key.Month, 1);

                // Calculating total divident payed for each month 
                if (ret.HcTotalMonthly.ContainsKey(month) == false)
                    ret.HcTotalMonthly.Add(month, kvp.Value.HcTotalDiv);
                else
                    ret.HcTotalMonthly[month] += kvp.Value.HcTotalDiv;

                if (kvp.Key < today.AddMonths(-13))
                    continue; // we only keep details for past year dividents for this report.. each stock.. each paymentDate on separately

                RepDataDivident.Payment div = new()
                {
                    StockMeta = stockMeta,
                    ExDivDate = kvp.Value.ExDivDate,
                    PaymentDate = kvp.Value.PaymentDate,
                    Units = kvp.Value.HoldingUnits + kvp.Value.TradesUnits,
                    PayPerUnit = kvp.Value.PaymentPerUnit,
                    HcPayPerUnit = kvp.Value.PaymentPerUnit * kvp.Value.CurrencyRate,
                    Currency = kvp.Value.Currency,
                };

                ret.LastPayments.Add(div);
            }
            ret.LastPayments = ret.LastPayments.OrderByDescending(d => d.PaymentDate).ToList();
        }

        return new OkResult<RepDataDivident>(ret);
    }
}
