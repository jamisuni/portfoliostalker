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

public partial class StockMgmtHoldings
{
    private EventHandler evChanged;
    public event EventHandler EvChanged
    {
        add
        {
            if (evChanged == null || !evChanged.GetInvocationList().Contains(value))
            {
                evChanged += value;
            }
        }
        remove
        {
            evChanged -= value;
        }
    }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    protected List<ViewReportHoldingsData> _viewReport;

    protected string _errMsg = "";

    protected bool _viewDividentColumn;
    protected CurrencyId _homeCurrency;

    protected override void OnParametersSet()
    {
        RefreshReport();
    }

    protected void RefreshReport()
    {
        _viewReport = new();
        _viewDividentColumn = false;
        _homeCurrency = Pfs.Config().HomeCurrency;

        Result<List<RepDataStMgHoldings>> reportData = Pfs.Report().GetStMgHoldings($"{Market}${Symbol}");

        if ( reportData.Fail )
        {
            _errMsg = (reportData as FailResult<List<RepDataStMgHoldings>>).Message;
            return;
        }

        foreach (RepDataStMgHoldings inData in reportData.Data)
        {
            ViewReportHoldingsData outData = new()
            {
                d = inData,
                ShowDetails = false,
                Units = $"{inData.Holding.Units}",
                MC = UiF.Curr(inData.RCEod.MarketCurrency),
                HC = UiF.Curr(_homeCurrency),
                DC = UiF.Curr(inData.DividentCurrency),
            };

            if (inData.Holding.Units < inData.Holding.OriginalUnits)
                outData.Units = $"{inData.Holding.Units} / {inData.Holding.OriginalUnits}";

            if (inData.TotalHoldingDivident != null)
                _viewDividentColumn = true;

            if (inData.Holding.Units < inData.Holding.OriginalUnits)
                outData.AllowEditing = false;

            _viewReport.Add(outData);
        }
    }

    private void OnRowClicked(TableRowClickEventArgs<ViewReportHoldingsData> data)
    {
        if (data == null || data.Item == null || data.Item.d.Divident == null || data.Item.d.Divident.Count == 0)
            return;

        data.Item.ShowDetails = !data.Item.ShowDetails;
    }

    protected async Task OnBtnEditNoteAsync(ViewReportHoldingsData data)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Reason to buy/etc:" },
            { "Label", "Your short notes for purhace." },
            { "Default", data.d.Holding.PurhaceNote }
        };

        var dialog = await LaunchDialog.ShowAsync<DlgSimpleEditField>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // Note-Holding PurhaceId Note 
            string cmd = $"Note-Holding PurhaceId=[{data.d.Holding.PurhaceId}] Note=[{result.Data}]";

            Result stalkerRes = Pfs.Stalker().DoAction(cmd);

            if (stalkerRes.Ok)
            {
                RefreshReport();
                StateHasChanged();
            }
            else
                await LaunchDialog.ShowMessageBox("Failed!", "Carefull with special characters, strict filtering", yesText: "Ok");
        }
    }

    protected async Task OnBtnSaleHoldingAsync(ViewReportHoldingsData data)
    {
        var parameters = new DialogParameters
        {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", data.d.PfName },
            { "TargetHolding", data.d.Holding },
            { "Defaults", new DlgSale.DefValues(MaxUnits: data.d.Holding.Units) },
        };

        // Ala Sale Holding operation == finishing trade of buy holding, and now sell holding(s)
        var dialog = await LaunchDialog.ShowAsync<DlgSale>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            RefreshReport();
            StateHasChanged();
        }
    }

    protected async Task OnBtnEditHoldingAsync(ViewReportHoldingsData data)
    {
        var parameters = new DialogParameters
        {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", data.d.PfName },
            { "Defaults", data.d.Holding },
            { "Edit", true }
        };

        var dialog = await LaunchDialog.ShowAsync<DlgHoldingsEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            evChanged?.Invoke(this, EventArgs.Empty);
        }
        RefreshReport();
        StateHasChanged();
    }
    
    protected async Task OnBtnAddDivident2HoldingAsync(ViewReportHoldingsData data)
    {
        var parameters = new DialogParameters
        {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", data.d.PfName },
            { "Holding", data.d.Holding },
        };

        var dialog = await LaunchDialog.ShowAsync<DlgDividentAdd>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            evChanged?.Invoke(this, EventArgs.Empty);
        }
        RefreshReport();
        StateHasChanged();
    }

    protected async Task OnBtnDeleteDividentAsync(ViewReportHoldingsData data, RRHoldingDivident divyData)
    {
        bool? result = await LaunchDialog.ShowMessageBox("Sure?", "Going to remove divident from this holding, and potential sold trades (but not from other holdings)", yesText: "Delete", cancelText: "Cancel");

        if (result.HasValue == false || result.Value == false)
            return;

        // // Delete-Divident PfName SRef ExDivDate PurhaceId
        string cmd = $"Delete-Divident PfName=[{data.d.PfName}] SRef=[{data.d.Holding.SRef}] ExDivDate=[{divyData.ExDivDate.ToYMD()}] PurhaceId=[{data.d.Holding.PurhaceId}]";

        Result stalkerRes = Pfs.Stalker().DoAction(cmd);

        if (stalkerRes.Ok)
        {
            RefreshReport();
            StateHasChanged();
        }
        else
            await LaunchDialog.ShowMessageBox("Failed!", (stalkerRes as FailResult).Message, yesText: "Ok");
    }

    public async Task FromOwner_DoAddHoldinAsync()
    {
        var parameters = new DialogParameters {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", null },
            { "Defaults", null },
            { "Edit", false }
        };

        var dialog = await LaunchDialog.ShowAsync<DlgHoldingsEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            StateHasChanged();
    }

    public async Task FromOwner_DoAddDividentAsync()
    {
        var parameters = new DialogParameters
        {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", null },
            { "Holding", null },
        };

        var dialog = await LaunchDialog.ShowAsync<DlgDividentAdd>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            evChanged?.Invoke(this, EventArgs.Empty);
        }
        RefreshReport();
        StateHasChanged();
    }

    protected class ViewReportHoldingsData
    {
        public RepDataStMgHoldings d;

        public string MC;

        public string HC;

        public string DC;

        public string Units;

        public decimal SortOnInvested;

        public bool ShowDetails;

        public bool AllowEditing = true;
    }
}
