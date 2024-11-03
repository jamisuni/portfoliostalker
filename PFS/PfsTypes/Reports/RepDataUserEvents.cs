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

using static Pfs.Types.SOrder;

namespace Pfs.Types;

public class RepDataUserEvents
{
    public UserEventType Type { get; set; }
    public UserEventStatus Status { get; set; }
    public DateOnly Date { get; set; }
    public int Id { get; set; }
    public StockMeta StockMeta { get; set; }
    public string PfName { get; set; } = string.Empty;
    public AlarmInfo Alarm { get; set; } = null;
    public OrderInfo Order { get; set; } = null;

    public class AlarmInfo
    {
        public decimal AlarmValue { get; set; }
        public decimal DayClosed { get; set; }
        public decimal? DayLow { get; set; } = null;
        public decimal? DayHigh { get; set; } = null;
        public decimal? AlarmDropP { get; set; } = null;
    }

    public class OrderInfo
    {
        public OrderType Type { get; set; }

        public decimal Units { get; set; }

        public decimal PricePerUnit { get; set; }
    }
}
