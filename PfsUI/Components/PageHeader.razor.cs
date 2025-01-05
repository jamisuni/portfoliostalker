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

using BlazorDownloadFile;
using MudBlazor;

using Pfs.Types;
using Pfs.Client;

using static Pfs.Client.IFEEod;

namespace PfsUI.Components;

public partial class PageHeader
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }

    [Parameter] public EventCallback<EvArgs> EvFromPageHeaderAsync { get; set; }    // All outgoing events are w this!
    public record EvArgs(EvId ID, object data);

    public enum EvId {
        MenuSel,        // data == string of MenuId
        SpeedButton,    // data == -null-
        ReportRefresh   // data == ReportParams
    }

    protected bool _unsavedDataStatus = false;      // One of DataOwner's has some unsaved content pending user action to save it

    protected ReportId _reportId = ReportId.Unknown; 

    protected DateTime _stockStatusLastUpdate = DateTime.MinValue; // just to make sure we dont do UI update for btn too often

    [Inject] PfsUiState PfsUiState { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }

    [Parameter] public string Username { get; set; }


    protected string _btnLabelSpeedOperation = String.Empty;

    protected AccountTypeId _accountTypeID = AccountTypeId.Unknown;

    protected UserEventAmounts _userEvAmounts = null;

    protected List<MenuItem> _customSettMenuItems = null;   // Items those specific page wants to add to main menu of application

    // 'Stock Status' shows if local client has latest assumed eod data for stock this users account is tracking
    protected string _stockStatusText = "uninitialized";
    protected bool _stockStatusDisable = true;
    protected bool _stockStatusBusy = false;

    // For portfolio selection menu, has all PfName's and non-empty PfName if specific PF is selected
    protected List<string> _pfNames = new();
    protected string _pfMenuLabel = "";

    protected override void OnInitialized()
    {   // Cant be on 'OnParametersSet'
        _userEvAmounts = Pfs.Account().GetUserEventAmounts();

        PfsUiState.OnMenuUpdated += OnPfMenuUpdated;

        Pfs.Client().EventPfsClient2PHeader += OnEventPfsClient;
    }

    protected static bool _wizardRunAlready = false;

    protected override void OnParametersSet()
    {
        _accountTypeID = Pfs.Account().AccountType;
        
        ReloadPfMenu();
        LoadCustomReportFilters();
        UpdateStockStatus();

        if (Pfs.Config().HomeCurrency == CurrencyId.Unknown && _wizardRunAlready == false)
        {   // On startup a wizard is run one time if looks like PFS is not setup yet on use
            _wizardRunAlready = true;
            _ = OnRunSetupWizardAsync();
        }

        if ( Pfs.Config().HomeCurrency != CurrencyId.Unknown )
        {
            var ratesInfo = Pfs.Eod().GetLatestRatesInfo();

            if (ratesInfo.date < Pfs.Platform().GetCurrentLocalDate().AddDays(-1))
                // Automatically fetch latest currency rates if new one should be now available
                _ = Pfs.Eod().RefetchLatestRates();
        }
    }

    protected void ReloadPfMenu()
    {
        _pfNames = Pfs.Stalker().GetPortfolios().Select(p => p.Name).ToList();
    }

    protected void OnPfMenuUpdated()
    {
        ReloadPfMenu();
        StateHasChanged();
    }

    public void OnBtnPfChanged(string pfName)
    {
        if (_pfMenuLabel == pfName)
            return;

        _pfMenuLabel = pfName;

        NavigationManager.NavigateTo(NavigationManager.BaseUri + "Portfolio/" + pfName);
    }

    protected void UpdateStockStatus()
    {
        StockExpiredStatus status = Pfs.Eod().GetExpiredEodStatus();

        _stockStatusDisable = false;

        if (status.ndStocks == 0 && status.expiredStocks == 0)
        {
            _stockStatusText = "Up-to-date";
            _stockStatusDisable = true;
        }
        else if (status.expiredStocks > 0 && status.ndStocks == 0)
        {
            _stockStatusText = string.Format($"Expired {status.expiredStocks}/{status.totalStocks}");
        }
        else if (status.expiredStocks == 0 && status.ndStocks > 0)
        {
            _stockStatusText = string.Format($"OK {status.totalStocks - status.ndStocks} N/D {status.ndStocks}");
        }
        else // if (status.expiredStocks > 0 && status.ndStocks > 0)
        {
            _stockStatusText = string.Format($"Exp {status.expiredStocks}, N/D {status.ndStocks}");
        }
    }

    protected void OnBtnStockStatus()
    {
        var options = new DialogOptions { CloseButton = true };

        if (_stockStatusBusy == false)
        {   // Normal case where not busy so press allows to do fetch
            (int fetchAmount, int pendingAmount) = Pfs.Eod().FetchExpiredStocks();

            if (fetchAmount > 0)
            {
                var parameters = new DialogParameters
                {
                    { "PendingAmount",  pendingAmount },
                };

                _ = LaunchDialog.ShowAsync<DlgFetchStats>(null, parameters, options);
            }
        }
        else
        {   // If busy fetching currently ala spinning then allow reopen dialog
            UpdateStockStatus();
            StateHasChanged();
            _ = LaunchDialog.ShowAsync<DlgFetchStats>(null, new DialogParameters(), options);
        }
    }

    protected void OnEventPfsClient(object sender, IFEClient.FeEventArgs args)
    {
        if ( Enum.TryParse(args.Event, out PfsClientEventId clientEvId) == true )
        {   // This event seams to be coming all the way from PFS Client side itself

            switch (clientEvId)
            {
                case PfsClientEventId.StartupWarnings:
                    {
                        List<string> warnings = (List<string>)args.Data;
                        var parameters = new DialogParameters {
                            { "Warnings",  string.Join(Environment.NewLine, warnings) },
                        };
                        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };
                        _ = LaunchDialog.ShowAsync<DlgStartupWarnings>(null, parameters, maxWidth);
                    }
                    break;

                case PfsClientEventId.StatusUnsavedData:
                    // This indicates that some component has unsaved data, so refresh display to show save button
                    _unsavedDataStatus = (bool)args.Data;
                    StateHasChanged();
                    break;

                case PfsClientEventId.FetchEodsStarted:
                    _stockStatusBusy = true;
                    UpdateStockStatus();
                    StateHasChanged();
                    break;

                case PfsClientEventId.StoredLatestEod:
                    // If busy fetching then this is nice place to keep numbers rolling
                    if (_stockStatusBusy == false)
                        break;

                    if (_stockStatusLastUpdate.Second == Pfs.Platform().GetCurrentLocalTime().Second)
                        break;

                    _stockStatusLastUpdate = Pfs.Platform().GetCurrentLocalTime();
                    UpdateStockStatus();
                    StateHasChanged();
                    break;

                case PfsClientEventId.FetchEodsFinished:
                    // Remove potential spinned from Stock Status - and update counts per what happened
                    _stockStatusBusy = false;
                    UpdateStockStatus();
                    StateHasChanged();
                    break;

                case PfsClientEventId.StockAdded:
                case PfsClientEventId.StockUpdated:
                    UpdateStockStatus();
                    StateHasChanged();
                    break;

                case PfsClientEventId.UserEventStatus:

                    _userEvAmounts = (UserEventAmounts)args.Data;
                    StateHasChanged();
                    break;
            }
        }
    }

    public void DoStateHasChanged()
    {
        StateHasChanged();
    }

    protected void DoSaveData()
    {
        Pfs.Account().SaveData();

        StateHasChanged();
    }

    protected async Task OnOpenDlgPendingEventsAsync()
    {
        if (_userEvAmounts.Total == 0)
            return;

        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Large, FullWidth = true };
        var parameters = new DialogParameters();

        var dialog = await LaunchDialog.ShowAsync<DlgUserEvents>("User Events", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
        }
    }

    protected async Task OnCustomSettMenuSelAsync(string menuItemId)
    {
        // From PageHeader -> MainLayout -> Page itself as a Layout.EvCustomMenuItemSelAsync
        await EvFromPageHeaderAsync.InvokeAsync(new EvArgs(EvId.MenuSel, (object)menuItemId));
    }

    public void SetLabelSpeedOperation(string label)
    {
        _btnLabelSpeedOperation = label;
    }

    private async Task OnBtnSpeedOperationAsync()
    {
        await EvFromPageHeaderAsync.InvokeAsync(new EvArgs(EvId.SpeedButton, null));
    }

    public void NavigateToHome() // Note! This is only for mild version, do not call here if wants logout enforced sametime
    {
        _pfMenuLabel = string.Empty;
        NavigationManager.NavigateTo(NavigationManager.BaseUri);
    }

    //******************************** REPORT & REPORT FILTERS

    protected string[] _customReportFilters = Array.Empty<string>();

    public void SetReport(ReportId id)
    {
        _reportId = id;
    }

    protected void LoadCustomReportFilters()
    {
        _customReportFilters = Pfs.Report().ListReportFilters();
    }

    protected async Task OnReportFilterSelAsync(string item)
    {
        if (item == ReportFilters.DefaultTag)
        {   // Just getting defaults is going to make it to be used on reports
            Pfs.Report().GetReportFilters(ReportFilters.DefaultTag);
            await EvFromPageHeaderAsync.InvokeAsync(new EvArgs(EvId.ReportRefresh, null));
        }
        else if (item == ReportFilters.CurrentTag)
        {
            var options = new DialogOptions { CloseButton = true };
            var parameters = new DialogParameters
            {
                { "ReportId", _reportId },
                { "Filters", Pfs.Report().GetReportFilters(ReportFilters.CurrentTag) }
            };

            var dialog = await LaunchDialog.ShowAsync<DlgReportFilters>("Filters", parameters, options);
            var result = await dialog.Result;

            LoadCustomReportFilters(); // can happen either case as separate save btn

            if (!result.Canceled)
            {
                Pfs.Report().UseReportFilters(result.Data as ReportFilters);
                await EvFromPageHeaderAsync.InvokeAsync(new EvArgs(EvId.ReportRefresh, null));
            }
            return;
        }
        else
        {   // Get is enough to activate it for 'current'
            Pfs.Report().GetReportFilters(item);
            await EvFromPageHeaderAsync.InvokeAsync(new EvArgs(EvId.ReportRefresh, null));
        }
    }

    //********************************

    public void SetCustomMenuItems(List<PageHeader.MenuItem> menuItems)
    {
        _customSettMenuItems = menuItems;
    }

    protected async Task OnRunSetupWizardAsync()
    {
        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true };

        var parameters = new DialogParameters();

        var dialog = await LaunchDialog.ShowAsync<DlgSetupWizard>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            PfsUiState.UpdateNavMenu();
            StateHasChanged();

            bool? msgRes = await LaunchDialog.ShowMessageBox("Wanna import your transactions?", "Has support for very limited amount of brokers/banks atm, but " +
                                                        "happy to add more if yours is missing. Want try import your own actions from banks CSV export file?", yesText: "Ok", cancelText: "Cancel");

            if (msgRes.HasValue == false || msgRes.Value == false)
                return;

            var options2 = new DialogOptions { FullScreen = true, CloseButton = true, BackdropClick = false };
            var parameters2 = new DialogParameters();

            var dialog2 = await LaunchDialog.ShowAsync<ImportTransactions>("Import Transactions", parameters2, options2);
            var result2 = await dialog.Result;

            if (!result2.Canceled)
            {
            }
        }
    }

    protected async Task OnUsernameClickedAsync()
    {
        var dialog = await LaunchDialog.ShowAsync<DlgLogin>();
        var result = await dialog.Result;

        if (result.Canceled)
            return;
    }

    protected void OnBtnExitDemo()
    {
        PfsUiState.UpdateNavMenu();
        NavigationManager.NavigateTo(NavigationManager.BaseUri, true);        // <= enforcing reload application and jump to idle + all components reload as empty
        StateHasChanged();
    }

    protected async Task OnExportAccountBackupZipFile()
    {
        // Alternative, not attempted yet: https://www.meziantou.net/generating-and-downloading-a-file-in-a-blazor-webassembly-application.htm
        byte[] zip = Pfs.Account().ExportAccountBackupAsZip();

        string fileName = "PfsV2Export_" + DateTime.Today.ToString("yyyyMMdd") + ".zip";
        await BlazorDownloadFileService.DownloadFile(fileName, zip, "application/zip");
    }

    protected async Task OnAboutAsync()
    {
        var parameters = new DialogParameters();

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = await LaunchDialog.ShowAsync<DlgAbout>("", parameters, maxWidth);
        await dialog.Result;
    }

    protected async Task OnOpenSettingsAsync()
    {
        var parameters = new DialogParameters();

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = await LaunchDialog.ShowAsync<SettingsDlg>("", parameters, maxWidth);
        await dialog.Result;
    }

    public class MenuItem
    {
        public string ID { get; set; }      // Page specific item ID, example string version of its MenuID ENUM's

        public string Text { get; set; }    // Menu Text visible for user
    }
}
