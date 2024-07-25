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
using System.Collections.Immutable;

namespace Pfs.Helpers;

public class CmdParser      // Note! Keep it now on separate helpers library.. used from configs that I dont want to use shared
{
    /* Eats content and template, and returns dictionary w elements found
     * - Each element has: name 
     * - Does it need optional elements? name_opt
     * - Can have sync elements, those actually just example C= 
     *   and returned as any other element
     */

    // On success returns template matched fields (incl 'cmd'), if fails returns help type info as warning
    public static Result<Dictionary<string, string>> Parse(string cmd, ImmutableArray<string> templates)
    {   // Supports <enums> and [multi part field] ... later add [#multi,enums]
        if (string.IsNullOrWhiteSpace(cmd))
            return RetInvalidCmd();

        char[] delimiters = { ' ' };
        string[] cmdSplit = cmd.Split(delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string templ = templates.FirstOrDefault(t => t.StartsWith(cmdSplit[0]));

        if (templ == null) // Could not find any template matching -> return list of all cmd's as help
            return RetInvalidCmd();

        string[] templSplit = templ.Split(delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Dictionary<string, string> ret = new();
        ret.Add("cmd", cmdSplit[0]);

        string multipart = string.Empty;    // if has content then collecting [many spaces]

        int templPos = 0; // As command can have [...] more spaces so needs separate counters

        // loop all parts of given cmd, and compare if matches requirements of template
        for ( int cmdPos = 1; cmdPos< cmdSplit.Length; cmdPos++ )
        {
            if ( string.IsNullOrWhiteSpace(multipart) == false )
            {   // part of long "[message with many spaces]" 
                multipart += " " + cmdSplit[cmdPos];

                if ( multipart.EndsWith(']'))
                {
                    ret.Add(templSplit[templPos], multipart.Substring(0, multipart.Length - 1));
                    multipart = string.Empty;
                }
                continue;
            }

            templPos++;
            
            if (templSplit[templPos].StartsWith("<#"))
            {   // <#provider> example means prov1,prov2 is possible as value
                throw new NotImplementedException("!!!LATER!!! Parse <# for multicheck enums");
            }
            else if (templSplit[templPos].StartsWith('<'))
            {   // a <enum> needs special handling
                Result<Array> enumResp = VerifyEnumValues(templSplit[templPos], cmdSplit[cmdPos]);

                if ( enumResp.Fail )
                    return new FailResult<Dictionary<string, string>>((enumResp as FailResult<Array>).Message);

                List<string> enumList = enumResp.Data.Cast<object>().Select(x => x.ToString()).ToList();
                ret.Add(templSplit[templPos], string.Join(",", enumList));
            }
            else if (cmdSplit[cmdPos].StartsWith('['))
            {
                if (cmdSplit[cmdPos].EndsWith(']'))
                    ret.Add(templSplit[templPos], cmdSplit[cmdPos].Substring(1, cmdSplit[cmdPos].Length -2 ));
                else
                    multipart = cmdSplit[cmdPos].Substring(1);
            }
            else // later can add some fancy format validations like max length etc, but not rush...
            {
                ret.Add(templSplit[templPos], cmdSplit[cmdPos]);
            }
        }

        if (ret.Count < templSplit.Count())
        {
            if (templSplit[ret.Count].StartsWith('<'))
            {
                Type enumType = GetEnumTypeFromTempl(templSplit[ret.Count]);

                return new FailResult<Dictionary<string, string>>($"Missing params: {templ} [{templSplit[ret.Count]},{string.Join(",", Enum.GetNames(enumType))}]");
            }
            else
                return new FailResult<Dictionary<string, string>>($"Missing params: {templ}");
        }

        // If it fails return failure with what to do next helper..

        return new OkResult<Dictionary<string, string>>(ret);

        FailResult<Dictionary<string, string>> RetInvalidCmd()
        {
            return new FailResult<Dictionary<string, string>>($"Invalid Cmd [commands,{GetAllCommands(templates)}]");
        }
    }

    protected static Type GetEnumTypeFromTempl(string enumTempl)
    {
        return enumTempl.Replace("<", "").Replace(">", "") switch
        {
            "provider" => typeof(ExtProviderId),
            "market" => typeof(MarketId),
            _ => throw new ArgumentException($"{enumTempl} is not supported")
        };
    }

    public static Result<Array> VerifyEnumValues(string enumTempl, string input)
    {
        Type enumType = GetEnumTypeFromTempl(enumTempl);

        string[] inputValues = input.Split(',');
        Array enumValues = Array.CreateInstance(enumType, inputValues.Length);

        for (int i = 0; i < inputValues.Length; i++)
        {
            if (Enum.TryParse(enumType, inputValues[i], true, out object value))
                enumValues.SetValue(value, i);
            else
                return new FailResult<Array>($"{inputValues[i]} not supported! [{enumTempl},{string.Join(",", Enum.GetNames(enumType))}]");
        }

        return new OkResult<Array>(enumValues);
    }

    protected static string GetAllCommands(ImmutableArray<string> templates)
    {
        List<string> all = new();

        foreach (string t in templates)
            all.Add(t.Split(' ')[0]);

        return string.Join(',', all);
    }
}
