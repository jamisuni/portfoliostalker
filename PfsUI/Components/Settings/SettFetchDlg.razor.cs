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

// Dedicated Dlg for just SettFetch rule editing, under settings
public partial class SettFetchDlg
{
    // Decision! Dont try enforce limits on editing time, just give error on saving if symbols rule try have many providers

    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    [Parameter] public ProvFetchCfg Cfg { get; set; } = null;

    protected bool _fullscreen { get; set; } = false;

    protected IEnumerable<MarketMeta> _availableMarkets;

    protected MarketId _editMarket = MarketId.Unknown;
    protected bool _lockMarket = false;
    protected string _editStocks;

    protected IEnumerable<ExtProviderId> _availableProviders = new List<ExtProviderId>();

    private IEnumerable<ExtProviderId> _editProviders { get; set; } = new HashSet<ExtProviderId>();

    protected MarketId EditMarket { 
        get { 
            return _editMarket; 
        } 
        set { 
            _editMarket = value;
            _lockMarket = true;
            _availableProviders = Pfs.Config().GetActiveEodProviders(_editMarket);
        } 
    }

    protected override async Task OnInitializedAsync()
    {
        await Task.CompletedTask;

        _availableMarkets = Pfs.Account().GetActiveMarketsMeta();

        if ( Cfg != null )
        {   // Edit Existing Rule (cant edit market)
            EditMarket = Cfg.market;
            _editProviders = Cfg.providers;
            _editStocks = Cfg.symbols;
        }
    }

    protected async Task OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;
        await MudDialog.SetOptionsAsync(MudDialog.Options with { FullScreen = fullscreen });
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }

    private async Task OnBtnSaveAsync()
    {
        if (_editMarket.IsReal() == false || _editProviders.Count() == 0)
            return;

        if (string.IsNullOrWhiteSpace(_editStocks) == false && _editProviders.Count() > 1)
        {
            await LaunchDialog.ShowMessageBox("Cant do!", "Rule that defines symbol(s) cant have multiple providers!", yesText: "Ok");
            return;
        }

        ProvFetchCfg ret = new ProvFetchCfg(_editMarket, _editStocks, _editProviders.ToArray() );

        MudDialog.Close(DialogResult.Ok(ret));
    }
}
