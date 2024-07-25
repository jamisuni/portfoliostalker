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
using System.Reflection;
using System.Text;

namespace Pfs.ExtTransactions;

// BaseClass for different Broker Transaction Parser's... so Nordnet etc
public class BtParser
{
    string[] _headerElems = null;
    BtMap[] _map = null;

    // This is first thing called for broker CSV file, just verifying per first line header that
    // all required elements per map file are included to file... => validity of CSV file
    protected string Init(string[] headerElems, BtMap[] map)
    {
        StringBuilder sb = new();
        _headerElems = headerElems;
        _map = map;

        foreach (BtMap entry in _map)
        {
            if (entry.header == null)
                continue;

            if ( entry.header.Contains('#'))
            {
                // Later...
            }
            else if (_headerElems.FirstOrDefault(e => e == entry.header) == null)
                sb.AppendLine($"Missing [{entry.header}]");
        }
        return sb.ToString();
    }

    protected virtual string[] SplitLine(string str)
    {
        return str.Split(',');
    }

    protected (BtAction bta, Dictionary<string, string> manual) Convert2Bta(string line)
    {
        var conv = Convert(SplitLine(line));

        BtAction bta = new()
        {
            TA = conv.action,
            Orig = line,
            ErrMsg = conv.errMsg,
            BrokerAction = conv.manual[BtField.Action.ToString()],
            // These needs to be set by caller
            LineNum = -1,
            Status = BtAction.TAStatus.Unknown,
            MapCompRef = string.Empty,
        };
        return (bta, conv.manual);
    }

    // Allows to convert single broker CSV file line, per broker mapping information to BtAction
    protected (Transaction action, Dictionary<string, string> manual, string errMsg) Convert(string[] lineElems)
    {
        Transaction retTA = new();
        Dictionary<string, string> manual = new(0);

        try
        {
            foreach (BtMap entry in _map)
            {
                if (entry.formatId == BtFormat.Unknown)
                    continue;

                switch (entry.formatId)
                {
                    case BtFormat.Manual:
                        if (entry.field != BtField.Unknown)
                            manual.Add(entry.field.ToString(), Get(entry.header));
                        else
                            manual.Add(entry.header, Get(entry.header));
                        break;

                    case BtFormat.Date:
                        SetDate(entry, Get(entry.header));
                        break;

                    case BtFormat.String:
                        Set(entry.field.ToString(), Get(entry.header));
                        break;

                    case BtFormat.Decimal:
                        SetDecimal(entry, Get(entry.header));
                        break;

                    case BtFormat.Currency:
                        SetCurrency(entry, Get(entry.header));
                        break;
                }
            }
            return (retTA, manual, string.Empty);
        }
        catch (Exception ex)
        {
            return (retTA, manual, $"BtParser.Convert failed to exception [{ex.Message}]");
        }

        void SetDate(BtMap entry, string content)
        {
            if (DateOnly.TryParseExact(content, entry.formatMask, out DateOnly date))
                Set(entry.field.ToString(), date);
        }

        void SetDecimal(BtMap entry, string content)
        {
            decimal? value = ConvDecimal(content);

            if (value.HasValue)
                Set(entry.field.ToString(), value);
        }

        void SetCurrency(BtMap entry, string content)
        {
            CurrencyId currency = ConvCurrency(content);
            if (currency != CurrencyId.Unknown)
                Set(entry.field.ToString(), currency);
        }

        string Get(string header)
        {
            string[] split = header.Split('#');

            if ( split.Length == 1 )
                // Per headerElems find position for wanted field, and return it content
                return lineElems[Array.IndexOf(_headerElems, header)];

            // Example Nordnet has many "Valuutta" fields, so by using header "Hankinta-arvo#Valuutta" we get valuutta after Hankinta-Arvo
            int pos = Array.IndexOf(_headerElems, split[0]);

            if (pos < 0 || _headerElems.Length <= pos || _headerElems[pos + 1] != split[1])
                return string.Empty;
            else
                return lineElems[pos + 1];
        }

        void Set(string field, object value)
        {   // Set property using it name
            PropertyInfo propertyInfo = retTA.GetType().GetProperty(field);
            propertyInfo.SetValue(retTA, value, null);
        }
    }

    public static decimal? ConvDecimal(string content)
    {
        NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands;
        CultureInfo culture = CultureInfo.InvariantCulture;

        if (decimal.TryParse(content.Replace(",", ".").Replace(" ", ""), style, culture, out decimal value) )
            return value;
        return null;
    }

    public static CurrencyId ConvCurrency(string content) 
    {
        if (Enum.TryParse(content, out CurrencyId currencyId) )
            return currencyId;
        return CurrencyId.Unknown;
    }

    public string Convert2Debug(string[] lineElems, BtMap[] map, Transaction ta)
    {
        StringBuilder sb = new();

        for (int p = 0; ; p++)
        {
            if (p >= _headerElems.Length && p >= lineElems.Length)
                break;

            string hdr = "-missing-";
            string ln = "-missing-";
            string me = "-missing-";
            string val = "-missing-";

            if (p < lineElems.Length)
                ln = lineElems[p];

            if (p < _headerElems.Length)
            {
                hdr = _headerElems[p];

                BtMap m = map.FirstOrDefault(x => x.header == hdr);
                if (m != null)
                {
                    me = m.field.ToString();

                    var property = ta.GetType().GetProperty(m.field.ToString());
                    if (property != null)
                        val = property.GetValue(ta).ToString();
                }
            }
            sb.AppendLine($"{hdr,-20} {ln,-40} {me,-55} {val,-70}");
        }
        return sb.ToString();
    }
}

public record BtMap(BtField field, string header, BtFormat formatId, string formatMask);

public enum BtField // this maps to 'Transaction' helping BtMap to ref field on target structure
{ 
    Unknown,
    Action,
    UniqueId,
    RecordDate,
    PaymentDate,
    Note,
    ISIN,
    Market,
    Symbol,
    CompanyName,
    Currency,
    CurrencyRate,
    Units,
    McAmountPerUnit,
    McFee,
};

public enum BtFormat
{
    Unknown,        
    Manual,         // means this is to be handled "manually" by broker code
    Date, 
    String,
    Decimal,
    Currency,
}

// Created as processing Transaction entry per broker specific CSV actions
public class BtAction
{
    public Transaction TA { get; set; } = null;                 // actual general data of buy, sell, divident etc

    public string MapCompRef { get; set; } = string.Empty;      // Per broker provided iban/symbol/name info created key to refer company

    public string BrokerAction { get; set; } = string.Empty;    // Broker action name, so if unknown then shows original == TA.Action

    public string Orig {  get; set; } = string.Empty;           // original line from broker csv

    public int LineNum { get; set; } = 0;

    public string ErrMsg { get; set; } = null;                  // user readable error details 

    public TAStatus Status { get; set; } = TAStatus.Unknown;

    public enum TAStatus
    {
        Unknown = 0,
        Ready,              // accepted and tested.. ready to applying
        Acceptable,         // looks ok but havent tested yet against stalker
        Manual,             // closings cant be handled here or conflicts
        MisRate,
        Ignored,            // user ignored or unknown type
        ErrConversion,      // failed to convert action of broker CSV
        ErrTest,            // failed test apply
        ErrTestDupl,
        ErrTestUnits,
    }
}
