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

public class ClosingEOD
{
    public DateOnly Date { get; set; }      // !!!NOTE!!! This is Market's local date, and not utc (actually utc would be same)
    public decimal Close { get; set; }

    public ClosingEOD()
    {
    }

    public ClosingEOD(string storageFormat) // little development helper to import fast some EOD file copies
    {
        // "2022-05-19,20.2100"

        string[] split = storageFormat.Split(',');
        Date = DateOnly.ParseExact(split[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Close = DecimalExtensions.Parse(split[1]);
    }
}

// This is format that all End-Of-Day ala Stock's EOD history is collected
public class FullEOD : ClosingEOD
{
    public decimal Open { internal get; set; } = -1;    // Note! If something isnt available by provider then keep -1's!
    public decimal High { internal get; set; } = -1;    // Lets see if can keep internal get's or not but try use GetSafeHigh()
    public decimal Low { internal get; set; } = -1;
    public decimal PrevClose { get; set; } = -1;        // Except PrevClose that gets overwritten on storage w previous stored
    public int Volume { get; set; } = -1;               // This 'get' is need on twelve's special case

    public bool HasLow() { return Low > 0.0001m; }
    public bool HasHigh() { return High > 0.0001m; }

    public decimal GetSafeLow()
    {
        return Low > 0.0001m ? Low : Close;
    }

    public decimal GetSafeHigh()
    {
        return High > 0.0001m ? High : Close;
    }

#if false
    public EOD DeepCopy()
    {
        EOD ret = (EOD)this.MemberwiseClone(); // Works as deep as long no complex tuff
        return ret;
    }
#endif

    public FullEOD()
    {
    }

    public FullEOD(string storageFormat) // little development helper to import fast some EOD file copies
    {
        // "2022-05-19,20.2100,19.9200,20.3400,19.9100,20.2300,41182332"

        string[] split = storageFormat.Split(',');

        Date = DateOnly.ParseExact(split[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Close = DecimalExtensions.Parse(split[1]);
        Open = DecimalExtensions.Parse(split[2]);
        High = DecimalExtensions.Parse(split[3]);
        Low = DecimalExtensions.Parse(split[4]);
        PrevClose = DecimalExtensions.Parse(split[5]);
        Volume = int.Parse(split[6]);
    }

    public void DivideBy(int divider) // Mainly to fix London that gives pennies instead of pounds on fetch/imports/etc
    {
        if (Close > divider)
            Close /= divider;

        if (Open > divider)
            Open /= divider;

        if (High > divider)
            High /= divider;

        if (Low > divider)
            Low /= divider;

        if (PrevClose > divider)
            PrevClose /= divider;
    }

    public string GetStoreFormat()
    {
        return $"{Date.ToString("yyyy-MM-dd")},{Close.ToString("0.####")},{Open.ToString("0.####")},{High.ToString("0.####")},"+
               $"{Low.ToString("0.####")},{PrevClose.ToString("0.####")},{Volume.ToString("0.####")}";
    }
}
