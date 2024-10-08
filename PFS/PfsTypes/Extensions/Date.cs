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

public static class DateOnlyExtensions
{
    public static DateTime ToDateTimeUTC(this DateOnly date)
    {
        return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime ToDateTimeLocal(this DateOnly date)
    {
        return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
    }

    public static string ToYMD(this DateOnly date)
    {
        return date.ToString("yyyy-MM-dd");
    }

    public static DateOnly ParseYMD(string dateString) // call with: DateOnlyExtensions.ParseYMD(
    {
        if (DateOnly.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly parsedDate))
            return parsedDate;

        throw new ArgumentException($"Invalid date format {dateString}. Expected yyyy-MM-dd.");
    }

    public static int GetWorkingDayOfMonth(this DateOnly date) // 0...22
    {
        DateOnly checkDate = new DateOnly(date.Year, date.Month, 1);

        int mon2fri = 0; // So far none of AIs been able to provide correct calculation version of this

        for (; checkDate < date; checkDate = checkDate.AddDays(1))
        {
            if (checkDate.DayOfWeek == DayOfWeek.Saturday || checkDate.DayOfWeek == DayOfWeek.Sunday)
                continue;

            mon2fri++;
        }

        return mon2fri;
    }

    public static int GetWorkingDaysOnMonth(this DateOnly date)
    {
        DateOnly checkDate = new DateOnly(date.Year, date.Month, 1);

        int mon2fri = 0; // So far none of AIs been able to provide correct calculation version of this

        for (; checkDate.Month == date.Month; checkDate = checkDate.AddDays(1))
        {
            if (checkDate.DayOfWeek == DayOfWeek.Saturday || checkDate.DayOfWeek == DayOfWeek.Sunday)
                continue;

            mon2fri++;
        }

        return mon2fri;
    }

    public static DateOnly AddWorkingDays(this DateOnly dtFrom, int nDays)
    {
        return DateOnly.FromDateTime(new DateTime(dtFrom.Year, dtFrom.Month, dtFrom.Day).AddWorkingDays(nDays));
    }

    public static DateOnly FridayOnTwoWeeksAhead(this DateOnly dtFrom)
    {
        DateOnly ret = dtFrom;

        while (ret.DayOfWeek != DayOfWeek.Friday)
            ret = ret.AddDays(1);

        return ret.AddDays(14);
    }
}
