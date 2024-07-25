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

public record RCExtraColumn(ExtraColumnId Id, decimal[] value)
{ 
    public RCExtraColumn(ExtraColumnId Id) : this(Id, null) {}

    public bool Valid()
    {
        if (Id == ExtraColumnId.Unknown || value == null)
            return false;
        return true;
    }

    public decimal Sort()
    {
        if (Valid() == false)
            return 0;

        return value[0];
    }
    public decimal ChangeP
    {
        get
        {
            if (Valid() && (Id == ExtraColumnId.CloseWeekAgo || Id == ExtraColumnId.CloseMonthAgo))
                return value[0];

            throw new InvalidOperationException();
        }
    }

    public decimal Min
    {
        get
        {
            if (Valid() && (Id == ExtraColumnId.CloseWeekAgo || Id == ExtraColumnId.CloseMonthAgo))
                return value[2];

            throw new InvalidOperationException();
        }
    }

    public decimal Max
    {
        get
        {
            if (Valid() && (Id == ExtraColumnId.CloseWeekAgo || Id == ExtraColumnId.CloseMonthAgo))
                return value[3];

            throw new InvalidOperationException();
        }
    }
}

/*  CloseWeekAgo,   0 = ChangeP,    1 = Close,      2 = Min,        3 = Max
 *  CloseMonthAgo,  0 = ChangeP,    1 = Close,      2 = Min,        3 = Max
 * 
 * Note! Even on unused case 'Id' is set per column type, but values are -1
 */
