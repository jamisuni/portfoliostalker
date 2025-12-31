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

// Attempt to follow wisdom of smarter ones, and having target weights for stocks... w plan to take profits/add as 'trim your garden'
public class RepDataWeight
{
    public StockMeta StockMeta { get; set; } = null;

    public string NoteHeader { get; set; } = null;

    public RCEod RCEod { get; set; } = null;    // This report does NOT support IntraDay

    public RCGrowth RCTotalHold { get; set; } = null;

    public RRTotalDivident RRTotalDivident { get; set; } = null;

    public string TargetP { get; set; } = null; // From Stalker group item like '5% high prio'

    public decimal CurrentP { get; set; } = 0; // Current %

    public decimal AvrgTimeAsMonths { get; set; } = 0;

    public decimal HcHistoryDivident { get; set; } = 0;

    public decimal HcTradeProfits { get; set; } = 0;

    public decimal HcTakenAgainstInv { get; set; } = 0;
    public decimal HcTakenAgainstVal { get; set; } = 0;

    public decimal YearlyDivPForHcHolding { get; set; } = 0;

    public List<RepDataWeightHoldingSub> SubHoldings { get; set; } = new();

    public List<RepDataWeightTradeSub> SubTrades { get; set; } = new();
}

public class RepDataWeightHoldingSub
{
    public RCHolding RCHolding { get; set; } = null;

    public RCGrowth RCTotalHold { get; set; } = null;

    public RRTotalDivident RRHoldingsTotalDiv { get; set; } = null;

    public decimal CurrentP { get; set; } = 0;

    public decimal YearlyDivPForHcHolding { get; set; } = 0;
}

public class RepDataWeightTradeSub
{
    public RCTrade RCTrade { get; set; } = null;

    public decimal HcTradeProfit { get; set; } = 0;

    public decimal HcTradeDividents { get; set; } = 0;
}

public class RepDataWeightHeader
{
    public decimal TotalCurrentP { get; set; } = 0;     // How much from iOwn is currently shown

    public decimal TotalPlannedP { get; set; } = 0;
}
