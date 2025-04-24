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

using System.Text.RegularExpressions;

namespace Pfs.Types;

/* Rules of trade:
 * 
 * On string, do nor allow:
 * {} as used by C#
 * [] as used by concat cmd Lines
 * = as used by concat cmd lines
 * " as part of C#
 * 
 * => Preferred in code delimeter is ASCII 31 (0x1F) Unit Separator (that is pretty much zero use on writing)
 */

public class Limit
{
    public const int CompName = 32;
    public const int PfName = 12;
    public const int Symbol = 8;    // 'ADC.PR.A' == 8 chars
}

public enum ValidateId : int
{
    Unknown = 0,
    Symbol,
    SRef,
    ISIN,
    CompName,
    PurhaceId,
    TradeId,
    PfName,
    StalkerNote,
    Holidays,
    SectorName,
    SectorField,
    FilterName,
    ProvPrivKey,    // just maximum limitation
}

public class Validate
{
    protected const string _allowedChars = " _#-!@$%^&*()+,.<>:;?~";

    static public Result Str(ValidateId id, string content)          // !!!THINK!!! Could also same time handle raise to upper for symbol, and remove trailing/ending spaces etc.. and return cleanup version
    {
        switch (id)
        {
            case ValidateId.Symbol:
                {
                    if (string.IsNullOrWhiteSpace(content) ||
                        content.Length > Limit.Symbol ||
                        new Regex(@"^[A-Z][A-Z0-9.\-]{0,7}$").IsMatch(content) == false)
                        return new FailResult<string>(FormatMsg(id));

                    return new OkResult<string>(content);
                }

            case ValidateId.SRef:
                {   // Need to have $ separating market and symbol. Market must match to regex [A-Z]{1,6} and symbol checked with recursive call to ValidateId.Symbol

                    if (string.IsNullOrWhiteSpace(content) ||
                        content.Length > Limit.Symbol + 6 + 1 || // 6 chars for market, 1 for $, and 8 for symbol
                        new Regex(@"^[A-Z]{1,6}\$[A-Z][A-Z0-9.\-]{0,7}$").IsMatch(content) == false)
                        return new FailResult<string>(FormatMsg(id));

                    return new OkResult<string>(content);
                }

            case ValidateId.CompName:
                {
                    // Had to add ' to company names as little company named ""MCD,McDonald's Corporation"" uses 
                    // And '/' as CODI,D/B/A Compass Diversified Holdings Shares of Beneficial Interest
                    char? invalid = Local_FindFirstNotAllowedChar(content, _allowedChars + "'/");

                    if (invalid != null)
                        return new FailResult($"A {invalid.Value} is not allowed character for Company name!");

                    return new OkResult();
                }

            case ValidateId.SectorName:
            case ValidateId.SectorField:
                {   // ReportFilters uses _unitSeparator = ';' so cant allow that to name itself as XML doesnt like unit-separator
                    char? invalid = Local_FindFirstNotAllowedChar(content, _allowedChars.Replace(";", ""));

                    if (invalid != null)
                        return new FailResult($"A {invalid.Value} is not allowed character");

                    if (content.Length > SSector.MaxNameLen ) // same len limit for both
                        return new FailResult($"A {invalid.Value} is too long");

                    return new OkResult();
                }

            default:
                {
                    char? invalid = Local_FindFirstNotAllowedChar(content, _allowedChars);

                    if (invalid != null)
                        return new FailResult($"A {invalid.Value} is not allowed character");

                    return new OkResult();
                }
        }

        char? Local_FindFirstNotAllowedChar(string content, string allowedSpecialChars)
        {
            // Loop through each character in the string
            foreach (char c in content)
            {
                // Check if the character is a letter, a digit, or an allowed character
                if (!char.IsLetterOrDigit(c) && !allowedSpecialChars.Contains(c))
                    // Return the first character that is not allowed
                    return c;
            }
            // Return null if no such character is found
            return null;
        }
    }

    static public string FormatMsg(ValidateId id)
    {
        switch ( id )
        {
            case ValidateId.Symbol: return "Letter+Letter/Numbers";
        }
        return $"FormatMsg({id}) is missing msg!";
    }
}
