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

// IPfsMarketConfig: User customizable parts of Market
public record MarketCfg(bool Active, string Holidays, int minFetchMins); // "2024:Jan,6,9:Apr,17:Jun,4,5,6:Dec,24,25,26"

// IPfsProvConfig
public record ProvFetchCfg(MarketId market, string symbols, ExtProviderId[] providers);
public record RatesFetchCfg(ExtProviderId provider);
