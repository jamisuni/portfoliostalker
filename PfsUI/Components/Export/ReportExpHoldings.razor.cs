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

using Microsoft.AspNetCore.Components;
using System.Dynamic;
using System.Globalization;
using System.Text;

using MudBlazor;
using BlazorDownloadFile;
using CsvHelper;
using Pfs.Types;

namespace PfsUI.Components;

// This report is specially targeted to more public/exportable holdings report generation
public partial class ReportExpHoldings
{
    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    protected List<ViewData> _viewStocks = null;
    protected ViewData _viewTotal = null;
    protected List<ViewData> _viewSectors = null; // [0] = no sector, [1..] 'Sector' as key. 
    protected int _withoutSector = 0;

    protected string _rawHtmlTable = string.Empty;

    protected bool _noContent = false; // Little helper to show nothing if has nothing

    // EXPORT
    public enum RepExport : int
    {
        CSV,
        HTMLTable,
    }

    public enum RepColumn // Heavily dependable to 'ViewExportHoldings.razor.cs' dont do anything funny here..
    {
        Symbol,
        CompanyName,
        Sector,
        AvrgPrice,              // Only MC$
        AvrgTime,
        Invested,               // Only HC
        Valuation,              // Only HC
        WeightP,                // % of total Valuation
        DivTotal,
        DivPTotal,
        TotalGain,
        TotalPGain,
    }

    public enum RepSettings
    {
        InvertedSorting,
        ViewSectorTotals,
        ViewTotal,
    }

    protected override void OnParametersSet()
    {
        // Note! No more saving of selections! Instead future add here checking if sector-1 is defined then 
        //       add sector totals also to default selections

        ViewData.HC = UiF.Curr(Pfs.Config().HomeCurrency);

        // SORTING 
        _sortBy = RepColumn.CompanyName.ToString();


        // COLUMNS
        List<RepColumn> selColumns = new();
        HashSet<string> selColumnDef = new HashSet<string>();
        foreach (RepColumn col in Enum.GetValues(typeof(RepColumn)))
        {
            if (col == RepColumn.Sector)
                continue;

            selColumns.Add(col);
            selColumnDef.Add(col.ToString());
        }
        SelColumnDef = selColumnDef;
        _selColumns = string.Join(',', selColumns);

        ReloadData();

        // SETTINGS -can be done after reload data
        if (_viewStocks.Count() > 5 && ((decimal)_withoutSector)/ _viewStocks.Count() < 0.25m)
            // Sector totals is defaulted on if has enough stocks(75%+) w sector defined
            _selSettings = string.Join(',', [RepSettings.ViewTotal.ToString(), RepSettings.ViewSectorTotals.ToString()]);
        else
            _selSettings = string.Join(',', [RepSettings.ViewTotal.ToString()]);

        HashSet<string> selSettingsDef = new HashSet<string>();
        foreach (RepSettings sett in Enum.GetValues(typeof(RepSettings)))
            if (_selSettings.Contains(sett.ToString()))
                selSettingsDef.Add(sett.ToString());
        SelSettingsDef = selSettingsDef;

        if (_viewStocks.Count() > 0)
        {
            _noContent = false;
            // First view is per stored settings combo
            RefreshReport();
        }
        else
            _noContent = true;
    }

    public void ByOwner_ReloadReport()
    {
        ReloadData();

        if (_viewStocks.Count() > 0)
        {
            _noContent = false;
            RefreshReport();
        }
        else
            _noContent = true;

        StateHasChanged();
    }

    protected void ReloadData()
    {
        _viewStocks = new();
        _viewTotal = new ViewData();
        _withoutSector = 0;
        _viewSectors = new() {
           new ViewData() { 
               CompanyName = "-unassigned-" // == [0]
           } 
        };

        List<RepDataExpHoldings> data = Pfs.Report().GetExportHoldingsData();

        if (data == null || data.Count == 0)
            return;

        foreach (RepDataExpHoldings holding in data)
        {
            ViewData entry = new ViewData(holding);
            _viewStocks.Add(entry);
            _viewTotal.Sum(entry);

            if (string.IsNullOrEmpty(holding.SectorDef))
            {
                _withoutSector++;
                _viewSectors[0].Sum(entry);
            }
            else
            {
                ViewData sectorTotal = _viewSectors.FirstOrDefault(s => s.Sector == holding.SectorDef);

                if (sectorTotal == null)
                {
                    sectorTotal = new();
                    sectorTotal.Sector = holding.SectorDef;
                    sectorTotal.CompanyName = holding.SectorDef; // used by 'Local_AddTotal'
                    _viewSectors.Add(sectorTotal);
                }
                sectorTotal.Sum(entry);
            }
        }

        if (_viewStocks.Count == 0)
            return;

        foreach (ViewData entry in _viewStocks)
        {
            if (entry.ValuationD > 0.01m)
                entry.WeightPD = entry.ValuationD / _viewTotal.ValuationD * 100;

            entry.ConvertViewFormat();
        }

        _viewTotal.ConvertViewFormat(true);

        foreach (ViewData viewSector in _viewSectors)
        {
            if (viewSector.ValuationD > 0.01m)
                viewSector.WeightPD = viewSector.ValuationD / _viewTotal.ValuationD * 100;

            viewSector.ConvertViewFormat(true);
        }
    }

    // EV: Select COLUMNS

    private IEnumerable<string> SelColumnDef { get; set; } = new HashSet<string>(); // Helper to get checkboxes to match 'SelColumns'

    // Note! This little beauty here allows '_selColumns' to have list of selection, but on screen it just shows 'Select'
    protected string _selColumns = string.Empty;
    protected string SelColumns(List<string> columns)
    {
        _selColumns = string.Join(',', columns.ToArray());
        RefreshReport();
        return "Select";
    }

    // EV: Do SORTING

    protected string _sortBy = string.Empty;
    protected string SortBy { get { return _sortBy; }
        set { _sortBy = value; RefreshReport(); } }

    // EV: Do SETTINGS
    
    private IEnumerable<string> SelSettingsDef { get; set; } = new HashSet<string>(); // Helper to get checkboxes to match 'SelSettings'

    protected string _selSettings = string.Empty;
    protected string SelSettings(List<string> settings)
    {
        _selSettings = string.Join(',', settings.ToArray());
        RefreshReport();
        StateHasChanged();
        return "Select";
    }

    protected void RefreshReport()
    {
        // !!!NOTE!!! Uses special ""@((MarkupString)_rawHtmlTable)"" to output dynamically created HTML table on page
        _rawHtmlTable = CreateHTMLTable();
    }

    protected List<dynamic> CreateDynRecords(List<ViewData> data)
    {
        try
        {
            var records = new List<dynamic>();

            foreach (ViewData entry in data)
                records.Add(CreateDynActiveFields(entry));

            return records;
        }
        catch (Exception)
        {
        }
        return null;
    }

    protected dynamic CreateDynActiveFields(ViewData data)
    {
        var activeFields = new ExpandoObject() as IDictionary<string, Object>;

        foreach (RepColumn col in Enum.GetValues(typeof(RepColumn)))
        {
            if (_selColumns.Contains(col.ToString()))
            {
                var value = data.GetType().GetProperty(col.ToString()).GetValue(data, null);

                if (value == null)
                    activeFields.Add(col.ToString(), string.Empty);
                else
                    activeFields.Add(col.ToString(), value);
            }
        }
        return activeFields;
    }

    protected string CreateHTMLTable()
    {
        try
        {
            if (_viewStocks.Count == 0)
            {
                _noContent = true;
                return string.Empty;
            }

            dynamic firstRecord = CreateDynActiveFields(_viewStocks[0]);

            StringBuilder content = new();
            content.AppendLine("<table>");
            content.AppendLine("<thead>");
            content.AppendLine("<tr>");

            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)firstRecord)
            {
                content.AppendLine($"<th>{kvp.Key}__</th>"); // need to add some -- to separate columns, fix when figuring out better way...
            }

            content.AppendLine("</tr>");
            content.AppendLine("</thead>");

            // Total row is per settings (we put it here first, as seams to push it around bit randomly)

            if (_selSettings.Contains(RepSettings.ViewTotal.ToString()) || _selSettings.Contains(RepSettings.ViewSectorTotals.ToString()))
                // We actually include this also to 'ViewSectorTotals' even if 'ViewTotal' as otherwise things get messy on browser
                Local_AddTotal(ref content, _viewTotal);

            // Header is same for all cases, but body handling variers

            if (_selSettings.Contains(RepSettings.ViewSectorTotals.ToString()) && _viewSectors != null )
                // 
                Local_SectorTable(ref content, _viewStocks);
            else
                Local_StandardTable(ref content, _viewStocks);

            content.AppendLine("</table>");

            return content.ToString();
        }
        catch (Exception)
        {
        }
        return string.Empty;


        void Local_StandardTable(ref StringBuilder content, List<ViewData> viewData)
        {
            // Coming here we just wanna apply user setting sorting, and create one big table w all stocks entries
            List<dynamic> records = CreateDynRecords(ApplySorting(viewData));

            content.AppendLine("<tbody>");

            foreach (var rec in records)
                Local_AddStock2Content(ref content, rec);

            content.AppendLine("</tbody>");
        }

        void Local_SectorTable(ref StringBuilder content, List<ViewData> viewData)
        {
            // In this case sectors are dictating viewing, so all stocks are under their user define sectors

            List<ViewData> sortedSectors = Local_GetSortedSectors();

            foreach (ViewData sector in sortedSectors)
            {
                if (string.IsNullOrEmpty(sector.Sector) == true)
                    //Unassigned sector that goes last...
                    continue;

                List<dynamic> records = CreateDynRecords(ApplySorting(viewData.Where(e => e.Sector == sector.Sector).ToList()));

                if (records.Count == 0)
                    //Not showing empty sectors
                    continue;

                Local_AddTotal(ref content, sector);

                content.AppendLine("<tbody>");

                foreach (var rec in records)
                    Local_AddStock2Content(ref content, rec);

                content.AppendLine("</tbody>");
            }

            // Plus there can be unassigned stocks, pretty much not yet set for any sector.. so those are shown end of report (wo own total)

            List<ViewData> unassigned = viewData.Where(e => string.IsNullOrEmpty(e.Sector)).ToList();

            if (unassigned.Count > 0)
            {
                ViewData unassignedTotal = sortedSectors.Single(g => string.IsNullOrEmpty(g.Sector));
                Local_AddTotal(ref content, unassignedTotal);

                List<dynamic> records = CreateDynRecords(ApplySorting(unassigned).ToList());

                content.AppendLine("<tbody>");

                foreach (var rec in records)
                    Local_AddStock2Content(ref content, rec);

                content.AppendLine("</tbody>");
            }
        }

        // Uses _origSectorTotals pulls things kind of sorted per generic selection if possible or defaults to user order
        List<ViewData> Local_GetSortedSectors()
        {
            Func<ViewData, Object> orderByFunc = null;

            switch (SortBy)
            {
                case "Symbol": orderByFunc = field => field.Symbol; break;
                case "CompanyName": orderByFunc = field => field.CompanyName; break;
                case "Sector": orderByFunc = field => field.Sector; break;
                case "AvrgPrice": orderByFunc = field => field.AvrgPriceD; break;
                case "AvrgTime": orderByFunc = field => field.AvrgTimeD; break;
                case "Invested": orderByFunc = field => field.InvestedD; break;
                case "Valuation": orderByFunc = field => field.ValuationD; break;
                case "WeightP": orderByFunc = field => field.WeightPD; break;
                case "DivTotal": orderByFunc = field => field.DivTotalD; break;
                case "DivPTotal": orderByFunc = field => field.DivPTotalD; break;
                case "TotalGain": orderByFunc = field => field.TotalGainD; break;
                case "TotalPGain": orderByFunc = field => field.TotalPGainD; break;

                default:
                    orderByFunc = field => field.CompanyName;
                    break;
            }

            if (_selSettings.Contains(RepSettings.InvertedSorting.ToString()))
                return _viewSectors.OrderByDescending(orderByFunc).ToList();
            else
                return _viewSectors.OrderBy(orderByFunc).ToList();
        }

        void Local_AddStock2Content(ref StringBuilder content, dynamic rec)
        {
            content.AppendLine("<tr>");
            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)rec)
            {
                content.AppendLine($"<td>{kvp.Value.ToString()}</td>");
            }
            content.AppendLine("</tr>");
        }

        void Local_AddTotal(ref StringBuilder content, ViewData viewTotal)
        {
            dynamic activeTotal = CreateDynActiveFields(viewTotal);

            content.AppendLine("<tfoot>");
            content.AppendLine("<tr>");
            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)activeTotal)
            {
                if (kvp.Key == RepColumn.CompanyName.ToString() )
                {   // Default Total: or SectorName: is shown on company name column... 
                    if (string.IsNullOrEmpty(kvp.Value.ToString()) == true)
                        content.AppendLine("<th colspan='1'>Total:</th>");
                    else
                        content.AppendLine($"<th colspan='1'>{kvp.Value.ToString()}:</th>");
                }
                else
                    content.AppendLine($"<th>{kvp.Value.ToString()}</th>");
            }
            content.AppendLine("</tr>");
            content.AppendLine("</tfoot>");
        }
    }

    List<ViewData> ApplySorting(List<ViewData> unsorted)
    {
        Func<ViewData, Object> orderByFunc = null;
        bool alphaInfoSorting = false;

        switch (SortBy)
        {
            case "Symbol": orderByFunc = field => field.Symbol; alphaInfoSorting = true; break;
            case "CompanyName": orderByFunc = field => field.CompanyName; alphaInfoSorting = true; break;
            case "Sector": orderByFunc = field => field.Sector; alphaInfoSorting = true; break;
            case "AvrgPrice": orderByFunc = field => field.AvrgPriceD; break;
            case "AvrgTime": orderByFunc = field => field.AvrgTimeD; break;
            case "Invested": orderByFunc = field => field.InvestedD; break;
            case "Valuation": orderByFunc = field => field.ValuationD; break;
            case "WeightP": orderByFunc = field => field.WeightPD; break;
            case "DivTotal": orderByFunc = field => field.DivTotalD; break;
            case "DivPTotal": orderByFunc = field => field.DivPTotalD; break;
            case "TotalGain": orderByFunc = field => field.TotalGainD; break;
            case "TotalPGain": orderByFunc = field => field.TotalPGainD; break;

            default:
                orderByFunc = field => field.CompanyName;
                break;
        }

        // Sorting for companyName is a,b,c.. but for number fields wants to have bigger ones first
        if (_selSettings.Contains(RepSettings.InvertedSorting.ToString()) == alphaInfoSorting)
            return unsorted.OrderByDescending(orderByFunc).ToList();
        else
            return unsorted.OrderBy(orderByFunc).ToList();
    }

    protected class ViewData // Note! Must have *property* field for each 'RepExportHoldingsColumn' w same name w type *string* w view formatted
    {
        /* RULES:
         * - Keep all field names clean of HC as user doesnt need to see all that HC tuff on headers even all fields are HC's
         * - Keep everything as STRING that is getting viewed, that way formatting easily passes to all exports on identical way
         * - As sorting still needs numbers, looks like we do double fields
         */
        public string Symbol { get; set; }
        public string CompanyName { get; set; }
        public string Sector { get; set; }
        public string MC { get; set; }  // Note! Only avrg price uses market currency
        public static string HC { get; set; }

        public string AvrgPrice { get; set; }               public decimal AvrgPriceD { get; set; } = 0;
        public string AvrgTime { get; set; }                public decimal AvrgTimeD { get; set; } = 0;             // !!!TODO!!! This needs more info! derived = How far as days is every invested HC  (long InvestedCurrencyDays)

        public string Invested { get; set; }                public decimal InvestedD { get; set; } = 0;
        public string Valuation { get; set; }               public decimal ValuationD { get; set; } = 0;
        public string WeightP { get; set; }                 public decimal WeightPD { get; set; } = 0;      // calculated on this file as needs holdings total valuation

        public string DivTotal { get; set; }                public decimal DivTotalD { get; set; } = 0;
        public string DivPTotal { get; set; }               // calculated % per DivTotal / Invested
        public string TotalGain { get; set; }               // calculated DivTotal + Valuation - Invested
        public string TotalPGain { get; set; }              // calculated % per TotalGain / Invested

        public decimal DivPTotalD { get {
                if (DivTotalD == 0)
                    return 0;
                else
                    return DivTotalD / InvestedD * 100;
            }
        }

        public decimal TotalGainD { get {
                return DivTotalD + ValuationD - InvestedD;
            }
        }

        public decimal TotalPGainD { get {
                if (TotalGainD == 0)
                    return 0;
                else
                    return TotalGainD / InvestedD * 100;
            }
        }

        public void ConvertViewFormat(bool total = false)
        {
            AvrgPrice = AvrgPriceD.To00() + MC; // MC$ viewed
            Invested = InvestedD.To();
            Valuation = ValuationD.To();
            WeightP = WeightPD.ToP();
            DivTotal = DivTotalD.To();
            DivPTotal = DivPTotalD.ToP();
            TotalGain = TotalGainD.To();
            TotalPGain = TotalPGainD.ToP();

            AvrgTime = Local_FormatAvrgTime((int)AvrgTimeD);

            if (total)
            {
                // In case of total field, we do remove some tuff...
                AvrgPrice = string.Empty;
                AvrgTime = string.Empty;

                Invested += HC;
                Valuation += HC;
                DivTotal += HC;
                TotalGain += HC;
            }

            return;


            string Local_FormatAvrgTime(int months)
            {
                if (months < 12)
                    return months.ToString() + "m";

                if (months < 36)
                    return (months / 12).ToString() + "y" + (months % 12 != 0 ? (months % 12).ToString() + "m" : "");

                return (months / 12).ToString() + "y";
            }
        }

        public ViewData()
        {
        }

        public ViewData(RepDataExpHoldings exp)
        {
            Symbol = exp.StockMeta.symbol;
            CompanyName = exp.StockMeta.name;
            Sector = exp.SectorDef;
            MC = UiF.Curr(exp.StockMeta.marketCurrency);

            AvrgPriceD = exp.RCTotalHold.HcAvrgPrice;
            AvrgTimeD = exp.AvrgTimeAsMonths;
            InvestedD = exp.RCTotalHold.HcInvested;
            ValuationD = exp.RCTotalHold.HcValuation;
            DivTotalD = exp.RRTotalDivident.HcDiv;
        }

        public void Sum(ViewData add)
        {
            AvrgPriceD += add.AvrgPriceD;
            InvestedD += add.InvestedD;
            ValuationD += add.ValuationD;
            DivTotalD += add.DivTotalD;
        }
    }

#region EXPORT

    protected async Task OnExportAsync(RepExport format)
    {
        string stringContent = string.Empty;
        string filename = string.Empty;

        switch (format)
        {
            case RepExport.CSV:
                stringContent = Local_ExportCSV();
                filename = "PfsHoldings_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                break;

            case RepExport.HTMLTable:
                stringContent = CreateHTMLTable();
                filename = "PfsHoldingsHtml_" + DateTime.Today.ToString("yyyyMMdd") + ".txt";
                break;
        }

        if (string.IsNullOrWhiteSpace(stringContent) == false)
        {
            //await BlazorDownloadFileService.DownloadFile(filename, stringContent, "application/zip");
            await BlazorDownloadFileService.DownloadFileFromText(filename, stringContent, Encoding.Default, "text/plain");
        }

        return;

        string Local_ExportCSV()
        {
            try
            {
                var dynRecords = CreateDynRecords(_viewStocks);

                using (var writer = new StringWriter())
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(dynRecords);

                    return writer.ToString();
                }
            }
            catch (Exception)
            {
            }
            return string.Empty;
        }
    }

    #endregion
}
