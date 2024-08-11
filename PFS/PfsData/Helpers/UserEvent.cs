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

using System.Runtime.Serialization;

using Pfs.Types;

namespace Pfs.Data;

// Presents single user event as string format thats easy/compact to store
public class UserEvent
{
    // 'Field' is 'Id' = 'Value'
    // ASCII 31 (0x1F) Unit Separator => used to separate 'Field's to create Content string
    const char _unitSeparator = ((char)31);
    const string _dateFormat = "yyMMdd";
    protected string _content = string.Empty;

    [DataContract]
    public enum EvFieldId
    {
        [EnumMember(Value = "T")]
        Type,                       // UserEventType (OrderExpired, etc)
        [EnumMember(Value = "D")]
        Date,                       // yyMMdd
        [EnumMember(Value = "P")]
        Portfolio,                  // PfName (just implement so that if PfName gets updated then wrong name is just ignored)
        [EnumMember(Value = "R")]
        SRef,
        [EnumMember(Value = "V")]
        Value,                      // Decimal "0.00" (AlarmValue, OrderValue)
        [EnumMember(Value = "U")]
        Units,                      // Decimal "0.00" (OrderUnits)
        [EnumMember(Value = "EC")]
        EodClose,                   // Decimal "0.00"
        [EnumMember(Value = "EL")]
        EodLow,                     // Decimal "0.00"
        [EnumMember(Value = "EH")]
        EodHigh,                    // Decimal "0.00"

    }

    public UserEvent(string storageFormat)
    {
        _content = storageFormat;
    }

    static public UserEvent Create(Dictionary<EvFieldId, object> prms)
    {
        List<string> strs = new();

        foreach ( KeyValuePair<EvFieldId, object> kvp in prms )
        {
            string value = "?";

            switch ( kvp.Key)
            {
                case EvFieldId.Type:
                    value = ((UserEventType)kvp.Value).GetEnumMemberValue();
                    break;

                case EvFieldId.SRef:
                case EvFieldId.Portfolio:
                    value = kvp.Value.ToString();
                    break;

                case EvFieldId.Date:
                    value = ((DateOnly)kvp.Value).ToString(_dateFormat);
                    break;

                case EvFieldId.Value:
                case EvFieldId.Units:
                case EvFieldId.EodClose:
                case EvFieldId.EodLow:
                case EvFieldId.EodHigh:
                    value = ((decimal)kvp.Value).ToString("0.00");
                    break;

                default:
                    throw new MissingFieldException($"UserEvent.Create is missing {kvp.Key.ToString()}");
            }

            strs.Add($"{kvp.Key.GetEnumMemberValue()}={value}");
        }

        return new UserEvent(string.Join(_unitSeparator, strs));
    }

    public Dictionary<EvFieldId, object> GetFields()
    {
        Dictionary<EvFieldId, object> ret = new();
        string[] fields = _content.Split(_unitSeparator);

        foreach (string field in fields)
        {
            string[] split = field.Split('=');
            EvFieldId id = EnumExtensions.ConvertBack<EvFieldId>(split[0]);
            object value = null;

            switch ( id )
            {
                case EvFieldId.Type:
                    value = EnumExtensions.ConvertBack<UserEventType>(split[1]);
                    break;

                case EvFieldId.SRef:
                case EvFieldId.Portfolio:
                    value = split[1];
                    break;

                case EvFieldId.Date:
                    value = DateOnly.ParseExact(split[1], _dateFormat);
                    break;

                case EvFieldId.Value:
                case EvFieldId.Units:
                case EvFieldId.EodClose:
                case EvFieldId.EodLow:
                case EvFieldId.EodHigh:
                    value = decimal.Parse(split[1]);
                    break;

                default:
                    throw new MissingFieldException($"UserEvent.GetFields is missing {field}");
            }
            ret.Add(id, value);
        }
        return ret;
    }

    public string GetStorageFormat()
    {   // Outside usage of actual string is limited to storing it
        return _content;
    }
}
