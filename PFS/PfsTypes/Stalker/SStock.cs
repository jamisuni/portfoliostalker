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

// Mainly client side, presents one tracked Stock on internal data format where Alarm's & StockMeta etc are available for specific Stock STID,
public class SStock
{
    public string SRef { get; set; }                    // "MarketId$SYMBOL"

    public int[] Sectors { get; set; }                  // If >= 0 then assigns to sector's field

    // actual types are: SAlarmUnder, SAlarmOver, 
    public List<SAlarm> Alarms { get; set; } = new();   // Stock can have multiple different alarms per its value & indicators

    public SStock(string sRef)
    {
        SRef = sRef;
        Sectors = Enumerable.Repeat(-1, SSector.MaxSectors).ToArray();
    }

    public SStock DeepCopy()
    {
        SStock ret = (SStock)this.MemberwiseClone();
        ret.Alarms = new();

        foreach (SAlarm alarm in this.Alarms)
            ret.Alarms.Add(alarm.DeepCopy());

        return ret;
    }
}
