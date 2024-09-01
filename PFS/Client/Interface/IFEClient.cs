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

using Pfs.Types;

namespace Pfs.Client;

public interface IFEClient // drop here things those dont yet fit to final apis
{
    event EventHandler<FeEventArgs> EventPfsClient2PHeader; // Single EV to all possible events those PFS may send to FE (this is for PageHeader)
    event EventHandler<FeEventArgs> EventPfsClient2Page;    // Identical event to page itself

    public class FeEventArgs : EventArgs
    {
        public string Event { get; set; }
        public object Data { get; set; }
    }

    // Allows to push EOD to storing/use, can be used example on TestFetch 
    void AddEod(MarketId marketId, string symbol, FullEOD eod);
}
