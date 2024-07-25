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

// Holds user defined sector-field's names to allow users own grouping of stocks
public class SSector
{
    public const int MaxSectors = 3;

    public const int MaxFields = 18;

    public const int MaxNameLen = 20;

    public string Name { get; set; }

    public string[] FieldNames { get; set; } = new string[MaxFields];

    public SSector(string name)
    {
        Name = name;
    }

    public SSector DeepCopy()
    {
        SSector ret = (SSector)this.MemberwiseClone();

        // !!!TODO!!!

        return ret;
    }
}
