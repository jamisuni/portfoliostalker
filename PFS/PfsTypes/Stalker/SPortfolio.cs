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

// Portfolio may present example one broker/bank account w its investments
public class SPortfolio
{
    public string Name { get; set; }

    public List<string> SRefs { get; set; }             // Stocks this portfolio is following (==showing on user on lists)

    public List<SOrder> StockOrders { get; set; }

    public List<SHolding> StockHoldings { get; set; }   // Still owned stocks (Note! 'PurhaceId' is unique on here)

    public List<SHolding> StockTrades { get; set; }     // Oldies ala sold holdings ('TradeId' is NOT unique but 'PurhaceId+TradeId' combo is)

    public SPortfolio()
    {
        SRefs = new();
        StockHoldings = new();
        StockOrders = new();
        StockTrades = new();
    }

    public SPortfolio DeepCopy()
    {
        SPortfolio ret = (SPortfolio)this.MemberwiseClone();

        ret.SRefs = new();
        foreach (string sRef in this.SRefs)
            ret.SRefs.Add(new string(sRef));

        ret.StockOrders = new();
        foreach (SOrder order in this.StockOrders)
            ret.StockOrders.Add(order.DeepCopy());

        ret.StockHoldings = new();
        foreach (SHolding holding in this.StockHoldings)
            ret.StockHoldings.Add(holding.DeepCopy());

        ret.StockTrades = new();
        foreach (SHolding trade in this.StockTrades)
            ret.StockTrades.Add(trade.DeepCopy());

        return ret;
    }
}
