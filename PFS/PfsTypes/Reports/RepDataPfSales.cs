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

// Provides details/profits etc from one of sales, and 'Holdings' has one/many holdings sold under TradeId
public class RepDataPfSales
{
    public string TradeId { get; set; }
    public string TradeNote { get; set; }
    public DateOnly SaleDate { get; set; }
    public decimal McSoldUnitPriceWithFee { get; set; }
    public decimal HcSoldUnitPriceWithFee { get; set; }

    public StockMeta StockMeta { get; set; } = null;

    public decimal SoldTotalUnits { get; set; }         // May include units from multiple holdings

    public RCGrowth TotalGrowth { get; set; } = null;   // Sale Profit, so value-invested

    public RRDivident TotalDivident { get; set; } = null;

    public List<ReportTradeHoldings> Holdings { get; set; } = new();
}

public class ReportTradeHoldings
{
    public SHolding Holding { get; set; } = null;

    public RCGrowth Growth { get; set; } = null;    // Sale Profit, so value-invested

    public RRDivident Divident { get; set; } = null;

    public int HoldingMonths { get; set; } = 0;
}
