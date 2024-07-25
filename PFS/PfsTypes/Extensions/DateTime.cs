
namespace Pfs.Types;

public static class DateTimeExtensions
{
    public static string ToYMD(this DateTime date)
    {
        return date.ToString("yyyy-MM-dd");
    }

    public static DateTime AddWorkingDays(this DateTime dtFrom, int nDays)
    {
        // https://snipplr.com/view/12420/method-for-adding-working-days-to-a-date (could not find any licensing restriction as of 2021-Oct)

        // determine if we are increasing or decreasing the days
        int nDirection = 1;
        if (nDays < 0)
        {
            nDirection = -1;
        }

        // move ahead the day of week
        int nWeekday = nDays % 5;
        while (nWeekday != 0)
        {
            dtFrom = dtFrom.AddDays(nDirection);

            if (dtFrom.DayOfWeek != DayOfWeek.Saturday
                && dtFrom.DayOfWeek != DayOfWeek.Sunday)
            {
                nWeekday -= nDirection;
            }
        }

        // move ahead the number of weeks
        int nDayweek = (nDays / 5) * 7;
        dtFrom = dtFrom.AddDays(nDayweek);

        return dtFrom;
    }
}
