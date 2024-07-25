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

namespace PfsUI;

public class UiF
{
    // !!!LATER!!! With far far on future, when doing actual clean UI with translations, change this system to use Microsoft proper output formats..
    static public string Curr(CurrencyId? currency)
    {
        if (currency.HasValue == false)
            return "?";

        switch (currency.Value)
        {
            case CurrencyId.CAD: return "C$";
            case CurrencyId.EUR: return "E";
            case CurrencyId.USD: return "U$";
            case CurrencyId.SEK: return "SEK";
            case CurrencyId.GBP: return "£";
        }
        return "?";
    }

    static public string AvrgPrice(decimal price)
    {
        if (price >= 100)
            return price.ToString("0");
        if ( price >= 20)
            return price.ToString("0.0");

        return price.ToString("0.00");
    }
}
