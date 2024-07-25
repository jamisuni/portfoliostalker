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

using System.Text;

namespace Pfs.Types;

public class Transaction
{
    public TaType Action { get; set; }              // Action: Buy, sell, or dividend.
    public string UniqueId { get; set; }            // Must be set if wanting to avoid duplicates on rerun's
    public DateOnly RecordDate { get; set; }        // Actual buy/sell date, or for dividents this is ExDivDate
    public DateOnly PaymentDate { get; set; }       // When transaction finished so 2 days delay for buy/sell. For divident is when company pays it.
    public string Note { get; set; }

    public string ISIN { get; set; }                // International Securities Identification Number.
    public MarketId Market { get; set; }            
    public string Symbol { get; set; }
    public string CompanyName { get; set; }

    public CurrencyId Currency { get; set; }        // Market currency
    public decimal CurrencyRate { get; set; }

    public decimal Units { get; set; }              // Quantity of shares.
    public decimal McAmountPerUnit { get; set; }    // PricePerUnit, PaymentPerUnit, etc ... Price / share: Share price
    public decimal McFee { get; set; }

    public bool IsRateMissing()
    {
        switch ( Action )
        {
            case TaType.Round:
            case TaType.Unknown:
            case TaType.Close:
                return false;
        }
        if (CurrencyRate <= 0)
            return true;

        return false;
    }

    public string IsValid()
    {
        StringBuilder sb = new();

        switch (Action)
        {
            case TaType.Unknown:
                return "Unknown type action";


        }

        // !!!TODO!!! Get back here and add verifications per type etc to make sure all fields are there and has positive etc values

        return sb.ToString();
    }
}

public enum TaType : int
{
    Unknown = 0,
    Buy,
    Sell,
    Divident,
    Round,
    Close,
}
