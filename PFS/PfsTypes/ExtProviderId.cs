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

using System.Runtime.Serialization;

namespace Pfs.Types;

[DataContract]
public enum ExtProviderId : int
{
    Unknown = 0,
    [EnumMember(Value = "Unibit")]
    Unibit,
    [EnumMember(Value = "AlphaVantage")]
    AlphaVantage,
    [EnumMember(Value = "Polygon")]
    Polygon,
    [EnumMember(Value = "CurrencyAPI")]
    CurrencyAPI,
    [EnumMember(Value = "TwelveData")]
    TwelveData,
    [EnumMember(Value = "Marketstack")]
    Marketstack,
    [EnumMember(Value = "FMP")]
    FMP,
    //[EnumMember(Value = "Tiingo")]    last tested 2024-Apr: WASM doesnt work! they have CORS config issues? F12 shows!
    //Tiingo,
    //[EnumMember(Value = "Iexcloud")]
    //Iexcloud,                         // shut down on August 31, 2024
}

public static class ExtProviderIdExtensions
{
    public static bool SupportsRates(this ExtProviderId provId)
    {
        switch ( provId)
        {
            case ExtProviderId.CurrencyAPI:
                return true;
        }
        return false;
    }

    public static bool SupportsStocks(this ExtProviderId provId)
    {
        switch (provId)
        {
            case ExtProviderId.AlphaVantage:
            case ExtProviderId.TwelveData:
            case ExtProviderId.Unibit:
            case ExtProviderId.Polygon:
            case ExtProviderId.Marketstack:
            case ExtProviderId.FMP:
                return true;
        }
        return false;
    }
}

public enum ExtProviderJobType : int
{
    Unknown = 0,
    EndOfDay,       // Provides basic End-Of-Day valuation for history & latest (IExtMarketDataProvider)
    Intraday,       // Optional functionality for 'IExtMarketDataProvider' to provide also Intraday information
    Currency,       // History & Latest currency valuations (IExtCurrencyDataProvider)
}
