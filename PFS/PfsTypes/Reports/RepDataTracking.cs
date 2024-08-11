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

public class RepDataTracking
{
    public StockMeta Stock { get; set; } = null;
    public string NoteHeader { get; set; } = null;

    public ExtProviderId[] FetchProvider { get; set; }
    public List<string> AnyPfTracking { get; set; } = new();    // Names of all portfolio's those follow this stock
    public List<string> AnyPfHoldings { get; set; } = new();    // Names of all portfolio's those have holdings of this stock
    public List<string> AnyPfTrades { get; set; } = new();      // Names of all stock group's those has sales of this stock

    public RCEod RCEod { get; set; } = null;

    public DateTime? IsIntraday { get; set; } = null;

    public bool IsMarketActive { get; set; } = true;            // Market Disabled/Closed
}
