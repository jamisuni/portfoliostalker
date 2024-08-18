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

using System.Text;

namespace Pfs.Types;

public class Note
{
    protected string _content;

    public Note()
    {
        _content = string.Empty;
    }

    public Note(string content)
    {
        _content = content;
    }

    public string Get()
    {
        return _content;
    }

    public string GetStorageContent()
    {
        return _content;
    }

    public string GetHeader()
    {
        if ( _content.StartsWith('>') == false)
            return null;

        string hdr = _content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)[0];
        return hdr.TrimStart('>');
    }

#if false

[#NYSE$TSN#>2024-Aug: Current 0.5% is keeper, double up asap, its big business I like to own longterm! Go heavy, bite heavy!!
- largest poultry, pork and beef processor in the entire United States, with impressive international
  operations selling the company's products in over 140 countries.
- The company's operations are fully vertically integrated, from breeding stock, contract farmers, 
  feed production, processing, VAP processing, marketing and logistics.
- Some of the largest characterizing factors in the company is the fact that the Tyson family as well 
  as the Tyson Limited Partnership('TLP'), owns around 70.97% of the voting rights in the company
- 10% on pork, 36% beef, 31% chicken, rest under "prepared food"
- Historically Tyson Foods is an underperformer in times of higher economic growth(higher inflation)
- Food production is a low-margin industry, and an increase in input costs can squeeze margins

! 2024-Aug: Hoping to keep this long term, divident looks safe, debt low..so my 0.5% is all ok place here
// ending is this.. but compilet assumes preprocessor...  #]

#endif    

    public string CreateExportFormat(string sRef)
    {
        if (string.IsNullOrWhiteSpace(_content))
            return null;

        return $"[#{sRef}#{_content}{Environment.NewLine}#]{Environment.NewLine}";
    }

    public static (string sRef, Note note, string errMsg) ParseExportFormat(string content)
    {
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return ParseExportFormat(ref lines);
    }

    public static (string sRef, Note note, string errMsg) ParseExportFormat(ref string[] lines, int lineStart = 0)
    {
        StringBuilder sb = null; // if not null then everything else is also set
        int sbLines = 0;
        MarketId marketId = MarketId.Unknown;
        string symbol = string.Empty;

        if (lines[lineStart].StartsWith("[#") == false)
            return (null, null, $"Note.ParseExportFormat: Invalid call. [{lines[lineStart]}]");

        string line = lines[lineStart].Substring(2);

        int nextPos = line.IndexOf('#');

        if (nextPos < "TSX$T".Length)
            return (null, null, $"Note.ParseExportFormat: Invalid call. [{lines[lineStart]}]");

        string stockinfo = line.Substring(0, nextPos);

        (marketId, symbol) = StockMeta.TryParseSRef(stockinfo);

        if (marketId == MarketId.Unknown)
            return (null, null, $"Note.ParseExportFormat: Invalid SRef format. [{lines[lineStart]}]");

        // Here we have start and we know stock its going... so lets start collecting tuff
        sb = new StringBuilder();
        line = line.Substring(nextPos + 1);
        if (string.IsNullOrWhiteSpace(line))
            sbLines = 0;
        else
        {
            sb.AppendLine(line);
            sbLines = 1;
        }

        for (int linePos = lineStart+1; linePos < lines.Count(); linePos++ )
        {
            line = lines[linePos].TrimEnd();

            if (line.TrimStart().StartsWith("#]"))
                return ($"{marketId}${symbol}", new Note(sb.ToString()), null);

            else if (line.StartsWith("[#"))
                return (null, null, $"Note.ParseExportFormat: Double start syntax error [{lines[lineStart]}]");

            else if (sbLines >= 50)
                return (null, null, $"Note.ParseExportFormat: Over 50 lines for [{lines[lineStart]}]");

            else if (string.IsNullOrWhiteSpace(line))
            {   // empty lines look ok on notes file, but not bringing them PFS as space is limited
                continue;
            }
            else
            {
                sbLines++;
                sb.AppendLine(line);
            }
        }
        return (null, null, $"Note.ParseExportFormat: Didnt find ending for [{lines[lineStart]}]");
    }
}
