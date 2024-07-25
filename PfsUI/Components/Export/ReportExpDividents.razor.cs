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

// Exports single row per each divident payed on specific time period
public partial class ReportExpDividents
{
    [Inject] IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    protected List<int> _speedFilterYears = new();
    protected int? _filterYear = null;

    protected List<ViewData> _viewData = null;
    protected ViewData _total = null;

    protected string _rawHtmlTable = string.Empty;

    protected bool _noContent = false; // Little helper to show nothing if has nothing

    // COLUMNS
    public enum RepColumn : int     // Presents all COLUMNS available on this report
    {
        Symbol,
        CompanyName,

        ExDivDate,
        PaymentDate,

        PayedUnits,

        PaymentPerUnit,
        HcPaymentPerUnit,

        HcPayment,

        Rate,
    }

    // SETTINGS
    public enum RepSettings : int   // Presents all ON/OFF settings supported for customizing report
    {
        InvertedSorting,
        ViewTotal
    }

    // EXPORT
    public enum RepExport : int
    {
        CSV,
        HTMLTable,
    }

    protected override void OnParametersSet()
    {
        _filterYear = DateTime.Now.Year;

        _selSettings = string.Join(',', [RepSettings.ViewTotal.ToString()]);
        _sortBy = RepColumn.PaymentDate.ToString();

        // Per '_selColumns' set hash table to help setting checkboxes also..
        List<RepColumn> selColumns = new();
        HashSet<string> selColumnDef = new HashSet<string>();
        foreach (RepColumn col in Enum.GetValues(typeof(RepColumn)))
        {
            if (col == RepColumn.Rate || col == RepColumn.HcPaymentPerUnit)
                continue;

            selColumns.Add(col);
            selColumnDef.Add(col.ToString());
        }
        SelColumnDef = selColumnDef;
        _selColumns = string.Join(',', selColumns);

        // Per _selSettings
        HashSet<string> selSettingsDef = new HashSet<string>();
        foreach (RepSettings sett in Enum.GetValues(typeof(RepSettings)))
            if (_selSettings.Contains(sett.ToString()))
                selSettingsDef.Add(sett.ToString());
        SelSettingsDef = selSettingsDef;

        ReloadData();
    }

    public void ByOwner_ReloadReport()
    {
        ReloadData();

        if (_viewData.Count() > 0)
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
        // Get report content from PFS.Client
        var resp = Pfs.Report().GetExportDividentsData();

        if (resp == null)
        {
            _rawHtmlTable = string.Empty;
            _noContent = true;
            StateHasChanged();
            return;
        }

        _viewData = new();
        _total = new(Pfs.Config().HomeCurrency);
        foreach (RepDataExpDividents orig in resp)
        {
            if (_speedFilterYears.Contains(orig.Div.PaymentDate.Year) == false)
                _speedFilterYears.Add(orig.Div.PaymentDate.Year);

            if (_filterYear.HasValue && orig.Div.PaymentDate.Year != _filterYear.Value)
                continue;

            ViewData data = new ViewData(orig);
            data.ConvertViewFormat();
            _viewData.Add(data);

            _total.HcPaymentD += data.HcPaymentD;

        }
        _total.ConvertViewFormat(true);

        _noContent = false;
        RefreshReport();
    }

    protected void OnSpeedFilterChanged(int year)
    {
        if (year == 0)
            _filterYear = null;
        else
            _filterYear = year;

        ReloadData();
        StateHasChanged();
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
            if (_viewData.Count == 0)
            {
                _noContent = true;
                return string.Empty;
            }

            dynamic firstRecord = CreateDynActiveFields(_viewData[0]);

            StringBuilder content = new();
            content.AppendLine("<table>");
            content.AppendLine("<thead>");
            content.AppendLine("<tr>");

            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)firstRecord)
            {
                content.AppendLine(string.Format("<th>{0}__</th>", kvp.Key));
            }

            content.AppendLine("</tr>");
            content.AppendLine("</thead>");

            if (_selSettings.Contains(RepSettings.ViewTotal.ToString()) && _total != null )
                // We actually include this also to 'ViewSectorTotals' even if 'ViewTotal' as otherwise things get messy on browser
                Local_AddTotal(ref content, _total);

            Local_StandardTable(ref content, _viewData);

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

        void Local_AddTotal(ref StringBuilder content, ViewData total)
        {
            dynamic activeTotal = CreateDynActiveFields(total);

            content.AppendLine("<tfoot>");
            content.AppendLine("<tr>");

            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)activeTotal)
            {
                if (kvp.Key == RepColumn.CompanyName.ToString())
                {
                    if (string.IsNullOrEmpty(kvp.Value.ToString()) == true)
                        content.AppendLine(string.Format("<th colspan='1'>Total:</th>"));
                    else
                        content.AppendLine(string.Format("<th colspan='1'>{0}:</th>", kvp.Value.ToString()));
                }
                else if (kvp.Key == RepColumn.HcPayment.ToString() )
                {
                    content.AppendLine(string.Format("<th>{0}</th>", kvp.Value.ToString()));
                }
                else
                    content.AppendLine(string.Format("<th></th>", kvp.Value.ToString()));
            }

            content.AppendLine("</tr>");
            content.AppendLine("</tfoot>");
        }

        void Local_AddStock2Content(ref StringBuilder content, dynamic rec)
        {
            content.AppendLine("<tr>");
            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)rec)
            {
                content.AppendLine(string.Format("<td>{0}</td>", kvp.Value.ToString()));
            }
            content.AppendLine("</tr>");
        }
    }

    List<ViewData> ApplySorting(List<ViewData> unsorted)
    {
        Func<ViewData, Object> orderByFunc = null;

        switch (SortBy)
        {
            case "Symbol": orderByFunc = field => field.Symbol; break;
            case "CompanyName": orderByFunc = field => field.CompanyName; break;
            case "PayedUnits": orderByFunc = field => field.PayedUnitsD; break;
            case "ExDivDate": orderByFunc = field => field.ExDivDateD; break;
            case "PaymentDate": orderByFunc = field => field.PaymentDateD; break;
            case "PaymentPerUnit": orderByFunc = field => field.PaymentPerUnitD; break;
            case "Rate": orderByFunc = field => field.RateD; break;

            case "HcPaymentPerUnit": orderByFunc = field => field.HcPaymentPerUnitD; break;
            case "HcPayment": orderByFunc = field => field.HcPaymentD; break;

            default:
                orderByFunc = field => field.CompanyName;
                break;
        }

        if (_selSettings.Contains(RepSettings.InvertedSorting.ToString()))
            return unsorted.OrderByDescending(orderByFunc).ToList();
        else
            return unsorted.OrderBy(orderByFunc).ToList();
    }

    protected class ViewData
    {
        public string Symbol { get; set; }
        public string CompanyName { get; set; }
        public string DC { get; set; }
        public string HC { get; set; }

        public string PayedUnits { get; set; }              public decimal PayedUnitsD { get; set; }
        public string ExDivDate { get; set; }               public DateOnly ExDivDateD { get; set; }
        public string PaymentDate { get; set; }             public DateOnly PaymentDateD { get; set; }
        public string PaymentPerUnit { get; set; }          public decimal PaymentPerUnitD { get; set; }
        public string HcPaymentPerUnit { get; set; }        public decimal HcPaymentPerUnitD { get; set; }
        public string HcPayment { get; set; }               public decimal HcPaymentD { get; set; }

        public string Rate {  get; set; }                   public decimal RateD { get; set; }

        public ViewData(RepDataExpDividents data)
        {
            Symbol = data.StockMeta.symbol;
            CompanyName = data.StockMeta.name;
            DC = UiF.Curr(data.Div.Currency);

            PayedUnitsD = data.Div.HoldingUnits + data.Div.TradesUnits;
            ExDivDateD = data.Div.ExDivDate;
            PaymentDateD = data.Div.PaymentDate;
            PaymentPerUnitD = data.Div.PaymentPerUnit;
            HcPaymentPerUnitD = data.Div.HcTotalDiv / PayedUnitsD;
            HcPaymentD = data.Div.HcTotalDiv;
            RateD = data.Div.CurrencyRate;
        }

        public ViewData(CurrencyId homeCurrencyId)
        {
            HcPaymentD = 0;
            HC = UiF.Curr(homeCurrencyId);
        }

        public void ConvertViewFormat(bool total = false)
        {
            HcPayment = HcPaymentD.To0();

            if (total)
            {
                HcPayment += HC;
                return;
            }

            PayedUnits = PayedUnitsD.To();
            ExDivDate = ExDivDateD.ToString("yyyy-MM-dd");
            PaymentDate = PaymentDateD.ToString("yyyy-MM-dd");
            PaymentPerUnit = PaymentPerUnitD.To00() + DC;
            HcPaymentPerUnit = HcPaymentPerUnitD.To00();
            Rate = RateD.To000();
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
                filename = "PfsExportDividents_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                break;

            case RepExport.HTMLTable:
                stringContent = CreateHTMLTable();
                filename = "PfsDividentsHtmlTable_" + DateTime.Today.ToString("yyyyMMdd") + ".txt";
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
                var dynRecords = CreateDynRecords(_viewData);

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
