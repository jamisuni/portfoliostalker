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

    /*
     * Symbol
     * Company
     * 
     * Target%  from group item like '5% high prio'
     * Curr%
     * Diff%    - missing atm...
     * 
     * Growth%
     * Div%
     * 
     * House%
     * 
     * 
     * 
     * TODO:
     * - Yes do also second line comment w % that has extra popup over target% to show plan to increase
     * - Keep that drop down (sub) almost unchanged, but add there also info from Curr%... and maybe from history sales?
     * - 
     */





    public decimal HcInvestedOfTotalP { get; set; }     // useless

    public decimal HcGain { get; set; }
    public int HcGainP { get; set; }

    public List<RepDataWeightSub> SubHoldings { get; set; }
}

public class RepDataWeightSub
{
    public RCHolding RCHolding { get; set; } = null;

    public RCGrowth RCTotalHold { get; set; } = null;

    public RRTotalDivident RRHoldingsTotalDiv { get; set; } = null;
}

public class RepDataWeightHeader
{
    public decimal HcIOwn { get; set; } = 0;            // This is coming from configs if user provided
    public decimal HcTotalValuation { get; set; } = 0;
    public decimal TotalCurrentP { get; set; } = 0;     // How much from iOwn is currently shown


    public RRTotalDivident HcTotalDivident { get; set; } = null;
    public int HcGrowthP { get; set; } = 0;
    public decimal HcTotalGain { get; set; }
    public int HcTotalGainP { get; set; }


    public decimal HcTotalInvested { get; set; } = 0;   // useless
}
