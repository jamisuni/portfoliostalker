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
using Pfs.Client;
using Pfs.Types;

namespace PfsUI.Components;

// Shows all stock meta, PF's with tracking/holding/trading stock, testing fetch, and deleting stock off
public partial class ReportTracking
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService Dialog { get; set; }

    private List<ViewTrackingData> _viewReport;

    protected bool _anythingToDelete; // 'Delete' column w button to delete is only shown if has stocks those wo dependencies so that they can be deleted

    protected string _headerTextName = string.Empty;

    protected List<string> _allPfNames = null;

    protected override void OnParametersSet()
    {
        Pfs.Waiting().EventPfsClient2Page += OnEventPfsClient;

        _allPfNames = Pfs.Stalker().GetPortfolios().Select(p => p.Name).ToList();

        RefreshReportData();
    }

    public void ReloadReport()
    {
        RefreshReportData();
        StateHasChanged();
    }

    protected void RefreshReportData()
    {
        _anythingToDelete = false;
        _viewReport = new();
        List<RepDataTracking> reportData = Pfs.Report().GetTracking();

        foreach (RepDataTracking inData in reportData)
        {
            ViewTrackingData outData = new()
            {
                d = inData,
                allowDelete = true,
            };

            if (inData.AnyPfHoldings.Count > 0 || inData.AnyPfTrades.Count > 0 )
                outData.allowDelete = false; // nothing is allowed as if otherwise just comes back w missing logic

            _viewReport.Add(outData);
        }

        _headerTextName = string.Format("Name (total {0} stocks)", _viewReport.Count());
    }

    protected void OnEventPfsClient(object sender, IFEWaiting.FeEventArgs args)
    {
        if (Enum.TryParse(args.Event, out PfsClientEventId clientEvId) == true)
        {   // This event seams to be coming all the way from PFS Client side itself

            switch (clientEvId)
            {
                case PfsClientEventId.FetchEodsFinished:
                    ReloadReport();
                    break;
            }
        }
    }

    private async Task DoTestFetchEodAsync(string sRef)
    {
        var stock = StockMeta.ParseSRef(sRef);

        var parameters = new DialogParameters
        {
            { "Market", stock.marketId },
            { "Symbol", stock.symbol }
        };

        var dialog = Dialog.Show<DlgTestStockFetch>(sRef, parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task OnTestFetchBtnAsync()
    {
        var dialog = Dialog.Show<DlgTestStockFetch>("Test Fetch");
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    private async Task DoDeleteStockAsync(ViewTrackingData entry)
    {
        bool? result = await Dialog.ShowMessageBox("Sure?", "Going to remove stock totally!", yesText: "Aye", cancelText: "Cancel");

        if (result.HasValue == false || result.Value == false)
            return;

        foreach ( string trackPf in entry.d.AnyPfTracking )
            Pfs.Stalker().DoAction($"Unfollow-Portfolio PfName=[{trackPf}] SRef=[{entry.d.Stock.marketId}${entry.d.Stock.symbol}]");

        await Pfs.Cmd().CmdAsync($"stockmeta remove {entry.d.Stock.marketId} {entry.d.Stock.symbol}");

        _viewReport.Remove(entry);

        StateHasChanged();
    }

    private async Task DoManageStockAsync(string sRef)
    {
        var stock = StockMeta.ParseSRef(sRef);

        var parameters = new DialogParameters
        {
            { "Market", stock.marketId },
            { "Symbol", stock.symbol }
        };

        var dialog = Dialog.Show<DlgStockMeta>(sRef, parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task OnBtnStockMgmtLaunchAsync(string sRef)
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

    protected void OnAssignTrackingPf(ViewTrackingData entry, string pfName)
    {
        Result stalkerResult = Pfs.Stalker().DoAction($"Follow-Portfolio PfName=[{pfName}] SRef=[{entry.d.Stock.marketId}${entry.d.Stock.symbol}]");

        if (stalkerResult.Ok)
        {
            entry.d.AnyPfTracking.Add(pfName);
            StateHasChanged();
        }
    }

    protected class ViewTrackingData
    {
        public RepDataTracking d;

        public bool allowDelete;
    }
}
