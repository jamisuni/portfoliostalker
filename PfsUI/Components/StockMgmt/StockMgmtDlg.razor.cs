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

public partial class StockMgmtDlg
{
    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    protected const string _btnNoteEdit = "Edit";
    protected const string _btnNoteSave = "Save";

    protected MudTabs _tabs;
    protected const int TabIdNote     = 0;
    protected const int TabIdHistory  = 1;
    protected const int TabIdAlarms   = 2;
    protected const int TabIdOrders   = 3;
    protected const int TabIdHoldings = 4;

    protected string _primBtnText = String.Empty;
    protected string _secBtnText = String.Empty;

    StockMgmtNote _childStockMgmtNote;
    StockMgmtAlarms _childStockMgmtAlarms;
    StockMgmtOrders _childStockMgmtOrders;
    StockMgmtHoldings _childStockMgmtHoldings;
    StockMgmtHistory _childStockMgmtHistory;

    protected StockMeta _stockMeta = null;

    protected bool _somethingIsChanged = false; // very important information, decides if caller should update report etc after dlg closes

    protected override void OnParametersSet()
    {
        _stockMeta = Pfs.Stalker().GetStockMeta(Market, Symbol);

        _primBtnText = _btnNoteEdit; // Hardcoded to default value of default tab
    }

    protected override void OnAfterRender(bool firstRender)  // Not set yet on 'OnInitialized' nor on 'OnParametersSet'
    {
        if (_childStockMgmtOrders != null)
            _childStockMgmtOrders.EvChanged += OnChanged;

        if (_childStockMgmtAlarms != null)
            _childStockMgmtAlarms.EvChanged += OnChanged;

        if (_childStockMgmtHoldings != null)
            _childStockMgmtHoldings.EvChanged += OnChanged;

        if (_childStockMgmtHistory != null)
            _childStockMgmtHistory.EvChanged += OnChanged;
    }

    protected void OnTabChanged(int tabID)
    {
        _primBtnText = string.Empty;

        switch ( tabID )
        {
            case TabIdNote:
                _primBtnText = _btnNoteEdit;
                _secBtnText = string.Empty;
                break;

            case TabIdAlarms:
            case TabIdOrders:
                _primBtnText = "Add";
                _secBtnText = string.Empty;
                break;

            case TabIdHoldings:
                _primBtnText = "Add Holding";
                _secBtnText = "Add divident";
                break;

            default:
                _primBtnText = string.Empty;
                _secBtnText = string.Empty;
                break;
        }
    }

    protected async Task OnBtnPrimAsync()
    {
        switch (_tabs.ActivePanelIndex)
        {
            case TabIdNote:

                _childStockMgmtNote.OnButtonPress();

                // Simply swap texts when pressed, default coming to tab is 'edit'
                if (_primBtnText == _btnNoteEdit)
                    _primBtnText = _btnNoteSave;
                else
                    _primBtnText = _btnNoteEdit;

                StateHasChanged();
                break;

            case TabIdAlarms:
                await _childStockMgmtAlarms.AddNewAlarmAsync();
                break;

            case TabIdOrders:
                await _childStockMgmtOrders.FromOwner_AddNewOrderAsync();
                break;

            case TabIdHoldings:
                await _childStockMgmtHoldings.FromOwner_DoAddHoldinAsync();
                break;
        }
    }

    protected async Task OnBtnSecAsync()
    {
        switch (_tabs.ActivePanelIndex)
        {
            case TabIdHoldings:
                await _childStockMgmtHoldings.FromOwner_DoAddDividentAsync();
                break;
        }
    }

    protected void OnChanged(object sender, object args)
    {
        _somethingIsChanged = true;
    }

    private void DlgClose()
    {
        if (_somethingIsChanged)
            MudDialog.Close();
        else
            MudDialog.Close(DialogResult.Cancel());
    }
}
