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
using Microsoft.AspNetCore.Components.Web;
using Pfs.Types;

namespace PfsUI.Components;

// Tiny component w 'search icon' that expands to smart edit field to speed selected symbol/ticker
public partial class WidgSelSymbol // 2025-Apr-17: "https://chat.deepseek.com/" was easy winner this time over: grox, claude and perplexity
{
    [Inject] IDialogService LaunchDialog { get; set; }

    [Inject] PfsClientAccess Pfs { get; set; }

    private bool _popoverOpen;
    private string _searchText = string.Empty;
    private MudTextField<string>? _searchField;

    private void TogglePopover()
    {
        _popoverOpen = !_popoverOpen;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_popoverOpen && _searchField != null)
            await _searchField.FocusAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs args)
    {
        StockMeta sm = null;

        switch (args.Key)
        {
            case "Escape":
                _popoverOpen = false;
                _searchText = string.Empty;
                break;

            case "Enter":
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    await Task.Delay(50);
                    sm = EnforceMatch();

                    _popoverOpen = false; // Close popover after search
                    _searchText = string.Empty;
                }
                break;

            default:
                await Task.Delay(50); // Small delay (as _searchText doesnt get updated on time missing last letter)
                sm = AnyPerfectMatchYet();
                break;
        }

        if ( sm != null )
        {
            _popoverOpen = false;
            _searchText = string.Empty;
            StateHasChanged();

            await ViewStockMgmtAsync(sm);
        }

        StateHasChanged();
    }

    private StockMeta AnyPerfectMatchYet()
    {
        StockMeta sm = Pfs.Stalker().FindStock(_searchText);

        if (sm == null)
            return null;

        List<StockMeta> match = Pfs.Stalker().FindStocksList(_searchText).ToList();

        // This may actually return long list of matched, but perfect match is to return M only if no MSFT example there

        List<StockMeta> confl = match.Where(s => s.symbol.StartsWith(_searchText, StringComparison.OrdinalIgnoreCase) &&
                                !s.symbol.Equals(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        if (confl.Count > 0)
            return null;

        return sm;
    }

    private StockMeta EnforceMatch()
    {
        // By pressing enter can enforce it to exact symbol match... so searching AH takes AH even may have AHH also
        return Pfs.Stalker().FindStock(_searchText);
    }

    private async Task ViewStockMgmtAsync(StockMeta stockMeta)
    {
        var parameters = new DialogParameters
        {
            { "Market", stockMeta.marketId },
            { "Symbol", stockMeta.symbol }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = await LaunchDialog.ShowAsync<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;
    }
}
