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

// GetMarketStatus() - Settings - Markets 'status report'
public record MarketStatus(MarketMeta market, bool active, DateOnly lastDate, DateTime lastClosingUtc, DateTime nextClosingUtc, int minFetchMins);

public class FetchProgress
{
    public int Requested { get; set; }
    public int PriorityLeft { get; set; }
    public int TotalLeft { get; set; }
    public int Failed { get; set; }
    public int Ignored { get; set; }    // no provider w rule
    public int Succeeded { get; set; }
    public PerProv[] ProvInfo { get; set; }

    public class PerProv
    {
        public ExtProviderId ProvId { get; set; }
        public bool Busy { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
        public int? CreditsLeft {  get; set; }
    }

    public class PerMarket
    {
        public MarketId Market { get; set; }

        // !!!TODO!!! Add when figuring out something important, a second table to view...
        //            but amount of pendings per market is NOT important
    }
}
