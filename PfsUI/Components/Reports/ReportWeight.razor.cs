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

using MudBlazor;
using Pfs.Types;

namespace PfsUI.Components;

public partial class ReportWeight
{
    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    private List<ViewReportWeightData> _viewReport = null;

    protected bool _viewCompanyNameColumn = true;
    protected CurrencyId _homeCurrency;
    protected string _HC = string.Empty;

    protected string _headerTextCompany = string.Empty;
    protected string _headerTextCurrent = string.Empty;


    protected override void OnParametersSet()
    {
        _viewReport = null;
        _homeCurrency = Pfs.Config().HomeCurrency;
        _HC = UiF.Curr(_homeCurrency);
        _viewCompanyNameColumn = Pfs.Account().GetAppCfg(AppCfgId.HideCompanyName) == 0;

        ReloadReport();
    }

    public void ByOwner_ReloadReport()
    {
        ReloadReport();
        StateHasChanged();
    }

    protected void ReloadReport()
    {
        (RepDataWeightHeader header, List <RepDataWeight> reportData) = Pfs.Report().GetWeightData();

        _viewReport = new();

        if (reportData == null || header == null)
            return; // "Nothing found"

        foreach (RepDataWeight inData in reportData)
        {
            ViewReportWeightData outData = new()
            {
                d = inData,
                MC = UiF.Curr(inData.RCEod.MarketCurrency),
                SymbolToolTip = string.Empty
            };

            if (_viewCompanyNameColumn == false)
                outData.SymbolToolTip += $"{inData.StockMeta.name}";

            if (string.IsNullOrEmpty(inData.NoteHeader) == false)
            {
                if (_viewCompanyNameColumn == false)
                    outData.SymbolToolTip += ": ";
                outData.SymbolToolTip += $"{inData.NoteHeader}";
            }

            _viewReport.Add(outData);
        }

        _headerTextCompany = $"Company (total {_viewReport.Count()})";
        _headerTextCurrent = $"Curr {header.TotalCurrentP.ToP()}";
    }

    private void OnRowClicked(TableRowClickEventArgs<ViewReportWeightData> data)
    {
        if (data == null || data.Item == null) return;

        data.Item.ShowDetails = !data.Item.ShowDetails;
    }

    private async Task ViewStockRequestedAsync(string sRef)
    {
        var stock = StockMeta.ParseSRef(sRef);

        var parameters = new DialogParameters
        {
            { "Market", stock.marketId },
            { "Symbol", stock.symbol }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = await LaunchDialog.ShowAsync<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected class ViewReportWeightData
    {
        public RepDataWeight d;

        public string MC;

        public bool ShowDetails;

        public string SymbolToolTip;
    }
}
