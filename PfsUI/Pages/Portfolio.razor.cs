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
using PfsUI.Components;
using PfsUI.Layout;

namespace PfsUI.Pages;

public partial class Portfolio
{
    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] PfsUiState PfsUiState { get; set; }
    [Parameter] public string PfName { get; set; }
    [CascadingParameter] public MainLayout Layout { get; set; }

    protected MudTabs _tabs;
    protected const int TabIdAllStocks = 0;
    protected const int TabIdSales = 1;

    protected ReportPfStocks _reportPfStocks;
    protected ReportPfSales _reportPfSales;

    protected override void OnInitialized()
    {
        Layout.EvFromPageHeaderAsync += OnEvFromPageHeaderAsync;

        Pfs.Client().EventPfsClient2Page += OnEventPfsClient;

        OnTabChanged(0);
    }

    protected override void OnParametersSet()
    {
        List<PageHeader.MenuItem> headerCustomMenuItems =
        [
            // Note! Resetting menu each time we come again on page, as may have set something else by other page
            new PageHeader.MenuItem()
            {
                ID = PfPageMenuID.PF_EDIT.ToString(),
                Text = "Pf Rename",
            },
            new PageHeader.MenuItem()
            {
                ID = PfPageMenuID.PF_DELETE.ToString(),
                Text = "Pf Delete",
            },
            new PageHeader.MenuItem()
            {
                ID = PfPageMenuID.PF_TOP.ToString(),
                Text = "Pf Top",
            },
        ];

        Layout.SetCustomMenuItems(headerCustomMenuItems);
        
    }

    protected void OnTabChanged(int tabId) // carefull, this is called atm from OnInitialized()
    {
        switch (tabId)
        {
            case TabIdAllStocks:
                {
                    Layout.SetSpeedOperationLabel("Add Stock");
                    Layout.SetAsReport(ReportId.PfStocks);
                }
                break;

            case TabIdSales:
                {
                    Layout.SetSpeedOperationLabel();
                    Layout.SetAsReport(ReportId.PfSales);
                }
                break;

            default:
                Layout.SetSpeedOperationLabel();
                Layout.SetNotReport();
                break;
        }
        Layout.PageHeaderDoStateHasChanged();
        return;
    }

    protected async Task OnEvFromPageHeaderAsync(PageHeader.EvArgs args)
    {
        switch ( args.ID )
        {
            case PageHeader.EvId.MenuSel:

                switch ((PfPageMenuID)Enum.Parse(typeof(PfPageMenuID), args.data.ToString()))
                {

                    case PfPageMenuID.PF_DELETE:
                        {
                            bool? result = await LaunchDialog.ShowMessageBox("Are you sure?", "Delete portfolio from account?", yesText: "Ok", cancelText: "Cancel");

                            if (result.HasValue == false || result.Value == false)
                                return;

                            Result stalkerResult = Pfs.Stalker().DoAction($"Delete-Portfolio PfName=[{PfName}]");

                            if (stalkerResult.Fail)
                            {
                                await LaunchDialog.ShowMessageBox("Delete failed!", "Cant delete portfolios those has ANY content / references!", yesText: "Ok");
                            }
                            else
                            {
                                // Easiest just to move back to account page, as dont wanna end up trying to reload this nor quess where to go
                                Layout.NavigateToHome();
                            }
                        }
                        break;

                    case PfPageMenuID.PF_EDIT:
                        {
                            var parameters = new DialogParameters
                            {
                                { "EditCurrPfName", PfName }
                            };
                            var dialog = await LaunchDialog.ShowAsync<DlgPortfolioEdit>("", parameters);
                            var result = await dialog.Result;

                            if (!result.Canceled)
                                Layout.NavigateToHome();
                        }
                        break;

                    case PfPageMenuID.PF_TOP:
                        {
                            Result stalkerResult = Pfs.Stalker().DoAction($"Top-Portfolio PfName=[{PfName}]");

                            if (stalkerResult.Ok)
                                PfsUiState.UpdateNavMenu();
                        }
                        break;
                }
                break;

            case PageHeader.EvId.SpeedButton: 
                switch (_tabs.ActivePanelIndex)
                {
                    case TabIdAllStocks:                    // "Add Stock"
                        await LaunchDialogAddNewStockAsync();
                        break;
                }
                break;

            case PageHeader.EvId.ReportRefresh:
                switch (_tabs.ActivePanelIndex)
                {   // On PageHeader user has changed ReportParams those passed there for active report
                    case TabIdAllStocks:
                        _reportPfStocks.ReloadReport();
                        break;
                }
                break;
        }
    }

    protected void OnEventPfsClient(object sender, IFEClient.FeEventArgs args)
    {
        if (Enum.TryParse(args.Event, out PfsClientEventId clientEvId) == true)
        {   // This event seams to be coming all the way from PFS Client side itself

            switch (_tabs.ActivePanelIndex)
            {
                case TabIdAllStocks:
                    {
                        switch (clientEvId)
                        {
                            case PfsClientEventId.FetchEodsFinished:
                                _reportPfStocks.ReloadReport();
                                break;
                        }
                    }
                    break;
            }
        }
    }

    protected async Task LaunchDialogAddNewStockAsync()
    {
        if (Pfs.Account().AccountType == AccountTypeId.Demo)
        {
            await LaunchDialog.ShowMessageBox("Not supported!", "Sorry, demo account doesnt support adding new stocks.", yesText: "Ok");
            return;
        }

        // !!!NOTE!!! MainLayout has default's for DialogOptions
        var dialog = await LaunchDialog.ShowAsync<DlgStockSelect>("", new DialogOptions() { });
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            StockMeta selStock = result.Data as StockMeta;

            // Follow-Portfolio PfName SRef
            Result stalkerResult = Pfs.Stalker().DoAction($"Follow-Portfolio PfName=[{PfName}] SRef=[{selStock.marketId}${selStock.symbol}]");

            if (stalkerResult.Fail)
                await LaunchDialog.ShowMessageBox("Operation failed!", (stalkerResult as FailResult).Message, yesText: "Ok");
            else
            {   // Stock added, so lets make sure stock table is up to date (Later! Could add checking that its visible atm)
                _reportPfStocks.ReloadReport();

                StateHasChanged();
            }
        }
    }

    protected enum PfPageMenuID
    {
        UNKNOWN = 0,
        PF_EDIT,
        PF_DELETE,
        PF_TOP,
    }
}
