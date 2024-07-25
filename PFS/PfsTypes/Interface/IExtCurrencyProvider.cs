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

// Prototype for any Ext Providers to access Currency Rate information
public interface IExtCurrencyProvider
{
    // Allows to fetch some provider specific limits
    int CurrencyLimit(CurrencyLimitId limitId);

    public enum CurrencyLimitId : int
    {
        Unknown = 0,
        SupportBatchFetch,          // 0 == false, 1 == true (=> can leave fromCurrency off and receive all currencies)
    }

    /* STALKER USES:    ConversionRate with ConversionTo == HomeCurrency... so FOLLOW SAME!
     * Example Stalker: 1000$ to euros has rate 0.85 = 850E 
	 * 
	 * ==> FROM is non-home currency, and TO is HomeCurrency... and result for $ -> HC E needs to be 0.85
     */

    // If toCurrency is left unknown then expected to return conversion for all 'CurrencyCode' currencies as a batch fetch
    Task<(DateTime UTC, Dictionary<CurrencyId, decimal> rates)> 
        GetCurrencyLatestAsync(CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown);

    // Get specific historical day rate information
    Task<Dictionary<CurrencyId, decimal>> 
        GetCurrencyHistoryAsync(DateOnly date, CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown);

    // Note! If need later range then add 'GetCurrencyRangeAsync' w from->to but wo batch?
}
