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

// Allows different edit operations to initiated for Stock Meta, change name, isin, symbol etc
public partial class DlgStockMeta
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    [Parameter] public MarketId Market { get; set; } = MarketId.Unknown;
    [Parameter] public string Symbol { get; set; }

    protected int _tabActivePanel = 0;
    protected const int _tabName = 0;
    protected const int _tabMarket = 1;
    protected const int _tabClose = 2;
    protected string _btnSave = "Save";

    protected bool _fullscreen { get; set; } = false;
    protected DateTime? _date = DateTime.Now;
    protected MarketId _editMarket = MarketId.Unknown;
    protected string _editSymbol = string.Empty;
    protected string _editCompany = string.Empty;
    protected string _editISIN = string.Empty;
    protected string _editComment = string.Empty;
    protected IEnumerable<MarketMeta> _activeMarkets;

    protected override async Task OnInitializedAsync()
    {
        _activeMarkets = Pfs.Account().GetActiveMarketsMeta();

        Set();
    }

    protected void Set()
    {
        StockMeta sm = Pfs.Stalker().GetStockMeta(Market, Symbol);

        _editMarket = sm.marketId;
        _editSymbol = sm.symbol;
        _editCompany = sm.name;
        _editISIN = sm.ISIN;
    }

    protected void OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;

        MudDialog.Options.FullWidth = _fullscreen;
        MudDialog.SetOptions(MudDialog.Options);
    }

    protected void OnTabChanged(int tabID)
    {
        _tabActivePanel = tabID;
        Set();

        _btnSave = tabID switch
        {
            _tabName => "Save",
            _tabMarket => "Change",
            _tabClose => "Kill",
            _ => string.Empty
        };
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    private async Task OnBtnSaveAsync()
    {
        string errMsg = Local_Verify();

        if ( string.IsNullOrEmpty(errMsg))
        {
            switch (_tabActivePanel)
            {
                case _tabName:
                    errMsg = Local_UpdateNameIsin();
                    break;

                case _tabMarket:
                    errMsg = Local_UpdateStockMeta();
                    break;

                case _tabClose:
                    errMsg = Local_CloseStock();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(errMsg))
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Failed!", errMsg, yesText: "Ok");
        return;


        string Local_UpdateNameIsin()
        {
            StockMeta sm = Pfs.Stalker().UpdateCompanyNameIsin(Market, Symbol, DateOnly.FromDateTime(_date.Value), _editCompany, _editISIN);

            if (sm != null)
                return "";

            return "Invalid characters?";
        }

        string Local_UpdateStockMeta()
        {
            if (string.IsNullOrWhiteSpace(_editComment))
                return $"Must give comment to descript reasons";

            StockMeta sm = Pfs.Stalker().UpdateStockMeta(Market, Symbol, _editMarket, _editSymbol, _editCompany, DateOnly.FromDateTime(_date.Value), _editComment);

            if (sm != null)
                return "";

            return "Invalid characters?";
        }

        string Local_CloseStock()
        {
            if (Market == MarketId.CLOSED)
                return "Really? Double close?";

            if (string.IsNullOrWhiteSpace(_editComment))
                return $"Must give comment to descript reasons";

            StockMeta sm = Pfs.Stalker().CloseStock(Market, Symbol, DateOnly.FromDateTime(_date.Value), _editComment);

            if (sm != null)
                return "";

            return "Somekind of conflict state? Please report!";
        }

        string Local_Verify()
        {
            if (string.IsNullOrWhiteSpace(_editCompany))
                return $"Must give company name";

            if (_editMarket == MarketId.Unknown)
                return $"Must select market";

            if (string.IsNullOrWhiteSpace(_editSymbol))
                return $"Must give symbol";

            return "";
        }
    }
}
