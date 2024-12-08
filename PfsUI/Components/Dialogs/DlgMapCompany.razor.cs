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
using System.Text;

namespace PfsUI.Components;

// Part of Broker Importing, allows to map/save broker given company information against StockMeta+ExtMeta
public partial class DlgMapCompany
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    [Parameter] public MapCompany[] Companies { get; set; } = null;

    protected List<ViewCompanies> _viewCompanies = new();
    protected List<MarketId> _allActiveMarketsId = null;

    protected bool _isBusy = false;

    protected bool _allowAddISINs = false;

    // Later!! Someday make sure ISIN match is 100% used, and dont even allow any remapping buttons if thats match

    protected class ViewCompanies
    {
        public MapCompany Company { get; set; }
        public MarketId ManualMarket { get; set; }
        public string ManualCompany { get; set; }
        public bool ManualSearch { get; set; }
    }

    protected override void OnInitialized()
    {
        foreach (MapCompany company in Companies)
        {
            ViewCompanies entry =new()
            {
                Company = company,
                ManualSearch = false,
            };

            _viewCompanies.Add(entry);
        }

        _viewCompanies = _viewCompanies.OrderBy(c => c.Company.ExtSymbol).ToList();

        _allActiveMarketsId = Pfs.Account().GetActiveMarketsMeta().Select(m => m.ID).ToList();

        // Spinning fetch on background
        _ = FindCompanies();
    }

    protected async Task FindCompanies()
    {
        await Task.CompletedTask;

        bool allowAdd = false;
        _isBusy = true;

        foreach ( ViewCompanies company in _viewCompanies )
        {
            if (company.Company.IsMatchingISIN() == false)
                allowAdd = true;

            if (company.Company.StockMeta != null)
                // Has already set something, so we go with that one
                continue;

            if (string.IsNullOrWhiteSpace(company.Company.ExtSymbol) == true)
                // Cant do automatic search for this one
                continue;

            StockMeta sm = Pfs.Stalker().FindStock(company.Company.ExtSymbol, company.Company.ExtMarketCurrencyId, company.Company.ExtISIN);

            if (sm == null)
                continue;

            company.Company.StockMeta = sm;


            StateHasChanged();
        }

        _allowAddISINs = allowAdd;
        _isBusy = false;
        StateHasChanged();
        return;
    }

    protected async Task OnTestFetchBtnAsync()
    {
        var dialog = await LaunchDialog.ShowAsync<DlgTestStockFetch>("Test Fetch");
        await dialog.Result;
    }

    protected async Task OnBtnCompanyBTAAsync(ViewCompanies company)
    {
        StringBuilder sb = new();

        if (string.IsNullOrEmpty(company.Company.ExtISIN) == false)
            sb.AppendLine($"ISIN  = {company.Company.ExtISIN}");

        if (company.Company.ExtMarketId != MarketId.Unknown)
            sb.AppendLine($"Market = {company.Company.ExtMarketId}");

        if (string.IsNullOrEmpty(company.Company.ExtSymbol) == false)
            sb.AppendLine($"Symbol = {company.Company.ExtSymbol}");

        if (string.IsNullOrEmpty(company.Company.ExtCompanyName) == false)
            sb.AppendLine($"Name  = {company.Company.ExtCompanyName}");

        if (company.Company.ExtMarketCurrencyId != CurrencyId.Unknown)
            sb.AppendLine($"Curr  = {company.Company.ExtMarketCurrencyId}");

        var parameters = new DialogParameters
        {
            { "Title",  "Company info" },
            { "Text",  sb.ToString() }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };

        await LaunchDialog.ShowAsync<DlgSimpleTextViewer>("", parameters, maxWidth);
    }

    protected async Task OnBtnAutomOffAsync(ViewCompanies company)
    {
        if( company.Company.IsMatchingISIN() )
        {
            bool? result = await LaunchDialog.ShowMessageBox("Are you sure sure?", "ISIN's are matching, its pretty sure looking match! Do manual?", yesText: "YES", cancelText: "Cancel");

            if (result.HasValue == false || result.Value == false)
                return;
        }
        company.Company.StockMeta = null;
        StateHasChanged();
    }
    
    protected void OnBtnSaveAllNewCompanies()
    {
        foreach ( ViewCompanies company in _viewCompanies)
        {
            if (company.Company.StockMeta != null)
                continue;

            if (company.ManualMarket.IsReal() == false || string.IsNullOrWhiteSpace(company.ManualCompany) == true)
                continue;

            company.Company.StockMeta = Pfs.Stalker().AddNewStockMeta(company.ManualMarket, company.Company.ExtSymbol, company.ManualCompany, company.Company.ExtISIN);
        }
        StateHasChanged();
    }

    protected void OnBtnAddInstantlyAsync(ViewCompanies company)
    {   // Could not find per given Symbol, but assuming market & company name is given here we do add as new StockMeta
        if (company.ManualMarket.IsReal() == false || string.IsNullOrWhiteSpace(company.ManualCompany) == true)
            return;

        company.Company.StockMeta = Pfs.Stalker().AddNewStockMeta(company.ManualMarket, company.Company.ExtSymbol, company.ManualCompany, company.Company.ExtISIN);
        StateHasChanged(); // Previous may return 'null' if has conflict, and stock stays unmapped
    }

    protected async Task OnBtnEnforceManualMatchAsync(ViewCompanies company)
    {
        var dialog = await LaunchDialog.ShowAsync<DlgStockSelect>("", new DialogOptions() { });
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            company.Company.StockMeta = result.Data as StockMeta;
            company.ManualSearch = true;
            StateHasChanged();
        }
    }

    protected async Task OnBtnOnlineSearchAllAsync()
    {
        _isBusy = true;
        StateHasChanged();
        foreach (ViewCompanies company in _viewCompanies)
        {
            if (company.Company.StockMeta != null)
                continue;

            if (company.ManualMarket != MarketId.Unknown || string.IsNullOrWhiteSpace(company.ManualCompany) == false)
                continue;

            StockMeta[] extSm = await Pfs.Stalker().FindStockExtAsync(company.Company.ExtSymbol, company.Company.ExtMarketId, company.Company.ExtMarketCurrencyId);

            if (extSm == null || extSm.Length != 1)
                continue;

            company.ManualMarket = extSm[0].marketId;
            company.ManualCompany = extSm[0].name;
        }
        _isBusy = false;
        StateHasChanged();
    }

    protected void OnBtnLocalResetAll()
    {
        _viewCompanies.ForEach(a => a.Company.StockMeta = null);

        // Spinning fetch on background
        _ = FindCompanies();
    }

    protected async Task OnBtnOnlineSearchAsync(ViewCompanies company)
    {
        if (company.ManualMarket != MarketId.Unknown && Pfs.Stalker().GetStockMeta(company.ManualMarket, company.Company.ExtSymbol) != null)
        {
            await LaunchDialog.ShowMessageBox("Duplicate!", "This symbol already exists for this market, press remapping", yesText: "Ok");
            return;
        }

        StockMeta[] extSm = await Pfs.Stalker().FindStockExtAsync(company.Company.ExtSymbol, company.ManualMarket);

        if (extSm == null || extSm.Length == 0)
        {
            await LaunchDialog.ShowMessageBox("Couldnt find match", "Maybe different market? Or create manually", yesText: "Ok");
            return;
        }

        if (company.ManualMarket != MarketId.Unknown)
            extSm = extSm.Where(s => s.marketId == company.ManualMarket).ToArray();

        if (extSm.Length == 0)
        {
            await LaunchDialog.ShowMessageBox("Couldnt find match", "Maybe different market? Or create manually", yesText: "Ok");
            return;
        }

        if (extSm.Length > 1)
        {   // Got multiple matches, but if ISIN given then we can use its CA123... start to narrow down
            if ( string.IsNullOrWhiteSpace(company.Company.ExtISIN) == false)
            {
                StockMeta sel = DecideMarketPerISIN(company.Company.ExtISIN, extSm);

                if (sel != null)
                    extSm = [sel];
            }

            if (extSm.Length > 1)
            {
                await LaunchDialog.ShowMessageBox("Many markets!", $"Select one of {string.Join(',', extSm.Select(s => s.marketId))} and try again", yesText: "Ok");
                return;
            }
        }

        if (Pfs.Stalker().GetStockMeta(extSm[0].marketId, extSm[0].symbol) != null)
        {
            await LaunchDialog.ShowMessageBox("Duplicate!", $"This already exists as only match was {extSm[0].marketId}${extSm[0].symbol}", yesText: "Ok");
            return;
        }

        company.ManualMarket = extSm[0].marketId;
        company.ManualCompany = extSm[0].name;

        StateHasChanged();
        return;
        
        StockMeta DecideMarketPerISIN(string ISIN, StockMeta[] available)
        {
            switch ( ISIN )
            {
                case string s when s.StartsWith("CA"):
                    return available.FirstOrDefault(m => m.marketId == MarketId.TSX);
            }
            return null;
        }
    }

    protected void AddMapping(ViewCompanies company)
    {
        Pfs.Stalker().AddSymbolSearchMapping(company.Company.ExtSymbol, company.Company.StockMeta.marketId, company.Company.StockMeta.symbol, "Added on broker import by user");
        company.ManualSearch = false;
        StateHasChanged();
    }

    protected void AddISIN(ViewCompanies company)
    {
        if (company.Company.IsMatchingISIN())
            return;

        company.Company.StockMeta = Pfs.Stalker().UpdateCompanyNameIsin(company.Company.StockMeta.marketId, company.Company.StockMeta.symbol, Pfs.Platform().GetCurrentLocalDate(), 
                                                                        company.Company.StockMeta.name, company.Company.ExtISIN);
        StateHasChanged();
    }

    protected void AddAllISIN()
    {
        foreach ( ViewCompanies company in _viewCompanies)
        {
            if (company.Company.StockMeta != null && company.Company.IsMatchingISIN() == false)
                company.Company.StockMeta = Pfs.Stalker().UpdateCompanyNameIsin(company.Company.StockMeta.marketId, company.Company.StockMeta.symbol, Pfs.Platform().GetCurrentLocalDate(),
                                                                                company.Company.StockMeta.name, company.Company.ExtISIN);
        }
        _allowAddISINs = false;
        StateHasChanged();
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }

    private async Task DlgDoneAsync()
    {
        if (_viewCompanies.Any(c => c.Company.StockMeta == null && c.ManualMarket.IsReal() && string.IsNullOrWhiteSpace(c.ManualCompany) == false))
        {
            bool? result = await LaunchDialog.ShowMessageBox("Unsaved companies?", "By continuing those get saved permanently for tracked stocks", yesText: "Save", cancelText: "Cancel");

            if (result.HasValue == false || result.Value == false)
                return;

            OnBtnSaveAllNewCompanies();
        }

        MudDialog.Close(DialogResult.Ok(_viewCompanies.Where(c => c.Company.StockMeta != null).Select(c => c.Company).ToList()));
    }
}
