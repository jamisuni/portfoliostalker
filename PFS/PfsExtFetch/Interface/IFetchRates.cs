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

namespace Pfs.ExtFetch;

public interface IFetchRates
{
    // Request to get latest, done background, and returned separately
    Result FetchLatest(CurrencyId toCurrency);

    // "Instant" return of single rate for latest/specific date 
    Task<decimal?> GetRateAsync(CurrencyId toCurrency, CurrencyId fromCurrency, DateOnly? date = null);
}
