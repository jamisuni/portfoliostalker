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

using System.Text.Json.Serialization;
using System.Text.Json;

namespace Pfs.Types;

// Mainly client side, presents one tracked Stock on internal data format where Alarm's & StockMeta etc are available for specific Stock STID,

[JsonConverter(typeof(SAlarmJsonConverter))]
public abstract class SAlarm
{
    /* => I want to have more complex alarms like ones those track EOD and give alarm on 20% drop ala "sale off warnings"
     * 
     * - Remember! There still needs to be stalker CMD to set/edit alarm, so those are set manually to template...
     *              => Type=AlarmType Level=Fixed Params=Unchecked,string,content,that,only,alarm,knows
     * 
     * - For new EOD received, that triggers alarm checking... pass in also PrevEod and NewEod allowing more complex logic
     *   like example can see date changes to new year etc
     * 
     * Decisions:
     * 
     * - Limit decimals on level to 0.00, and make sure there is no duplicates on same level!
     * 
     * - Alarm itself can decide it storage format, and knows how to unpack it.. per each alarm type
     *      - Also should have them unpack from ~wave alarms format by alarms itself
     *      
     * - 'LocalCheckAlarms' changed on EOD receiving so that its alarm itself that decides if Events should be generated
     *   (Note! Has there atm checking w PrevClose that only gives alarm when alarm line is break but doesnt repeat)
     *          (update.Data.Low <= alarm.Value && alarm.Value <= update.Data.PrevClose)
     *       
     * Types:
     * 
     * - TrailingStop, has Params="ATH=20"     to protect gains   https://www.investopedia.com/terms/t/trailingstop.asp
     * 
     * - Own alarm type for support levels? Those are not to be included to % showings but just give event
     * 
     * - Own alarm type for "House Money"? With taxes% included.. allows to sell when has go enough up to bring original inv back
     *   and still leave half or 2/3 left...
     *   
     * - Time period alarms.. like 10 years investment event to indicate that stock is hopping over 40% fin tax rate
     */

    public virtual SAlarmType AlarmType { get; set; } = SAlarmType.Unknown;
    public virtual decimal Level { get; set; } // This is static, and used as ref ID also
    public virtual string Note { get; set; }
    public virtual string Prms { get; set; }

    public virtual (string ev, bool needSave) CheckWithNewEod(FullEOD newEod, FullEOD oldEod)
    {
        return (string.Empty, false);
    }

    // This returns 'dynLevel' that often is 'Level' but for 'TrailingSellP' could be ATH -20% example
    public virtual (decimal dynLevel, decimal procent) GetAlarmDistance(decimal latestClose)
    {
        return (0, 0);
    }

    public virtual string GetStorageFormat()
    {
        return $"{AlarmType}\x1F{decimal.Round(Level, 2)}\x1F{Note}\x1F{Prms}";
    }

    public virtual SAlarm CreateFromStorageFormat(string storage)
    {
        string[] split = storage.Split('\x1F');

        if (split.Count() < 2)
            return null; // Handle this! Dont waste full data for broken alarm!

        SAlarmType alarmType = Enum.Parse<SAlarmType>(split[0]);
        decimal level = DecimalExtensions.Parse(split[1]);
        string note = string.Empty;
        string prms = string.Empty;

        if ( split.Count() > 2 )
            note = split[2];

        if ( split.Count() > 3 )
            prms = split[3];

        return Create(alarmType, level, note, prms);
    }

    // Decision! FE knows alarms, and knows what fields(types) needs to show what one of them
    //           and how to back entered information to alarm specific way to prms field

    public static SAlarm Create(SAlarmType aType, decimal level, string note, string prms)
    {
        switch ( aType )
        {
            case SAlarmType.Over:
                return SAlarmOver.Create(level, note, prms);

            case SAlarmType.Under:
                return SAlarmUnder.Create(level, note, prms);

        }
        return null;
    }

    public virtual SAlarm DeepCopy()
    {
        return Create(AlarmType, Level, new string (Note), new string (Prms));
    }
}

public class SAlarmUnder : SAlarm
{
    public override SAlarmType AlarmType { get; set; } = SAlarmType.Under;
    public override decimal Level { get; set; }
    public override string Note { get; set; }
    public override string Prms { get; set; }

    public override (string ev, bool needSave) CheckWithNewEod(FullEOD newEod, FullEOD oldEod)
    {
        return (string.Empty, false);
    }

    public override (decimal dynLevel, decimal procent) GetAlarmDistance(decimal latestClose)
    {
        return (dynLevel: Level, procent: (Level - latestClose) / latestClose * 100);
    }

    public static SAlarmUnder Create(decimal level, string note, string prms)
    {
        return new SAlarmUnder()
        {
            Level = level,
            Note = note,
        };
    }
}

public class SAlarmOver : SAlarm
{
    public override SAlarmType AlarmType { get; set; } = SAlarmType.Over;
    public override decimal Level { get; set; }
    public override string Note { get; set; }
    public override string Prms { get; set; }

    public override (string ev, bool needSave) CheckWithNewEod(FullEOD newEod, FullEOD oldEod)
    {
        return (string.Empty, false);
    }

    public override (decimal dynLevel, decimal procent) GetAlarmDistance(decimal latestClose)
    {
        return (dynLevel: Level, procent: (latestClose - Level) / latestClose * 100);
    }

    public static SAlarmOver Create(decimal level, string note, string prms)
    {
        return new SAlarmOver()
        {
            Level = level,
            Note = note,
        };
    }
}

public enum SAlarmType : int
{
    Unknown = 0,
    Under,
    Over,
    TrailingSellP,
}

public static class SAlarmTypeExtensions
{
    public static bool IsOverType(this SAlarmType alarmType)
    {
        switch ( alarmType)
        {
            case SAlarmType.Over: 
                return true;
        }
        return false;
    }

    public static bool IsUnderType(this SAlarmType alarmType)
    {
        switch (alarmType)
        {
            case SAlarmType.Under:
            case SAlarmType.TrailingSellP:
                return true;
        }
        return false;
    }
}

/* Auts that took some effort, starting from rookie mistake for not making variables to params {get;set;}
 * Anyway... as SStock has List<SAlarm> those are abstract classes and actually are different derived 
 * classes presenting a actual alarm types.. these needs custom read/write handling for each alarm
 * type to support proper JSON conversions!. 
 */

public class SAlarmJsonConverter : JsonConverter<SAlarm>    // !!!CODE!!!
{
    public override SAlarm Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        int alarmType = 0;
        decimal level = 0;
        string note = "";
        string prms = "";

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return SAlarm.Create((SAlarmType)alarmType, level, note, prms);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            string propertyName = reader.GetString();

            reader.Read();

            switch (propertyName)
            {
                case "AlarmType":
                    alarmType = reader.GetInt32();
                    break;
                case "Level":
                    level = reader.GetDecimal();
                    break;
                case "Note":
                    note = reader.GetString();
                    break;
                case "Prms":
                    prms = reader.GetString();
                    break;
                default:
                    throw new JsonException();
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, SAlarm value, JsonSerializerOptions options)
    {
        if (value is SAlarmUnder sAlarmUnder)
        {
            JsonSerializer.Serialize(writer, sAlarmUnder, options);
        }
        else if (value is SAlarmOver sAlarmOver)
        {
            JsonSerializer.Serialize(writer, sAlarmOver, options);
        }
        else
        {
            // Serialize the value using the default serialization
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
