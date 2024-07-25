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

// Exports single row per each holding traded/sold off for specific time period
public partial class ReportExpSales
{
    [Inject] IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    protected List<int> _speedFilterYears = new();
    protected int? _filterYear = null;

    protected List<ViewData> _viewStocks = null;
    protected ViewData _total = null;

    protected string _rawHtmlTable = string.Empty;

    protected bool _noContent = false; // Little helper to show nothing if has nothing

    // COLUMNS
    public enum RepColumn : int     // Presents all COLUMNS available on this report
    {
        PfName,
        Symbol,
        CompanyName,

        Units,

        PurhaceDate,
        McPurhacePrice,             // Need to have these MC so that selection list doesnt confuse to duplicates
        HcPurhacePrice,
        PurhaceRate,

        SaleDate,
        McSalePrice,
        HcSalePrice,
        SaleRate,

        McProfit,
        HcProfit,
    }

    // SETTINGS
    public enum RepSettings : int   // Presents all ON/OFF settings supported for customizing report
    {
        InvertedSorting,
        ViewTotal,
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

        _sortBy = RepColumn.SaleDate.ToString();

        // Per '_selColumns' set hash table to help setting checkboxes also..
        List<RepColumn> selColumns = new();
        HashSet<string> selColumnDef = new HashSet<string>();
        foreach (RepColumn col in Enum.GetValues(typeof(RepColumn)))
        {
            if (col == RepColumn.HcPurhacePrice || col == RepColumn.HcSalePrice ||
                col == RepColumn.PurhaceRate || col == RepColumn.SaleRate)
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
        List<RepDataExpSales> report = Pfs.Report().GetExportSalesData();

        if (report == null )
        {
            _rawHtmlTable = string.Empty;
            _noContent = true;
            StateHasChanged();
            return;
        }

        _viewStocks = new();
        _total = new(Pfs.Config().HomeCurrency);
        foreach (RepDataExpSales orig in report)
        {
            if (_speedFilterYears.Contains(orig.Holding.SH.Sold.SaleDate.Year) == false)
                _speedFilterYears.Add(orig.Holding.SH.Sold.SaleDate.Year);

            if (_filterYear.HasValue && orig.Holding.SH.Sold.SaleDate.Year != _filterYear.Value)
                continue;

            ViewData data = new ViewData(orig);
            data.ConvertViewFormat();
            _viewStocks.Add(data);

            _total.HcPurhacePriceD += data.HcPurhacePriceD * data.UnitsD;
            _total.HcSalePriceD += data.HcSalePriceD * data.UnitsD;
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
            if (_viewStocks.Count == 0)
            {
                _noContent = true;
                return String.Empty;
            }

            dynamic firstRecord = CreateDynActiveFields(_viewStocks[0]);

            StringBuilder content = new();
            content.AppendLine("<table>");
            content.AppendLine("<thead>");
            content.AppendLine("<tr>");

            foreach (KeyValuePair<string, object> kvp in (IDictionary<string, object>)firstRecord)
            {
                if ( kvp.Key == "SaleDate" || kvp.Key == "PfName")
                    content.AppendLine($"<th>{kvp.Key}_____</th>");
                else
                    content.AppendLine($"<th>{kvp.Key}__</th>");
            }

            content.AppendLine("</tr>");
            content.AppendLine("</thead>");

            if (_selSettings.Contains(RepSettings.ViewTotal.ToString()) && _total != null)
            {
                // We actually include this also to 'ViewSectorTotals' even if 'ViewTotal' as otherwise things get messy on browser
                Local_AddTotal(ref content, _total);
            }

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
                        content.AppendLine($"<th colspan='1'>Total:</th>");
                    else
                        content.AppendLine($"<th colspan='1'>{kvp.Value.ToString()}:</th>");
                }
                else if (kvp.Key == RepColumn.HcPurhacePrice.ToString() || 
                         kvp.Key == RepColumn.HcSalePrice.ToString() ||
                         kvp.Key == RepColumn.HcProfit.ToString() )
                {
                    content.AppendLine($"<th>{kvp.Value.ToString()}</th>");
                }
                else
                    content.AppendLine($"<th></th>");
            }

            content.AppendLine("</tr>");
            content.AppendLine("</tfoot>");
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
    }

    List<ViewData> ApplySorting(List<ViewData> unsorted)
    {
        Func<ViewData, Object> orderByFunc = null;

        switch (SortBy)
        {
            case "PfName": orderByFunc = field => field.PfName; break;
            case "Symbol": orderByFunc = field => field.Symbol; break;
            case "CompanyName": orderByFunc = field => field.CompanyName; break;
            case "Units": orderByFunc = field => field.UnitsD; break;
            case "PurhaceDate": orderByFunc = field => field.PurhaceDateD; break;
            case "McPurhacePrice": orderByFunc = field => field.McPurhacePriceD; break;
            case "HcPurhacePrice": orderByFunc = field => field.HcPurhacePriceD; break;

            case "SaleDate": orderByFunc = field => field.SaleDateD; break;
            case "McSalePrice": orderByFunc = field => field.McSalePriceD; break;
            case "HcSalePrice": orderByFunc = field => field.HcSalePriceD; break;

            case "McProfit": orderByFunc = field => field.McProfitD; break;
            case "HcProfit": orderByFunc = field => field.HcProfitD; break;

            case "PurhaceRate": orderByFunc = field => field.PurhaceRateD; break;
            case "SaleRate": orderByFunc = field => field.SaleRateD; break;

            default:
                orderByFunc = field => field.CompanyName;
                break;
        }

        if (_selSettings.Contains(RepSettings.InvertedSorting.ToString()))
            return unsorted.OrderByDescending(orderByFunc).ToList();
        else
            return unsorted.OrderBy(orderByFunc).ToList();
    }

    #region VIEW DATA

    protected class ViewData
    {
        public string PfName { get; set; }
        public string Symbol { get; set; }
        public string CompanyName { get; set; }
        public string Sector { get; set; }
        public string MC { get; set; }
        public string HC { get; set; }

        public string Units { get; set; }                   public decimal UnitsD { get; set; }
        public string PurhaceDate { get; set; }             public DateOnly PurhaceDateD { get; set; }
        public string McPurhacePrice { get; set; }          public decimal  McPurhacePriceD { get; set; }
        public string HcPurhacePrice { get; set; }          public decimal HcPurhacePriceD { get; set; }
        public string PurhaceRate { get; set; }             public decimal PurhaceRateD {  get; set; }

        public string SaleDate { get; set; }                public DateOnly SaleDateD { get; set; }
        public string McSalePrice { get; set; }             public decimal  McSalePriceD { get; set; }
        public string HcSalePrice { get; set; }             public decimal HcSalePriceD { get; set; }
        public string SaleRate { get; set; }                public decimal SaleRateD { get; set; }

        public string McProfit { get; set; }
        public string HcProfit { get; set; }

        public decimal McProfitD { get {
                return (McSalePriceD - McPurhacePriceD) * UnitsD;
            }
        }

        public decimal HcProfitD { get {
                return (HcSalePriceD - HcPurhacePriceD) * UnitsD;
            }
        }

        public void ConvertViewFormat(bool total = false)
        {
            HcPurhacePrice = HcPurhacePriceD.To00();
            HcSalePrice = HcSalePriceD.To00();
            HcProfit = HcProfitD.To();

            if (total)
            {
                HcPurhacePrice += HC;
                HcSalePrice += HC;
                HcProfit += HC;
                return;
            }

            Units = UnitsD.To();
            PurhaceDate = PurhaceDateD.ToString("yyyy-MM-dd");
            McPurhacePrice = McPurhacePriceD.To00() + MC;
            PurhaceRate = PurhaceRateD.To000();

            SaleDate = SaleDateD.ToString("yyyy-MM-dd");
            McSalePrice = McSalePriceD.To00() + MC;
            SaleRate = SaleRateD.To000();

            McProfit = McProfitD.To() + MC;
        }

        public ViewData(RepDataExpSales exp)
        {
            PfName = exp.Holding.PfName;
            Symbol = exp.StockMeta.symbol;
            CompanyName = exp.StockMeta.name;
            Sector = exp.SectorDef;
            MC = UiF.Curr(exp.StockMeta.marketCurrency);

            UnitsD = exp.Holding.SH.Units;
            PurhaceDateD = exp.Holding.SH.PurhaceDate;
            McPurhacePriceD = exp.Holding.SH.McPriceWithFeePerUnit;
            HcPurhacePriceD = exp.Holding.SH.HcPriceWithFeePerUnit;
            PurhaceRateD = exp.Holding.SH.CurrencyRate;

            SaleDateD = exp.Holding.SH.Sold.SaleDate;
            McSalePriceD = exp.Holding.SH.Sold.McPriceWithFeePerUnit;
            HcSalePriceD = exp.Holding.SH.Sold.HcPriceWithFeePerUnit;
            SaleRateD = exp.Holding.SH.Sold.CurrencyRate;
        }

        public ViewData(CurrencyId homeCurrencyId)
        {
            HcPurhacePriceD = 0;
            HcSalePriceD = 0;
            UnitsD = 1;
            HC = UiF.Curr(homeCurrencyId);
        }
    }
    #endregion

    #region EXPORT

    protected async Task OnExportAsync(RepExport format)
    {
        string stringContent = string.Empty;
        string filename = string.Empty;

        switch (format)
        {
            case RepExport.CSV:
                stringContent = Local_ExportCSV();
                filename = "PfsSales_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                break;

            case RepExport.HTMLTable:
                stringContent = CreateHTMLTable();
                filename = "PfsSalesHtml_" + DateTime.Today.ToString("yyyyMMdd") + ".txt";
                break;
        }

        if (string.IsNullOrWhiteSpace(stringContent) == false)
            await BlazorDownloadFileService.DownloadFileFromText(filename, stringContent, Encoding.Default, "text/plain");

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
