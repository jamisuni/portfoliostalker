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
using PfsUI.Components;
using PfsUI.Layout;
using System.Web;

namespace PfsUI.Pages;

public partial class Home
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] PfsUiState PfsUiState { get; set; }                    // !!!TODO!!! See if this can be removed and do same w existing alternatives!
    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }
    [CascadingParameter] public MainLayout Layout { get; set; }

    protected Overview _reportOverview;
    protected ReportInvested _reportInvested;
    protected ReportWeight _reportWeight;
    protected ReportDivident _reportDivident;
    protected ReportTracking _reportTrackedStocks;
    protected ReportExpHoldings _reportExpHoldings;
    protected ReportExpSales _reportExpSales;
    protected ReportExpDividents _reportExpDividents;

    protected MudTabs _tabs;
    protected int _tabId = 0;

    protected List<string> TabIds = new()
    {
        "Overview",
        "Invested",
        "Weight",
        "Dividents",
        "Export",
        "Tracking",
    };

    protected MudTabs _tabsExp;
    protected int _tabExpId = 0;
    protected const int TabExpIdHoldings = 0;
    protected const int TabExpIdSales = 1;
    protected const int TabExpIdDividents = 2;

    protected override void OnInitialized()
    {
        Layout.EvFromPageHeaderAsync += OnEvFromPageHeaderAsync;

        System.Collections.Specialized.NameValueCollection queryParams = HttpUtility.ParseQueryString(new Uri(NavigationManager.Uri).Query);

        if (queryParams != null)
        {
            string demo = queryParams.AllKeys
                                   .Where(key => string.Equals(key, "demo", StringComparison.OrdinalIgnoreCase))
                                   .SelectMany(key => queryParams.GetValues(key))
                                   .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(demo) == false && int.TryParse(demo, out int demoId) == true && demoId > 0)
                LaunchDemo(demoId-1);
        }
        return;
    }

    protected void LaunchDemo(int demoId)
    {
        var demo = BlazorPlatform.SetDemo(demoId);

        if (demo.demoZip == null)
            return;

        Result res = Pfs.Account().LoadDemo(demo.demoZip);

        Layout.NavigateToHome();
    }

    protected override void OnParametersSet()
    {
        List<PageHeader.MenuItem> headerCustomMenuItems =
        [
            // Note! Resetting menu each time we come again on page, as may have set something else by other page
            new PageHeader.MenuItem()
            {
                ID = HomePageMenuID.PF_ADD.ToString(),
                Text = "Portfolio Add",
            },
        ];

        SetTabsReportFilter();
        SetTabsReportSpeedOperation();
        Layout.SetCustomMenuItems(headerCustomMenuItems);
    }

    protected void SetTabsReportSpeedOperation()
    {
        string label = "";

        switch (TabIds[_tabId])
        {
            case "Tracking": label = "Add NEW Stock"; break;
        }
        Layout.SetSpeedOperationLabel(label);
    }

    protected void SetTabsReportFilter()
    {
        switch (TabIds[_tabId])
        {
            case "Invested": Layout.SetAsReport(ReportId.Invested); break;
            case "Weight": Layout.SetAsReport(ReportId.Weight); break;
            case "Dividents": Layout.SetAsReport(ReportId.Divident); break;

            case "Overview":
            case "Tracking":
                Layout.SetNotReport();
                break;

            case "Export":
                switch ( _tabExpId )
                {
                    case TabExpIdHoldings: Layout.SetAsReport(ReportId.ExpHoldings); break;
                    case TabExpIdSales: Layout.SetAsReport(ReportId.ExpSales); break;
                    case TabExpIdDividents: Layout.SetAsReport(ReportId.ExpDividents); break;

                    default:
                        Layout.SetNotReport();
                        break;
                }
                break;

            default:
                Layout.SetNotReport();
                break;
        }
    }

    protected void OnTabChanged(int tabId)
    {
        _tabId = tabId;
        SetTabsReportSpeedOperation();
        SetTabsReportFilter();

        Layout.PageHeaderDoStateHasChanged();
        return;
    }

    protected void OnTabExpChanged(int tabExpId)
    {
        _tabExpId = tabExpId;
        SetTabsReportSpeedOperation();
        SetTabsReportFilter();

        Layout.PageHeaderDoStateHasChanged();
        return;
    }

    protected async Task OnEvFromPageHeaderAsync(PageHeader.EvArgs args)
    {
        switch ( args.ID)
        {
            case PageHeader.EvId.MenuSel:
                
                switch ((HomePageMenuID)Enum.Parse(typeof(HomePageMenuID), args.data as string))
                {
                    case HomePageMenuID.PF_ADD:
                        {
                            var dialog = await LaunchDialog.ShowAsync<DlgPortfolioEdit>();
                            var result = await dialog.Result;

                            if (!result.Canceled)
                                PfsUiState.UpdateNavMenu();
                        }
                        break;
                }
                break;

            case PageHeader.EvId.SpeedButton:
                switch (TabIds[_tabs.ActivePanelIndex])
                {
                    case "Tracking":                    // "Add Stock"
                        await LaunchDialogAddNewStockAsync();
                        break;
                }
                break;

            case PageHeader.EvId.ReportRefresh:
                switch (TabIds[_tabs.ActivePanelIndex])
                {   // On PageHeader user has changed ReportFilters
                    case "Overview":
                        _reportOverview.ByOwner_ReloadReport();
                        break;
                    case "Invested":
                        _reportInvested.ByOwner_ReloadReport();
                        break;
                    case "Weight":
                        _reportWeight.ByOwner_ReloadReport();
                        break;
                    case "Dividents":
                        _reportDivident.ByOwner_ReloadReport();
                        break;
                    case "Export":
                        switch (_tabExpId)
                        {
                            case TabExpIdHoldings:
                                _reportExpHoldings.ByOwner_ReloadReport();
                                break;

                            case TabExpIdSales:
                                _reportExpSales.ByOwner_ReloadReport();
                                break;

                            case TabExpIdDividents:
                                _reportExpDividents.ByOwner_ReloadReport();
                                break;
                        }
                        break;
                }
                break;
        }
    }

    protected async Task LaunchDialogAddNewStockAsync()
    {
        if (Pfs.Account().AccountType == AccountTypeId.Demo)
        {
            await LaunchDialog.ShowMessageBox("Not supported!", "Sorry, demo account doesnt support adding new stocks.", yesText: "Ok");
            return;
        }

        var dialog = await LaunchDialog.ShowAsync<DlgAddNewStockMeta>("", new DialogOptions() { });
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            StockMeta sm = result.Data as StockMeta;
            Pfs.Eod().FetchStock(sm.marketId, sm.symbol);
            _reportTrackedStocks.ReloadReport();
        }
    }

    protected enum HomePageMenuID
    {
        UNKNOWN = 0,
        PF_ADD,
    }
}
