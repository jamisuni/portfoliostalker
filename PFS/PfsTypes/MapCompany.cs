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

// Used when per external company information, like transactions, trying to figure out StockMeta per given info
public class MapCompany
{
    public string ExtSymbol { get; set; }
    public MarketId ExtMarketId { get; set; } = MarketId.Unknown;
    public string ExtCompanyName { get; set; }
    public string ExtISIN { get; set; }
    public CurrencyId ExtMarketCurrencyId { get; set; } = CurrencyId.Unknown;

    // This is only assigned after PFS figures out what stock this broker refers of user stock base
    public StockMeta StockMeta { get; set; } = null;

    // ISIN$SYMBOL$NAME,    w any two can be missing, like "$MSFT$" => Allows example map transactions to companies

    public MapCompany DeepCopy()
    {
        return new MapCompany()
        {
            ExtSymbol = new(this.ExtSymbol),
            ExtMarketId = this.ExtMarketId,
            ExtCompanyName = new(this.ExtCompanyName),
            ExtISIN = new(this.ExtISIN),
            ExtMarketCurrencyId = this.ExtMarketCurrencyId,
            StockMeta = this.StockMeta?.DeepCopy(),
        };
    }

    public static string MapCompRef(string ISIN, string symbol, string name)
    {
        if ( ISIN == null ) ISIN = string.Empty;
        if ( symbol == null ) symbol = string.Empty;
        if ( name == null ) name = string.Empty;

        if (string.IsNullOrEmpty(ISIN) && string.IsNullOrEmpty(symbol) && string.IsNullOrEmpty(name))
            return "";

        return $"{ISIN}${symbol}${name}";
    }

    public string MapCompRef()
    {
        return MapCompRef(ExtISIN, ExtSymbol, ExtCompanyName);
    }

    public bool IsSame(string mapCompRef)
    {
        return string.Compare(mapCompRef, MapCompRef()) == 0;
    }

    public bool IsMatchingISIN()
    {
        if (string.IsNullOrEmpty(ExtISIN) || StockMeta == null || string.IsNullOrEmpty(StockMeta.ISIN))
            return false;

        return string.Compare(ExtISIN, StockMeta.ISIN) == 0;
    }
}
