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

// Basic high level Client side report of Invested stocks purhace prices, latest valuations, and profit... on Market and HomeCurrency
public class RepDataInvested
{
    public StockMeta StockMeta { get; set; } = null;

    public RCEod RCEod { get; set; } = null;    // This report does NOT support IntraDay

    public RCGrowth RCTotalHold { get; set; } = null;

    public RRTotalDivident RRTotalDivident { get; set; } = null;

    public decimal HcInvestedOfTotalP { get; set; }
    public decimal HcValuationOfTotalP { get; set; }

    public decimal HcGain { get; set; }
    public int HcGainP { get; set; }

    public List<RepDataInvestedSub> SubHoldings { get; set; }
}

public class RepDataInvestedSub
{
    public RCHolding RCHolding { get; set; } = null;

    public RCGrowth RCTotalHold { get; set; } = null;

    public RRTotalDivident RRHoldingsTotalDiv { get; set; } = null;
}

public class RepDataInvestedHeader
{
    public decimal HcTotalInvested { get; set; } = 0;  // Total available if all stocks has 'HcInvested' prorly set
    public decimal HcTotalValuation { get; set; } = 0; // If normal 'TotalValuation' can be calculated then just requires latest currency conversion rates
    public RRTotalDivident HcTotalDivident { get; set; } = null;
    public int HcGrowthP { get; set; } = 0;
    public decimal HcTotalGain { get; set; }
    public int HcTotalGainP { get; set; }
}
