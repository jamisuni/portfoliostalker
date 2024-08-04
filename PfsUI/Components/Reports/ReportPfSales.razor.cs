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

public partial class ReportPfSales
{
    [Parameter] public string PfName { get; set; } = string.Empty;

    [Inject] IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    private List<ViewSaleEntry> _viewReport = null;

    protected decimal _totalHcGrowth = 0;
    protected decimal _totalHcDiv = 0;

    protected CurrencyId _homeCurrency = CurrencyId.Unknown;
    protected string _HC = string.Empty;

    protected bool _noContent = false; // Little helper to show nothing if has nothing

    protected List<int> _speedFilterYears = new();
    protected int? _filterYear = null;

    protected List<RepDataPfSales> _reportData = null;

    protected override void OnParametersSet()
    {
        _viewReport = null;

        _homeCurrency = Pfs.Config().HomeCurrency;
        _HC = UiF.Curr(_homeCurrency);

        ReloadReport();
    }

    protected void ReloadReport()
    {
        _reportData = Pfs.Report().GetPfSales(PfName);

        if (_reportData != null)
        {
            foreach (RepDataPfSales inData in _reportData)
            {
                if (_speedFilterYears.Contains(inData.SaleDate.Year) == false)
                    _speedFilterYears.Add(inData.SaleDate.Year);
            }
        }

        if (_reportData == null || _reportData.Count() == 0)
        {
            _noContent = true;
            return;
        }
        _noContent = false;

        _viewReport = new();
        _totalHcGrowth = 0;
        _totalHcDiv = 0;

        foreach (RepDataPfSales inData in _reportData)
        {
            if (_filterYear.HasValue && inData.SaleDate.Year != _filterYear.Value)
                continue;

            ViewSaleEntry outData = new()
            {
                d = inData,
                MC = UiF.Curr(inData.StockMeta.marketCurrency),
            };

            _totalHcGrowth += inData.TotalGrowth.HcGrowthAmount;
            if ( inData.TotalDivident != null)
                _totalHcDiv += inData.TotalDivident.HcDiv;

            // DropDown -start
            outData.ViewHoldings = new();

            foreach (ReportTradeHoldings inHoldings in inData.Holdings)
            {
                ViewSaleHolding outHoldings = new()
                {
                    d = inHoldings,
                };

                outData.ViewHoldings.Add(outHoldings);
            }
            // DropDown -end

            _viewReport.Add(outData);
        }
    }

    private void OnRowClicked(TableRowClickEventArgs<ViewSaleEntry> data)
    {
        if (data == null || data.Item == null) return;

        data.Item.ShowDetails = !data.Item.ShowDetails;
    }

    protected void OnSpeedFilterChanged(int year)
    {
        _filterYear = year;
        ReloadReport();
        StateHasChanged();
    }

    protected async Task OnBtnEditHoldingNoteAsync(ViewSaleHolding data)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Reason to buy/etc:" },
            { "Label", "Your short notes for purhace." },
            { "Default", data.d.Holding.PurhaceNote }
        };

        var dialog = Dialog.Show<DlgSimpleEditField>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // Note-Holding PurhaceId Note 
            string cmd = $"Note-Holding PurhaceId=[{data.d.Holding.PurhaceId}] Note=[{result.Data}]";

            Result stalkerRes = Pfs.Stalker().DoAction(cmd);

            if (stalkerRes.Ok)
            {
                ReloadReport();
                StateHasChanged();
            }
            else
                await Dialog.ShowMessageBox("Failed!", "Carefull with special characters, strict filtering", yesText: "Ok");
        }
    }

    protected async Task OnBtnEditTradeNoteAsync(ViewSaleEntry data)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Reason to sell/etc:" },
            { "Label", "Your short notes for trade." },
            { "Default", data.d.TradeNote }
        };

        var dialog = Dialog.Show<DlgSimpleEditField>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // // Note-Trade TradeId Note
            string cmd = $"Note-Trade TradeId=[{data.d.TradeId}] Note=[{result.Data}]";

            Result stalkerRes = Pfs.Stalker().DoAction(cmd);

            if (stalkerRes.Ok)
            {
                ReloadReport();
                StateHasChanged();
            }
            else
                await Dialog.ShowMessageBox("Failed!", $"Carefull with special characters: {(stalkerRes as FailResult).Message}", yesText: "Ok");
        }
    }

    protected async Task OnBtnRemoveTradeAsync(ViewSaleEntry data)
    {
        bool? result = await Dialog.ShowMessageBox("Please confirm!", 
                "Removing trade changes everything as it was before, with holdings owned again " + Environment.NewLine + 
                "be very carefull with this as FIFO logic of sales gets easily confused if rolling back" + Environment.NewLine +
                "old sales. Only use this if just made sale and need to fix some type on it by redoing it",
                yesText: "Remove Sale", cancelText: "Cancel");

        if (result.HasValue == false || result.Value == false)
            return;

        // Delete-Trade TradeId
        string cmd = string.Format("Delete-Trade TradeId=[{0}]", data.d.TradeId);

        Result stalkerRes = Pfs.Stalker().DoAction(cmd);

        if (stalkerRes.Ok)
        {
            ReloadReport();
            StateHasChanged();
        }
        else
            await Dialog.ShowMessageBox("Failed!", $"Error: {(stalkerRes as FailResult).Message}", yesText: "Ok");
    }

    private async Task ViewStockRequestedAsync(StockMeta sm)
    {
        var parameters = new DialogParameters
        {
            { "Market", sm.marketId },
            { "Symbol", sm.symbol }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = Dialog.Show<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected class ViewSaleEntry
    {
        public RepDataPfSales d;

        public bool ShowDetails;

        public string MC;

        public List<ViewSaleHolding> ViewHoldings;
    }

    protected class ViewSaleHolding
    {
        public ReportTradeHoldings d;
    }
}
