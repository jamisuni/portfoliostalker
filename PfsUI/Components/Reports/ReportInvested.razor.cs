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

public partial class ReportInvested
{
    [Inject] IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    private List<ViewReportInvestedData> _viewReport = null;

    protected CurrencyId _homeCurrency;
    protected string _HC = string.Empty;

    protected string _headerTextCompany = string.Empty;
    protected string _headerTextInvested = string.Empty;
    protected string _headerTextValuation = string.Empty;
    protected string _headerTextDivident = string.Empty;
    protected string _headerTextGain = string.Empty;

    protected override void OnParametersSet()
    {
        _viewReport = null;
        _homeCurrency = Pfs.Config().HomeCurrency;
        _HC = UiF.Curr(_homeCurrency);

        ReloadReport();
    }

    public void ByOwner_ReloadReport()
    {
        ReloadReport();
        StateHasChanged();
    }

    protected void ReloadReport()
    {
        (RepDataInvestedHeader header, List <RepDataInvested> reportData) = Pfs.Report().GetInvestedData();

        _viewReport = new();

        if (reportData == null || header == null)
            return; // "Nothing found"

        foreach (RepDataInvested inData in reportData)
        {
            ViewReportInvestedData outData = new()
            {
                d = inData,
                MC = UiF.Curr(inData.RCEod.MarketCurrency),
            };

            _viewReport.Add(outData);
        }

        _headerTextCompany = $"Company (total {_viewReport.Count()})";

        _headerTextInvested = $"Invested {header.HcTotalInvested.ToString("0")}{_HC}";

        if ( header.HcGrowthP >= 0 )
            _headerTextValuation = $"Val. +{header.HcGrowthP}% {header.HcTotalValuation}{_HC}";
        else
            _headerTextValuation = $"Val. -{header.HcGrowthP}% {header.HcTotalValuation}{_HC}";

        if (header.HcTotalDivident != null)
            _headerTextDivident = $"Div {header.HcTotalDivident.ViewHcDivP}% {header.HcTotalDivident.ViewHcDiv}{_HC}";

        _headerTextGain = $"Gain {header.HcTotalGainP}% {header.HcTotalGain.ToString("0")}{_HC}";
    }

    private void OnRowClicked(TableRowClickEventArgs<ViewReportInvestedData> data)
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

        var dialog = Dialog.Show<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected class ViewReportInvestedData
    {
        public RepDataInvested d;

        public string MC;

        public bool ShowDetails;
    }
}
