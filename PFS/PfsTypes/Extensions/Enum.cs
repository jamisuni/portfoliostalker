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

using System.Reflection;
using System.Runtime.Serialization;

public static class EnumExtensions
{
    public static string GetEnumMemberValue<T>(this T value) where T : Enum
    {
        return typeof(T)
            .GetTypeInfo()
            .DeclaredMembers
            .SingleOrDefault(x => x.Name == value.ToString())
            ?.GetCustomAttribute<EnumMemberAttribute>(false)
            ?.Value;
    }

    public static T ConvertBack<T>(string value) where T : struct, Enum        // Conversion "[EnumMember(Value = "S")]" => "EvFieldId.Status" => As Enum.TryParse / Enum.Parse doesnt NOT work for Value's
    {
        foreach (T e in Enum.GetValues(typeof(T)))
        {
            if (e.GetEnumMemberValue() == value)
                return e;
        }
        throw new InvalidProgramException($"{typeof(T)}! ConvertBack failed!");
    }
}
