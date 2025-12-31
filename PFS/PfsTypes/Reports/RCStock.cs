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

using System.Threading;

namespace Pfs.Types;

// Created by ReportCollector, and on creation applies ReportFilters to allow filtering per Pf's etc etc
public class RCStock
{
    public StockMeta StockMeta { get; internal set; }
    // [MarketId marketId, string symbol, string name, CurrencyId marketCurrency, string ISIN]

    public SStock Stock { get; internal set; }

    // Note! Alarms atm dont feel need to do anything, they under Stock above

    public RCEod RCEod { get; set; } = null;
    // [FullEOD fullEOD, CurrencyId MarketCurrency, decimal LatestConversionRate]

    public RCGrowth RCTotalHold { get; internal set; } = null; // All 'Holdings' total sum of invested/valuation

    public RCTotalHcDivident RCHoldingsTotalDivident { get; internal set; } = null; // Yes just total for Holdings

    public List<string> PFs { get; set; } = new();

    public List<RCHolding> Holdings { get; set; } = new();

    public List<RCTrade> Trades { get; set; } = new();

    public List<RCOrder> Orders { get; set; } = new(); // Zero/One per each PF, triggered one or closest to be triggered

    public Dictionary<DateOnly, RCDivident> Dividents { get; set; } = new(); // Each PaymentDate is own RCDivident

    public int DivPaidTimesPerYear { get; set; } = 4; // 1 (=yearly),2 (=twice),4 (=quarterly), 12 (=monthly)

    public decimal YearlyDivPForHcHolding { get; set; } = 0;

    public decimal HcTotalTradeDividents
    {
        get 
        {
            decimal total = 0;
            foreach (KeyValuePair<DateOnly, RCDivident> kvp in Dividents)
                total += kvp.Value.HcTotalTradeDiv;
            return total;
        }
    }

    public RCStock(SStock sStock, StockMeta stockMeta)
    {
        Stock = sStock;
        StockMeta = stockMeta;
    }

    public void AddHoldingDivident(decimal units, SHolding.Divident divInfo)
    {
        GetOrAddDivident(divInfo).HoldingUnits += units;
    }

    public void AddTradeDivident(decimal units, SHolding.Divident divInfo)
    {
        GetOrAddDivident(divInfo).TradesUnits += units;
    }

    protected RCDivident GetOrAddDivident(SHolding.Divident divInfo)
    {
        if ( Dividents.ContainsKey(divInfo.PaymentDate) )
            return Dividents[divInfo.PaymentDate];

        RCDivident ret = new(divInfo);
        Dividents.Add(divInfo.PaymentDate, ret);
        return ret;
    }

    public void RecalculateTotals()
    {
        if ( RCEod == null || Holdings.Count == 0 )
        {
            RCTotalHold = null;
            RCHoldingsTotalDivident = null;
            return;
        }

        decimal units = 0;
        decimal hcInvested = 0;
        decimal mcInvested = 0;

        foreach ( RCHolding holding in Holdings )
        {
            units += holding.SH.Units;
            mcInvested += holding.SH.Units * holding.SH.McPriceWithFeePerUnit;
            hcInvested += holding.SH.Units * holding.SH.HcPriceWithFeePerUnit;
        }

        RCTotalHold = new RCGrowth(units, mcInvested, hcInvested, RCEod.fullEOD.Close, RCEod.HcClose);

        // All dividents for this stocks current holdings

        decimal hcHoldingDiv = 0;

        foreach ( KeyValuePair<DateOnly, RCDivident> kvp in Dividents)
            hcHoldingDiv += kvp.Value.HoldingUnits * kvp.Value.PaymentPerUnit * kvp.Value.CurrencyRate;

        RCHoldingsTotalDivident = new RCTotalHcDivident(hcHoldingDiv, hcInvested);

        // Estimate divident payment frequence from last 2 payments

        var twoLatestDivs = Dividents
            .OrderByDescending(kvp => kvp.Key)    // Sort by date descending
            .Take(2)
            .Select(kvp => kvp.Value)
            .ToList();

        if ( twoLatestDivs.Count == 2 )
        {   // if >10 then 12, if > 4 then 6, if >2 then 3, otherwise 1
            int monthsBetween = (twoLatestDivs.First().PaymentDate.Year * 12 + twoLatestDivs.First().PaymentDate.Month) - 
                                (twoLatestDivs.Last().PaymentDate.Year * 12 + twoLatestDivs.Last().PaymentDate.Month);

            DivPaidTimesPerYear = monthsBetween switch
            {
                < 2 => 12,
                >= 2 and <= 4 => 4,
                >= 5 and <= 7 => 2,
                >= 10 and <= 14 => 1,
                _ => 4
            };
        }

        if (RCTotalHold.HcAvrgPrice > 0 && twoLatestDivs.Count > 0)
            // Estimate yearly div% for HcInvested per payment frequence and latest divident payment
            YearlyDivPForHcHolding = twoLatestDivs.First().HcPaymentPerUnit * DivPaidTimesPerYear * 100 / RCTotalHold.HcAvrgPrice;
    }
}
