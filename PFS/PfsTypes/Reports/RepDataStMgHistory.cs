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

namespace Pfs.Types;

public class RepDataStMgHistory
{
    /* Report shows many type entries, where each one is presented one of these records. 
     * As data of them is different, a fields here are filler per Type
     * 
     * - If partial sell then comes both: OWN & BUY
     * - Under BUY's has line(s) that identifies date of sale w price, profit, divident total
     * - Under OWN has its dividents on dropdown
     * - 
     */

    public string PfName { get; set; }

    public iOwn Own { get; set; } = null;

    public iBuy Buy { get; set; } = null;

    public iSold Sold { get; set; } = null;

    public StockMetaHist History { get; set; } = null;

    public iTotal Total { get; set; } = null;

    public DateOnly Date { get; set; }

    public string Note { get; set; }

    public RCEod RCEod { get; set; } = null;

    public RRTotalDivident TotalDivident { get; set; } = null;

    protected RepDataStMgHistory()
    {

    }

    public static RepDataStMgHistory CreateOwnPerHolding(string pfName, SHolding holding, RCEod rcEod)
    {
        RepDataStMgHistory ret = new()
        {
            PfName = pfName,
            Date = holding.PurhaceDate,
            Note = holding.PurhaceNote,
            RCEod = rcEod,
        };

        if ( holding.AnyDividents())
            ret.TotalDivident = new RRTotalDivident(holding);

        ret.Own = new()
        {
            Holding = holding,
            HcInv = holding.HcPriceWithFeePerUnit * holding.Units,
            HcGrowth = (rcEod.HcClose - holding.HcPriceWithFeePerUnit) * holding.Units
        };

        return ret;
    }

    public static RepDataStMgHistory CreateBuyPerHolding(string pfName, SHolding holding, RCEod rcEod)
    {
        RepDataStMgHistory ret = new()
        {
            PfName = pfName,
            Date = holding.PurhaceDate,
            Note = holding.PurhaceNote,
            RCEod = rcEod,
            TotalDivident = null,       // Need to be calculated by creator as bases "Sales"
        };

        ret.Buy = new()
        {
            Holding = holding,
            Sales = new(),
        };

        return ret;
    }

    public static RepDataStMgHistory CreateSoldPerTrade(string pfName, SHolding trade, RCEod rcEod)
    {
        RepDataStMgHistory ret = new()
        {
            PfName = pfName,
            Date = trade.Sold.SaleDate,
            Note = trade.Sold.TradeNote,
            RCEod = rcEod,
        };

        if (trade.Dividents.Count > 0)
            ret.TotalDivident = new RRTotalDivident(trade);

        ret.Sold = new()
        {
            Holding = trade,
            HcInv = trade.HcPriceWithFeePerUnit * trade.Units,
            HcSold = trade.Sold.HcPriceWithFeePerUnit * trade.Units
        };

        return ret;
    }

    public static RepDataStMgHistory CreateTotal(RCEod rcEod)
    {
        RepDataStMgHistory ret = new()
        {
            Date = DateOnly.MinValue,
            RCEod = rcEod,
        };

        ret.Total = new()
        {
        };

        return ret;
    }

    public static RepDataStMgHistory CreateHistory(StockMetaHist hist)
    {
        RepDataStMgHistory ret = new()
        {
            Date = hist.Date,
            RCEod = null,
            PfName = string.Empty,
            Note = hist.Note,
        };

        ret.History = hist;

        return ret;
    }

    public class iTotal
    {
        public decimal HcInv { get; set; } = 0;    // Total currently invested
        public decimal HcProfit { get; set; } = 0; // Total sum from 'Sold's
        public decimal HcGrowth { get; set; } = 0; // Total sum from 'Own's
        public decimal HcDiv { get; set; } = 0;    // Total sum from 'Own's + 'Sold's

        public void AddOwn(RepDataStMgHistory own)
        {
            HcInv += own.Own.HcInv;

            HcGrowth += own.Own.HcGrowth;

            if (own.TotalDivident != null)
                HcDiv += own.TotalDivident.HcDiv;
        }

        public void AddSold(RepDataStMgHistory sold)
        {
            HcProfit += sold.Sold.HcSold - sold.Sold.HcInv;

            if (sold.TotalDivident != null)
                HcDiv += sold.TotalDivident.HcDiv;
        }
    }

    public class iOwn
    {
        public SHolding Holding { get; set; } = null;

        public decimal HcInv {  get; set; } = 0;
        public decimal HcGrowth { get; set; } = 0;
    }

    public class iBuy
    {
        public SHolding Holding { get; set; } = null;
        public List<SHolding> Sales { get; set; } = null;
    }

    public class iSold
    {
        public SHolding Holding { get; set; } = null;

        public decimal HcInv { get; set; } = 0;
        public decimal HcSold { get; set; } = 0;
    }
}
