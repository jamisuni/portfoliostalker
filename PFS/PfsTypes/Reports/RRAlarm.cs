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

public class RRAlarm
{
    public decimal? Over { get; internal set; } = null;
    public decimal? OverP { get; internal set; } = null;
    public string OverNote { get; internal set; } = null;

    public decimal? Under { get; internal set; } = null;
    public decimal? UnderP { get; internal set; } = null;
    public string UnderNote { get; internal set; } = null;

    public RRAlarm(RCEod rcEOD, List<SAlarm> alarms)
    {
        decimal latestHigh = rcEOD.fullEOD.GetSafeHigh();
        decimal latestLow  = rcEOD.fullEOD.GetSafeLow();

        // PFS allows to have many alarms, even same types, but for UI we only show one over and one under
        // alarm. Picking one thats closest to alarm level or has already active alarm
        foreach ( SAlarm alarm in alarms)
        {
            if (alarm.AlarmType.IsOverType())
            {
                decimal? procent = alarm.GetAlarmDistance(latestHigh);

                if ( OverP == null || procent.HasValue && procent.Value > OverP )
                {   // first or highest
                    Over = alarm.Level;
                    OverP = procent;

                    if (rcEOD.fullEOD.HasHigh())
                        OverNote = $"{Over}: {alarm.Note} (days highest {latestHigh.To00()})";
                    else
                        OverNote = $"{Over}: {alarm.Note}";
                }
            }
            else if (alarm.AlarmType.IsUnderType())
            {
                decimal? procent = alarm.GetAlarmDistance(latestLow);

                if (UnderP == null || procent.HasValue && procent.Value > UnderP)
                {
                    Under = alarm.Level;
                    UnderP = procent;
                    if (rcEOD.fullEOD.HasLow())
                        UnderNote = $"{Under}: {alarm.Note} (days lowest {latestLow.To00()})";
                    else
                        UnderNote = $"{Under}: {alarm.Note}";
                }
            }
        }
    }
}
