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

public class RepDataExpHoldings
{
    public StockMeta StockMeta { get; set; } = null;
    public string SectorDef { get; set; }

    public RCEod RCEod { get; set; } = null;

    public RCGrowth RCTotalHold { get; set; } = null;

    public RRTotalDivident RRTotalDivident { get; set; } = null;

    public decimal HcGain { get; set; }
    public int HcGainP { get; set; }

    public decimal AvrgTimeAsMonths { get; set; } = 0;
}
