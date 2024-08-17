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
using System.Text;

namespace Pfs.ExtTransactions;

// http://nordnet.fi/ CSV importing to PFS
public class BtNordnet : BtParser
{
    /*
                    TRADE-BUY       AOT-BUY         TRADE-DIV       AOT-DIV         M&A-"BUY"           DECIM-CUT           CLOSE (???)
                    ==========      ========        =========       =======         =========           =========           =====
    Id	            1687464777	    934451172       1725357907      1476793749      1109166543          1109168837          1109166541
    Kirjauspäivä	2024-04-26	    2021-09-16      2024-06-04      2023-09-18      2022-07-07          2022-07-07          2022-07-07
    Kauppapäivä     2024-04-26	    2021-09-16      2024-05-06      2023-08-30      2022-07-04          2022-07-07          2022-07-04
    Maksupäivä      2024-04-30	    2021-09-20      2024-06-03      2023-09-15      2022-07-04          2022-07-07          2022-07-04
    Salkku          50806520	    7545734         50806520        7545734         7545734             7545734             7545734
    Tapahtumatyyppi OSTO	        OSTO            OSINKO          OSINKO          VAIHTO AP-JÄTTÖ     DESIM KIRJAUS OTTO  VAIHTO AP-OTTO
    Arvopaperi      INTC	        ABX             INTC            ABX             MATV                MATV                NP.US/X
    ISIN            US4581401001	CA0679011084    US4581401001    CA0679011084    US8085411069        US8085411069        US6400791090
    Määrä           70	            100             70              300             67,9                0,9                 50
    Kurssi          31,71	        23,7
    Korko           0	            0
    Kokonaiskulut   8	            29,82
    Valuutta        EUR	            CAD
    Summa           -2 084,68	    -2 399,82       8,07            30              0                   21,06               0
    Valuutta        EUR	            CAD             EUR             USD             USD                 USD                 USD
    Hankinta-arvo   2 228,25	    2 399,82                                        1 658,3
    Valuutta        USD	            CAD                                             USD
    Tulos           0	            0                                               0                   -0,9204             0
    Valuutta        USD	            CAD                                             USD                 USD                 USD
    Kokonaismäärä   70	            280             0               0               67,9                67                  0
    Saldo           292,03	        6 532           307,94          25 889,6399     824,6299            824,6299            824,6299
    Vaihtokurssi    0,9355
    Tapahtumateksti                                 0.125 USD/OS    CA 0.1 USD/OS   Exc 1 for 1,358s    Fractional          Exchange 1 share for 1,358 shares
    Mitätöintipäivä
    Laskelma        1947023784      762729614
    Vahvistusnumero 1947023784      762729614       1955057276      1897288666      1810457252          1810457759          1810457252
    Välityspalkkio  8               29,82
    Valuutta        EUR             CAD
    Viitevaluuttakurssi             0,6728                          0,9186                              0,9823              
    */
    protected readonly static BtMap[] _map = [
        new BtMap(BtField.Action,           "Tapahtumatyyppi",          BtFormat.Manual,        null),          // Tapahtumatyyppi	    OSTO
        new BtMap(BtField.UniqueId,         "Vahvistusnumero",          BtFormat.String,        null),          // Vahvistusnumero      1687464777 (same as export pdf top-right corner)
        new BtMap(BtField.RecordDate,       "Kauppapäivä",              BtFormat.Date,          "yyyy-MM-dd"),  // Kauppapäivä	        2024-04-26	
        new BtMap(BtField.PaymentDate,      "Maksupäivä",               BtFormat.Date,          "yyyy-MM-dd"),  // Maksupäivä	        2024-04-30
        new BtMap(BtField.Note,             "Tapahtumateksti",          BtFormat.String,        null),          // Tapahtumateksti	    
        new BtMap(BtField.ISIN,             "ISIN",                     BtFormat.String,        null),          // ISIN	                US4581401001
        new BtMap(BtField.Market,           null,                       BtFormat.Unknown,       null),          // --doesnt support--
        new BtMap(BtField.Symbol,           "Arvopaperi",               BtFormat.String,        null),          // Arvopaperi	        INTC	
        new BtMap(BtField.CompanyName,      null,                       BtFormat.Unknown,       null),          // --doesnt support--
        new BtMap(BtField.Units,            "Määrä",                    BtFormat.Decimal,       null),          // Määrä	            70
        new BtMap(BtField.McAmountPerUnit,  "Kurssi",                   BtFormat.Decimal,       null),          // Kurssi	            31,71
        new BtMap(BtField.McFee,            "Kokonaiskulut",            BtFormat.Decimal,       null),          // Kokonaiskulut        8
        new BtMap(BtField.Currency,         "Hankinta-arvo#Valuutta",   BtFormat.Currency,      null),          // Valuutta	            USD	
        new BtMap(BtField.CurrencyRate,     "Vaihtokurssi",             BtFormat.Decimal,       null),          // Vaihtokurssi         0,9355
        // Following ones are for handling here to do some fixing per account/currency types
        new BtMap(BtField.Unknown,          "Kokonaiskulut#Valuutta",   BtFormat.Manual,        null),          // <- if HC then recalc McFee
        new BtMap(BtField.Unknown,          "Viitevaluuttakurssi",      BtFormat.Manual,        null),          // <- if 'Vaihtokurssi' is missing then use this
        new BtMap(BtField.Unknown,          "Summa",                    BtFormat.Manual,        null),          // 
        new BtMap(BtField.Unknown,          "Summa#Valuutta",           BtFormat.Manual,        null),          // 
        new BtMap(BtField.Unknown,          "Hankinta-arvo",            BtFormat.Manual,        null),          // 
        new BtMap(BtField.Unknown,          "Kokonaismäärä",            BtFormat.Manual,        null),          // 
    ];

    protected CurrencyId _homeCurrency = CurrencyId.Unknown;

    public string Convert2Debug(string line, Transaction ta)
    {
        return Convert2Debug(SplitLine(line), _map, ta);
    }

    public static string Convert2RawString(byte[] byteContent)
    {
        return Encoding.Unicode.GetString(byteContent);
    }

    protected override string[] SplitLine(string str)
    {
        return str.TrimEnd(['\n', '\r']).Split('\t');
    }

    // Note! Bit non-standard, but by keeping 'this' can repeat conversions w origLines (so think this function also as "init")
    public Result<List<BtAction>> InitAndConvert(byte[] byteContent, CurrencyId homeCurrency)
    {
        _homeCurrency = homeCurrency;
        int lineNum = 0;
        List<BtAction> ret = new();

        using (StringReader reader = new StringReader(Convert2RawString(byteContent)))
        {
            string line = reader.ReadLine();
            string[] headerElems = SplitLine(line);

            if (headerElems[0].Contains("Id"))
                // Start of file has some invisible fileformat control characters... those just nasty to handle, 
                // so trusting here that first element is Id (with potential control chars) so well fix on fly!
                headerElems[0] = "Id";

            string err = Init(headerElems, _map);

            if (string.IsNullOrEmpty(err) == false)
                return new FailResult<List<BtAction>>(err);

            while ((line = reader.ReadLine()) != null)
            {
                lineNum++;

                if (line.StartsWith('#'))
                    continue;

                (BtAction bta, Dictionary<string, string> manual) = Convert2Bta(line);

                bta.TA.Action = ConvAction(bta.BrokerAction);

                if (bta.TA.Symbol.EndsWith("/X"))
                    // Nordnet seams use this /X for old already gone symbols
                    bta.TA.Symbol = bta.TA.Symbol.Replace("/X", "");

                if (string.IsNullOrEmpty(bta.ErrMsg))
                {
                    try
                    {
                        bta.TA.Currency = GetMarketCurrency(bta.TA, manual);            // MC can be solved first as no dependencies
                        bta.TA.McAmountPerUnit = GetMcAmountPerUnit(bta.TA, manual);    // depends to Currency
                        bta.TA.CurrencyRate = GetCurrencyRate(bta.TA, manual);          // depends to Currency & McAmountPerUnit
                        bta.TA.McFee = GetFeeAsMc(bta.TA, manual);                      // depends to Currency & CurrencyRate

                        if (bta.TA.Action == TaType.Round)
                            bta.TA.Units = RoundInitialUnits(bta.TA, manual);

                        if ((bta.TA.Action == TaType.Buy || bta.TA.Action == TaType.Sell) && bta.TA.McAmountPerUnit > 100)
                            // London has everything as pennies, so need /100 .. doing it now here 
                            bta.TA.McAmountPerUnit = bta.TA.McAmountPerUnit / 100;
                    }
                    catch (Exception ex)
                    {
                        bta.ErrMsg = $"BtNordnet.Convert failed to exception [{ex.Message}]";
                    }
                }

                bta.LineNum = lineNum;
                ret.Add(bta);
            }
        }

        return new OkResult<List<BtAction>>(ret);
    }

    public bool AddMissingRate(BtAction bta, decimal currencyRate)
    {
        if (_homeCurrency == CurrencyId.Unknown)
            return false;

        bta.TA.CurrencyRate = currencyRate;

        // McFee depends from CurrencyRate
        (Transaction action, Dictionary<string, string> manual, string errMsg) = Convert(SplitLine(bta.Orig));
        bta.TA.McFee = GetFeeAsMc(bta.TA, manual);
        return true;
    }

    protected static TaType ConvAction(string content)
    {
        switch (content)
        {
            case "VAIHTO AP-JÄTTÖ":                         // Company buy my other company with M&A, and converted their shares to this company
            case "OSINKO AP JÄTTÖ":                         // Spin-off some sub-company, like OGN was spinned by Merch w 10 shares gives 1 share
            case "YHTIÖIT. IRR JÄTTÖ":                      // ATT to WBD type spin-off w value moving
            case "OSTO": return TaType.Buy;
            case "MYYNTI": return TaType.Sell;
            case "OSINKO": return TaType.Divident;
            case "DESIM KIRJAUS OTTO": return TaType.Round; // Getting rid off decimal shares, example post M&A received XX.YY, so this sells 0.YY to leave XX
            case "VAIHTO AP-OTTO":
            case "LUNASTUS AP OTTO": return TaType.Close;   // M&A etc that simply makes company boof... any remaining holdings are zero profit "sold"

            // VAIHTO ARVOPAPERIN OTTO & VAIHTO ARVOPAPERIN JÄTTÖ - currency/market change for stock, rare, YAMANA did have this as prep CAD->USD before M&A
            // LUNASTUS AP KÄT. - money added from sale, not tracked atm
        }
        return TaType.Unknown;
    }

    protected CurrencyId GetMarketCurrency(Transaction ta, Dictionary<string, string> manual) // Simple rule! If anywhere is currency other than HC then we use other
    {
        if ( ta.Currency != CurrencyId.Unknown && ta.Currency != _homeCurrency) // Hankinta-arvo#Valuutta
            return ta.Currency;

        CurrencyId currency = ConvCurrency(manual["Summa#Valuutta"]);

        if (currency != CurrencyId.Unknown && currency != _homeCurrency)
            return currency;

        if (IsDivNote(ta))
        {
            currency = Enum.Parse<CurrencyId>(ta.Note.Split(' ')[3].Split('/')[0]);

            if (currency != CurrencyId.Unknown && currency != _homeCurrency)
                return currency;
        }

        return _homeCurrency;
    }

    // McAmountPerUnit == "Kurssi", just not given many cases.. so calculate it wher ever can...
    protected static decimal GetMcAmountPerUnit(Transaction ta, Dictionary<string, string> manual)
    {
        if ( ta.McAmountPerUnit > 0)
            return ta.McAmountPerUnit;  // Kurssi
             
        if (ta.Action == TaType.Divident) // <= depends "ta.Currency"
        {   // Divident case - can find if from Summa..
            if (ConvCurrency(manual["Summa#Valuutta"]) == ta.Currency)
            { 
                decimal? summa = ConvDecimal(manual["Summa"]);

                if (summa.HasValue && ta.Units > 0)
                    // These cases looks like we can get perUnit w simple divide
                    return (summa.Value / ta.Units).Round5();
            }

            // or worst case from note
            if (IsDivNote(ta))
                return ConvDecimal(ta.Note.Split(' ')[2]).Value;
        }
        else if ( ta.Action == TaType.Buy )
        {   // M&A-"BUY" doesnt give it so calculate
            if (ta.McAmountPerUnit == 0)
            {
                decimal? arvo = ConvDecimal(manual["Hankinta-arvo"]);

                if (arvo.HasValue && arvo.Value > 0)
                    return (arvo.Value / ta.Units).Round5();
            }
        }
        return 0;
    }

    protected decimal GetCurrencyRate(Transaction ta, Dictionary<string, string> manual)
    {
        if ( ta.CurrencyRate > 0 )
            return ta.CurrencyRate;

        if (string.IsNullOrEmpty(manual["Viitevaluuttakurssi"]) == false)
        {
            decimal? value = ConvDecimal(manual["Viitevaluuttakurssi"]);
            if ( value.HasValue && value.Value > 0)
                return value.Value;
        }

        string note = ta.Note;
            
        if (IsDivNote(ta)) // TRADE-DIV - case has to go hard way
        {
            decimal? totalDiv = ConvDecimal(manual["Summa"]);

            if (manual["Summa#Valuutta"] == _homeCurrency.ToString() && ta.Currency != _homeCurrency) // <= depends "ta.Currency"
                return (totalDiv.Value / (ta.Units * ta.McAmountPerUnit)).Round5(); // <= depends "ta.McAmountPerUnit"
        }

        if (_homeCurrency != ta.Currency)
            return -1;
        else
            return 1;
    }

    protected static bool IsDivNote(Transaction ta)
    {   // TRADE-DIV - where they dont seam be able to add rate or MC needs to use 'Note'
        if (ta.Action == TaType.Divident &&
            (ta.Note.StartsWith("OSINKO ") && ta.Note.EndsWith("/OSAKE")) ||    // OSINKO INTC 0.125 USD/OSAKE
            (ta.Note.StartsWith("DIVIDEND ") && ta.Note.EndsWith("/SHARE")))    // DIVIDEND ABX 0,1 USD/SHARE
            return true;
        return false;
    }

    protected static decimal GetFeeAsMc(Transaction ta, Dictionary<string, string> manual)
    {
        if (ta.McFee == 0)
            return 0;

        if (Enum.TryParse(manual["Kokonaiskulut#Valuutta"], out CurrencyId currencyId) == false || currencyId == ta.Currency || ta.CurrencyRate <= 0)
            return ta.McFee;

        // If account is not currency base, like OST isnt then fee comes as EuroHC and needs do bit recalc to get McFee
        return (ta.McFee / ta.CurrencyRate).Round5();
    }

    protected static decimal RoundInitialUnits(Transaction ta, Dictionary<string, string> manual)
    {
        decimal? finalUnits = ConvDecimal(manual["Kokonaismäärä"]);

        if ( ta.Units > 0 && ta.Units < 1 && finalUnits.HasValue && finalUnits.Value > 1 && finalUnits.Value.IsInteger() )
            return finalUnits.Value + ta.Units;

        return ta.Units;
    }
}
