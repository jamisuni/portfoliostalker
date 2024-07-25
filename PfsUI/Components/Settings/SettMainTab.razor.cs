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
using BlazorDownloadFile;

using Pfs.Types;

namespace PfsUI.Components;

public partial class SettMainTab
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] PfsUiState PfsUiState { get; set; }
    [Inject] private IDialogService Dialog { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }

    protected AccountTypeId _accountTypeId = AccountTypeId.Unknown;

    protected override async Task OnParametersSetAsync()
    {
        await UpdateAllSettingsAsync();
    }

    // To be used on Init and after ClearAll/RestoreBackup
    protected async Task UpdateAllSettingsAsync()
    {
        _accountTypeId = Pfs.Account().AccountType;

        UpdateCurrencyFields();
    }

    #region CURRENCY

    protected CurrencyId _homeCurrency = CurrencyId.Unknown;
    protected ExtProviderId _selCurrencyProvider;
    protected string _latestCurrencyDate = "-missing-";             // !!!TODO!!!
    protected ExtProviderId[] _currencyProviders = null;
    protected DateOnly _currencyDate;
    protected CurrencyRate[] _currencyRates;
    protected bool _currencyFetchOnGoing = false;

    protected void UpdateCurrencyFields()
    {
        _homeCurrency = Pfs.Config().HomeCurrency;

        _selCurrencyProvider = Pfs.Config().GetActiveRatesProvider();
        _currencyProviders = Pfs.Config().GetAvailableRatesProviders();
        (_currencyDate, _currencyRates) = Pfs.Account().GetLatestRatesInfo();
    }

    protected async Task OnBtnUpdateCurrencyConversionRatesAsync()
    {
        Result resp = Pfs.Account().RefetchLatestRates();

        if (resp.Ok == false)
            await Dialog.ShowMessageBox("Cant do!", (resp as FailResult).Message, yesText: "Ok");
        else
        {
            _currencyFetchOnGoing = true;

            Task.Delay(3000).ContinueWith(_ =>
            {
                UpdateCurrencyFields();
                _currencyFetchOnGoing = false;
                StateHasChanged();
            });
        }
    }

    protected async Task OnSetCurrencyProviderAsync(ExtProviderId provider)
    {
        Result resp = Pfs.Config().SetActiveRatesProvider(provider);

        if (resp.Ok == false)
            await Dialog.ShowMessageBox("Cant do!", (resp as FailResult).Message, yesText: "Ok");
        else
        {
            _selCurrencyProvider = provider;
            StateHasChanged();
        }
    }

    protected void OnHomeCurrencySelected(CurrencyId homeCurrency)
    {
        Pfs.Config().HomeCurrency = _homeCurrency = homeCurrency;
        StateHasChanged();
    }

    #endregion

    protected async Task OnBtnImportDlgAsync()
    {
        var dialog = Dialog.Show<DlgImport>("", new DialogOptions() { CloseButton = true, FullWidth = true });
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // All done on dialog side, except need to update..
            await UpdateAllSettingsAsync();
            PfsUiState.UpdateNavMenu();
            StateHasChanged();
        }
    }

    protected async Task OnBtnClearAllAsync()
    {
        bool? result = await Dialog.ShowMessageBox("Are you sure sure?", "Clears all locally, wiping this applications Local Storage in browser. Make sure you have backup! Clear all data from application?", yesText: "CLEAR", cancelText: "Cancel");

        if (result.HasValue == false || result.Value == false)
            return;

        Pfs.Account().ClearLocally();
        PfsUiState.UpdateNavMenu();
        NavigationManager.NavigateTo("/", true);        // <= enforcing reload application and jump to idle + all components reload as empty
        StateHasChanged();
    }

    protected async Task OnBtnImportTransactionsDlgAsync()
    {
        var options = new DialogOptions { FullScreen = true, CloseButton = true, DisableBackdropClick = true };
        var parameters = new DialogParameters();

        var dialog = Dialog.Show<ImportTransactions>("Import Transactions", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
        }
    }
}
