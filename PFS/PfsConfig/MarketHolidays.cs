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

namespace Pfs.Config;

// Helper class for MarketConfig, providing handling of special holidays string format
public class MarketHolidays
{
    public static DateOnly[] GetDates(string holidays) // "2024:Jan,6,9:Apr,17:Jun,4,5,6:Dec,24,25,26"
    {
        if (string.IsNullOrWhiteSpace(holidays))
            return Array.Empty<DateOnly>(); // empty is OK

        try
        {
            string[] months = holidays.Split(':');

            int yearNumber;

            if (int.TryParse(months[0], out yearNumber) == false || (yearNumber != DateTime.UtcNow.Year && yearNumber != DateTime.UtcNow.Year-1) )
                return null; // but errors are 'null' marking failure

            List<DateOnly> dates = new();

            for (int m = 1; m < months.Length; m++)
            {
                string[] days = months[m].Split(',');

                int monthNumber = DateTime.ParseExact(days[0], "MMM", CultureInfo.InvariantCulture).Month;

                for (int d = 1; d < days.Length; d++)
                    dates.Add(new DateOnly(yearNumber, monthNumber, int.Parse(days[d])));
            }
            return dates.ToArray();
        }
        catch ( Exception )
        {
        }
        return null;
    }

    // EodHD would have https://eodhd.com/financial-apis/exchanges-api-trading-hours-and-stock-market-holidays
    // but maybe not worth of coding... as can do this few mins on end of year manually.
}
