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

namespace Pfs.Config;

// Providers simple interface allowing caller to fetch information what providers can be used to fetch specific markets symbols
public interface IPfsFetchConfig
{
    // Used for UI purposes to show what provider(s) is to be used
    ExtProviderId[] GetUsedProvForStock(MarketId market, string symbol);

    // Rule those define symbol(s) are handled as priority on fetching, with single provider
    ExtProviderId GetDedicatedProviderForSymbol(MarketId market, string symbol);

    void SetDedicatedProviderForSymbol(MarketId market, string symbol, ExtProviderId providerId);

    // Returns all those market's that this provider is set as one of default fetch providers
    MarketId[] GetMarketsPerRulesForProvider(ExtProviderId providerId);

    ExtProviderId GetRatesProv();
}
