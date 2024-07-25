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

namespace Pfs.Shared.Stalker;

public class StalkerSplit
{
    // Instead of using standard string.Split... doing own as I wanna keep things together if has [] around it
    public static List<string> SplitLine(string line)
    {
        /* Rules:
            * - Supports [this is longer] and Note=[This is longer]    => 'this is longer' 'Note=This is longer'
            * - '[' is required to be after ' ' or '='
            * - ']' is required to be before space or end of line
            */
        List<string> ret = new();

        int open = 0;
        string split = string.Empty;
        char prevCh;
        char ch = '~';

        for ( int pos = 0; pos < line.Length; pos++ )
        {
            prevCh = ch;
            ch = line[pos];

            if (ch == '[' && (prevCh == ' ' || prevCh == '='))
            {
                // increase open count
                open++;
                continue;
            }

            if (ch == ']' && open > 0 && (pos+1 == line.Length || line[pos+1] == ' ') )
            {
                open--;
                continue;
            }

            if (ch == ' ' && open == 0)
            {
                if (string.IsNullOrWhiteSpace(split) == false)
                    ret.Add(split);

                split = string.Empty;
                continue;
            }

            split += ch;
        }

        if (string.IsNullOrWhiteSpace(split) == false)
            ret.Add(split);

        return ret;
    }
}
