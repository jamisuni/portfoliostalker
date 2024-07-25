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

// Allows to add new StockMeta to define market+symbol compo, with companyName (strictly no editing w this dlg!)
public partial class DlgAddNewStockMeta
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    protected MarketId _market = MarketId.Unknown;
    protected string _symbol = string.Empty;
    protected string _company = string.Empty;
    protected string _ISIN = string.Empty;

    protected IEnumerable<MarketMeta> _activeMarkets;

    protected override void OnInitialized()
    {
        _activeMarkets = Pfs.Account().GetActiveMarketsMeta();
        return;
    }

    protected async void OnBtnSearchSymbolAsync()
    {   // Can be called w unknown or specific market
        if (string.IsNullOrWhiteSpace(_symbol))
            return;

        if (_market != MarketId.Unknown && Pfs.Stalker().GetStockMeta(_market, _symbol) != null)
        {
            await Dialog.ShowMessageBox("Duplicate!", "This already exists.", yesText: "Ok");
            return;
        }

        StockMeta[] extSm = await Pfs.Stalker().FindStockExtAsync(_symbol.ToUpper(), _market);

        if (extSm == null || extSm.Length == 0)
            return;

        if (_market != MarketId.Unknown)
            extSm = extSm.Where(s => s.marketId == _market).ToArray();

        if (extSm == null || extSm.Length == 0)
            return;

        if (extSm.Length > 1)
        {
            await Dialog.ShowMessageBox("Many markets!", $"Select one of {string.Join(',', extSm.Select(s => s.marketId))} and try again", yesText: "Ok");
            return;
        }

        if (Pfs.Stalker().GetStockMeta(extSm[0].marketId, extSm[0].symbol) != null)
        {
            await Dialog.ShowMessageBox("Duplicate!", $"This already exists as only match was {extSm[0].marketId}${extSm[0].symbol}", yesText: "Ok");
            return;
        }

        _company = extSm[0].name;

        if (string.IsNullOrEmpty(_ISIN) && string.IsNullOrWhiteSpace(extSm[0].ISIN))
            _ISIN = extSm[0].ISIN;

        StateHasChanged();
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    private async Task OnBtnSaveAsync()
    {
        if (_market == MarketId.Unknown ||
            Validate.Str(ValidateId.Symbol, _symbol.ToUpper()).Fail ||
            Validate.Str(ValidateId.CompName, _company).Fail )
            return;

        StockMeta sm = Pfs.Stalker().AddNewStockMeta(_market, _symbol.ToUpper(), _company, _ISIN);

        if (sm != null)
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Failed!", "duplicate?", yesText: "Dang");
    }
}
