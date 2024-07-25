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

using Pfs.Types;
using System.Globalization;

namespace Pfs.Shared.Stalker;

// Handles single parameter for StalkerAction, validating and parsing it out
public class StalkerParam
{
    public string Name { get; internal set; } = string.Empty;

    protected string Value { get; set; } = string.Empty;

    protected string Template { get; set; } = string.Empty;

    public ParamType Type { get; internal set; } = ParamType.Unknown;

    public Result Error { get; internal set; } = null; // This should be OK only if acceptable 'Value' is set

    public StalkerParam(string template) // See StalkerActionTemplate for template
    {
        string[] split = template.Split('=');

        if (split.Count() != 2)
            return;
            
        Name = split[0];
        Template = split[1];

#if false                                                   // 2024 Nice idea, but does this work or is it even used?? commented off => follow up..
        if (Name.StartsWith('+') == true)
            // indicates optional parameter, that is OK by default even not set.. so either empty or needs to be set
            Error = StalkerError.OK;
#endif

        string[] templateSegments = Template.Split(':');
        Type = (ParamType)Enum.Parse(typeof(ParamType), templateSegments[0]);
    }

    public Result Parse(string param)
    {
        /* Double:0.01:100.00      MIN,MAX          => double on defined range if min/max set.. barely used atm as now all Decimals
            * SRef                                     => "MarketId$symbol"
            * String:0:100:StalkerNote                 => string with min/max length, possible char set checking against named char set template
            * Date                                     => "yyyy-MM-dd"
            * StockAlarmType                           => Per enum values
            * StockOrderType                           => Per enum values
            * PurhaceId                                => special string w support for 'empty' that changes it to use random uniqueID, or can be user defined
            * TradeId                                  => unique value for Sales ala Trades
            * Decimal:0.01:100.00                      => for currency values
            * SectorId                                 => 0..2
            * FieldId                                  => 0..17
            * SectorName                               => Use validation
            * FieldName                                => Use validation
            * CurrencyId                               => Per enum values
            */

        if (param.Contains('=') == true)
        {
            if (LocalCheckName(param) == false)
                // If param contains '=' then name must be match
                return new FailResult($"Given param {param} doesnt match {Name}=value");

            // Rest of code doesnt care name...
            param = param.Split('=')[1];
        }

        string[] templateSegments = Template.Split(':');

        switch ( Type )
        {
            case ParamType.Decimal:
                {
                    decimal value = LocalGetDecimal(templateSegments, param);

                    if (Error != null)
                        return Error;

                    Value = value.ToString();
                    return Error = new OkResult();
                }

            case ParamType.SRef:
                {
                    string value = LocalGetString(templateSegments, param);

                    if (Error != null)
                        return Error;

                    string[] split = value.Split("$"); // "MarketId$symbol"

                    if (split.Length != 2 || value.IndexOf(' ') >= 0 || Enum.TryParse<MarketId>(split[0], out _) == false)
                        return new FailResult($"{Name} is supposed to be market$symbol, {value} doesnt look proper!");

                    Value = value;
                    return Error = new OkResult();
                }

            case ParamType.String:
                {
                    string value = LocalGetString(templateSegments, param);

                    if (Error != null)
                        return Error;

                    Value = value;
                    return Error = new OkResult();
                }

            case ParamType.Date:
                {
                    DateOnly? date = LocalGetDate(param);

                    if (date.HasValue == false)
                        return new FailResult($"{Name} is supposed to be yyyy-MM-dd and {param} isnt!");

                    Value = date.Value.ToString("yyyy-MM-dd");
                    return Error = new OkResult();
                }

            case ParamType.StockAlarmType:
                {
                    if (Enum.TryParse<SAlarmType>(param, out SAlarmType alarmType) == false)
                        return new FailResult($"{Name} is supposed to be SAlarmType and {param} isnt!");

                    Value = alarmType.ToString();
                    return Error = new OkResult();
                }

            case ParamType.StockOrderType:
                {
                    if (Enum.TryParse<SOrder.OrderType>(param, out SOrder.OrderType orderType) == false)
                        return new FailResult($"{Name} is supposed to be SOrder.OrderType and {param} isnt!");

                    Value = orderType.ToString();
                    return Error = new OkResult();
                }

            case ParamType.PurhaceId:
                {
                    string value = LocalGetPurhaceId(param);

                    if (Error != null)
                        return Error;

                    Value = value;
                    return Error = new OkResult();
                }

            case ParamType.TradeId:
                {
                    string value = LocalGetTradeId(param);

                    if (Error != null)
                        return Error;

                    Value = value;
                    return Error = new OkResult();
                }

            case ParamType.SectorId:
                {
                    if (int.TryParse(param, out int value) == false)
                        return Error = new FailResult($"{Name}={param} must be int!");
                        
                    if ( value < 0 || value >= SSector.MaxSectors )
                        return Error = new FailResult($"{Name}={param} out of range 0-{SSector.MaxSectors-1}");

                    Value = value.ToString();
                    return Error = new OkResult();
                }

            case ParamType.FieldId:
                {
                    if (int.TryParse(param, out int value) == false)
                        return Error = new FailResult($"{Name}={param} must be int!");

                    if (value < 0 || value >= SSector.MaxFields)
                        return Error = new FailResult($"{Name}={param} out of range 0-{SSector.MaxFields - 1}");

                    Value = value.ToString();
                    return Error = new OkResult();
                }

            case ParamType.SectorName:
                {
                    if ( string.IsNullOrWhiteSpace(param) )
                        return Error = new FailResult($"{Name}={param} must contain something!");

                    Result res = Validate.Str(ValidateId.SectorName, param);

                    if ( res.Fail )
                        return Error = new FailResult($"{Name}={param} failed validation! [{(res as FailResult).Message}]");

                    Value = param;
                    return Error = new OkResult();
                }

            case ParamType.FieldName:
                {
                    if (string.IsNullOrWhiteSpace(param))
                        return Error = new FailResult($"{Name}={param} must contain something!");

                    Result res = Validate.Str(ValidateId.SectorField, param);

                    if (res.Fail)
                        return Error = new FailResult($"{Name}={param} failed validation! [{(res as FailResult).Message}]");

                    Value = param;
                    return Error = new OkResult();
                }

            case ParamType.CurrencyId:
                {
                    if (Enum.TryParse<CurrencyId>(param, out CurrencyId currency) == false)
                        return new FailResult($"{Name} is supposed to be CurrencyId and {param} isnt!");

                    Value = currency.ToString();
                    return Error = new OkResult();
                }
        }
        return (Error = new FailResult($"{Type} is unhandled by Parse"));

        // LOCAL FUNCTIONS

        bool LocalCheckName(string param)
        {
            string[] split = param.Split('=');

            if (split.Count() != 2)
                return false;

            if (split[0] != Name)
                return false;

            return true;
        }

        decimal LocalGetDecimal(string[] templateSegments, string param)
        {
            decimal value;

            if (decimal.TryParse(param, out value) == false)
            {
                Error = new FailResult($"{Name} failed to parse {param} as decimal");
                return 0;
            }

            if (templateSegments.Count() >= 2 && templateSegments[1].Length > 0)
            {
                decimal min = decimal.Parse(templateSegments[1]);

                if (value < min)
                {
                    Error = new FailResult($"{Name} minimum is {min}");
                    return 0;
                }
            }

            if (templateSegments.Count() >= 3 && templateSegments[2].Length > 0)
            {
                decimal max = decimal.Parse(templateSegments[2]);

                if (value > max)
                {
                    Error = new FailResult($"{Name} maximum is {max}");
                    return 0;
                }
            }

            return value;
        }

        string LocalGetString(string[] templateSegments, string param)  // String:1:20:CharSet
        {
            if (templateSegments.Count() >= 2 && templateSegments[1].Length > 0)
            {
                int minLength = int.Parse(templateSegments[1]);

                if ( minLength > param.Length )
                {
                    Error = new FailResult($"{Name} minimum length is {minLength}");
                    return string.Empty;
                }
            }

            if (templateSegments.Count() >= 3 && templateSegments[2].Length > 0)
            {
                int maxLength = int.Parse(templateSegments[2]);

                if (maxLength < param.Length)
                {
                    Error = new FailResult($"{Name} maximum length is {maxLength}");
                    return string.Empty;
                }
            }

            if (templateSegments.Count() >= 4 && templateSegments[3].Length > 0)
            {
               ValidateId charSet = (ValidateId)Enum.Parse(typeof(ValidateId), templateSegments[3]);

                Result temp;

                if ((temp = Validate.Str(charSet, param)).Ok == false)
                {
                    Error = temp;
                    return string.Empty;
                }
            }

            return param;
        }

        string LocalGetPurhaceId(string param)
        {
            if ( param.Length > 50 )
            {
                Error = new FailResult($"PurhaceId max length is 50");
                return string.Empty;
            }

            if ( param.Length == 0 )
                // Special case, if client didnt define PurhaceId then we assign a unique ID for it
                return "PID:" + Guid.NewGuid().ToString();

            Result temp;

            if ((temp = Validate.Str(ValidateId.PurhaceId, param)).Ok == false)
            {
                Error = temp;
                return string.Empty;
            }

            return param;
        }

        string LocalGetTradeId(string param)
        {
            if (param.Length > 50)
            {
                Error = new FailResult($"TradeId max length is 50");
                return string.Empty;
            }

            if (param.Length == 0)
                // Special case, if client didnt define TradeId then we assign a unique ID for it
                return "TID:" + Guid.NewGuid().ToString();

            Result temp;

            if ((temp = Validate.Str(ValidateId.TradeId, param)).Ok == false)
            {
                Error = temp;
                return string.Empty;
            }
            return param;
        }

        DateOnly? LocalGetDate(string param)
        {
            DateOnly result;
            
            if (DateOnly.TryParseExact(param, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result) == false)
                return null;

            return result;
        }
    }

    public static implicit operator string(StalkerParam param) { return param.Value; }
    public static implicit operator int(StalkerParam param) { return int.Parse(param.Value); }
    public static implicit operator double(StalkerParam param) { return double.Parse(param.Value); }
    public static implicit operator decimal(StalkerParam param) { return decimal.Parse(param.Value); }
    public static implicit operator DateOnly(StalkerParam param) { return DateOnly.ParseExact(param.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
    public static implicit operator SAlarmType(StalkerParam param) {  return (SAlarmType)Enum.Parse(typeof(SAlarmType), param.Value); }
    public static implicit operator SOrder.OrderType(StalkerParam param) { return (SOrder.OrderType)Enum.Parse(typeof(SOrder.OrderType), param.Value); }
    public static implicit operator CurrencyId(StalkerParam param) { return (CurrencyId)Enum.Parse(typeof(CurrencyId), param.Value); }

    public enum ParamType : int
    {
        Unknown = 0,
        Decimal,        // All currency related values
        String,
        Date,
        SRef,
        StockAlarmType,
        StockOrderType,
        PurhaceId,
        TradeId,
        SectorId,
        FieldId,
        SectorName,
        FieldName,
        CurrencyId,
    }
}
