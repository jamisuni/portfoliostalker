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

using System.Globalization;

namespace Pfs.Types;

public static class DecimalExtensions
{
    public static decimal Round5(this decimal value)
    {
        return Math.Round(value, 5);
    }

    public static decimal Round3(this decimal value)
    {
        return Math.Round(value, 3);
    }

    public static bool IsInteger(this decimal value)
    {
        return value % 1 == 0;
    }

    public static string To(this decimal value)
    {
        return value.ToString("0");
    }

    public static string To0(this decimal value)
    {
        return value.ToString("0.0");
    }

    public static string To00(this decimal value)
    {
        return value.ToString("0.00");
    }

    public static string To000(this decimal value)
    {
        return value.ToString("0.000");
    }

    public static string To0000(this decimal value)
    {
        return value.ToString("0.0000");
    }

    public static string ToP(this decimal value) // procentage
    {
        if (-20 < value && value < 20 )
            return value.ToString("0.0") + "%";
        else
            return value.ToString("0") + "%";
    }

    public static string ToV(this decimal value) // compact value
    {
        if (value < 20)
            return value.ToString("0.00");
        else if (value < 100)
            return value.ToString("0.0");
        else
            return value.ToString("0");
    }

    public static decimal ToVR(this decimal value) // compact value
    {
        if (value < 20)
            return decimal.Round(value, 2);
        else if (value < 100)
            return decimal.Round(value, 1);
        else
            return decimal.Round(value, 0);
    }

    public static decimal Parse(string str) // call with: DecimalExtensions.Parse(
    {   // Had issue with region effecting to parsing result of decimals, so this allows either way
        return decimal.Parse(str.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}
