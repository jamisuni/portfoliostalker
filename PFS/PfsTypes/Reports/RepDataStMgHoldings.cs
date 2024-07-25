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

public class RepDataStMgHoldings
{
    public string PfName { get; set; }

    public SHolding Holding { get; set; }

    public RCEod RCEod { get; set; } = null;    // This report does NOT support IntraDay

    public RCGrowth RRTotalHold { get; set; } = null;

    public RRTotalDivident TotalHoldingDivident { get; set; } = null;

    public List<RRHoldingDivident> Divident { get; set; } = new(); // Latest is first per PaymentDate

    public CurrencyId DividentCurrency { get; set; } = CurrencyId.Unknown;
}
