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

// This class is for select one of known/tracked stocks. Mainly to be used on Portfolio to add stock under it.
public partial class DlgStockSelect
{
    [Inject] PfsClientAccess PfsClientAccess { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    protected bool _fullscreen { get; set; } = false;

    IEnumerable<MarketMeta> _markets = null;
    List<StockMeta> _viewedStocks = null;
    string _search = "";

    MarketMeta _selMarket = null;
    object _selStock; // really StockMeta, but razor side @bind-SelectedValue requires this as object

    protected override void OnInitialized()
    {
        // All markets available on selection list
        _markets = PfsClientAccess.Account().GetActiveMarketsMeta();
    }

    protected void MarketSelectionChanged(MarketMeta market)
    {
        _selMarket = market;

        // Re-using function thats also event triggered by search box
        UpdateStocks();
    }

    protected void OnSearchChanged(string search)
    {
        UpdateStocks();
    }

    protected void UpdateStocks()
    {
        _viewedStocks = null;

        if ( string.IsNullOrWhiteSpace(_search) == true )
            return;

        IEnumerable<StockMeta> resp = PfsClientAccess.Stalker().FindStocksList(_search, _selMarket == null ? MarketId.Unknown : _selMarket.ID).ToList();

        if (resp.Count() >= 1 && resp.First().symbol == _search.ToUpper())
            _viewedStocks = [resp.First()];
        else
            _viewedStocks = resp.ToList();
    }


    protected void OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;

        MudDialog.Options.FullWidth = _fullscreen;
        MudDialog.SetOptions(MudDialog.Options);
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    private async Task DlgSelectStock()
    {
        if (_selStock == null)
            return;

        MudDialog.Close(DialogResult.Ok(_selStock as StockMeta));
    }
}
