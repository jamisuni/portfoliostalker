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

public partial class OverviewStocks
{
    [Inject] IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    protected List<OverviewStocksData> _reportData = null;  // Contains absolute all stocks those can be included any pages
    protected List<View> _viewStocks = null;                // all stocks those are allowed to be part of current group
    protected List<View> _viewTop15 = null;                 // Actual 15 those are shown on time per column/sorting selections
    protected bool _viewOrdersColumn = false;
    protected bool _viewHoldingsColumn = false;

    protected enum BTN
    {
        Latest,
        Alarm,
        Order,
        ExCol0,
        ExCol1,
        ExCol2,
        ExCol3,
    }

    protected Color _btnNeutral  = Color.Default;
    protected Color _btnNegative = Color.Secondary;
    protected Color _btnPositive = Color.Success;

    protected Dictionary<BTN, Color> _BTN = null;

    protected string[] _exColHdr = new string[IExtraColumns.MaxCol];    // header text for extra columns (or null)

    protected override void OnParametersSet()
    {
        _BTN = new();

        foreach (BTN btn in Enum.GetValues(typeof(BTN)))
            _BTN.Add(btn, _btnNeutral);

        _BTN[BTN.Latest] = _btnPositive;

        _reportData = Pfs.Report().GetOverviewStocks();

        // Note! Nothing happens before owner call's 'OnUpdateReport'
    }

    public void Owner_ReloadReport()
    {   // called on situation that 'FetchEodsFinished' to get latest EOD's
        _reportData = Pfs.Report().GetOverviewStocks();
        StateHasChanged();
    }

    public void OnUpdateStocks(OverviewGroups.SelChangedEvArgs args)
    {
        _viewStocks = new();
        _viewOrdersColumn = false;
        _viewHoldingsColumn = false;

        if (_reportData.Count > 0 ) // expects that always has column types on each stock, even may not have value
        {                           // allowing to capture here with column type -> header text for Extra Columns
            for ( int x = 0; x < IExtraColumns.MaxCol; x++ )
                _exColHdr[x] = RCellExtraColumn.GetHeader(_reportData[0].ExCol[x].Id);
        }

        foreach (OverviewStocksData inData in _reportData)
        {
            if (args.SRefs.Contains(inData.StockMeta.GetSRef()) == false)
                continue;

            View view = new()
            {
                d = inData,
                Active = true,
                CompanyName = inData.StockMeta.name,
            };

            if (inData.RCTotalHold != null)
                _viewHoldingsColumn = true;

            if ( view.CompanyName.Length > 25 )
                view.CompanyName = view.CompanyName.Substring(0,25);

            if (string.IsNullOrEmpty(args.OrdersFromPf))
            {   // group is not PF specific, so use best order of any PF
                if (view.d.BestOrder != null)
                {
                    _viewOrdersColumn = true;
                    view.Order = view.d.BestOrder;
                    view.OrderTT = Local_ToolTipText(view.d.BestOrder, view.d.RCEod.MarketCurrency);
                }
            }
            else
            {
                RCOrder order = view.d.PfOrder.FirstOrDefault(o => o.PfName == args.OrdersFromPf);

                if ( order != null )
                {
                    _viewOrdersColumn = true;
                    view.Order = order;
                    view.OrderTT = Local_ToolTipText(order, view.d.RCEod.MarketCurrency);
                }
            }

            _viewStocks.Add(view);
        }
        OnUpdateReport();
        return;

        string Local_ToolTipText(RCOrder order, CurrencyId marketCurrency)
        {
            switch (order.SO.Type)
            {
                case SOrder.OrderType.Buy:
                    if (order.SO.FillDate != null)
                        return $"Filled? Buy {order.SO.PricePerUnit.To00()}{UiF.Curr(marketCurrency)}";
                    else
                        return $"{order.PfName}: Buy Order {order.SO.PricePerUnit.To00()}{UiF.Curr(marketCurrency)}";

                case SOrder.OrderType.Sell:
                    if (order.SO.FillDate != null)
                        return $"Filled? Sold {order.SO.PricePerUnit.To00()}{UiF.Curr(marketCurrency)}";
                    else
                        return $"{order.PfName}: Sell Order {order.SO.PricePerUnit.To00()}{UiF.Curr(marketCurrency)}";
            }
            return "";
        }
    }

    protected void OnUpdateReport()
    {
        if (_BTN[BTN.Latest] == _btnNegative)
            _viewTop15 = _viewStocks.OrderBy(s => s.d.RCEod?.ChangeP).ToList();
        else if (_BTN[BTN.Latest] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.RCEod?.ChangeP).ToList();

        if (_BTN[BTN.ExCol0] == _btnNegative)
            _viewTop15 = _viewStocks.OrderBy(s => s.d.ExCol[0].Sort()).ToList();
        else if (_BTN[BTN.ExCol0] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.ExCol[0].Sort()).ToList();

        if (_BTN[BTN.ExCol1] == _btnNegative)
            _viewTop15 = _viewStocks.OrderBy(s => s.d.ExCol[1].Sort()).ToList();
        else if (_BTN[BTN.ExCol1] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.ExCol[1].Sort()).ToList();

        if (_BTN[BTN.ExCol2] == _btnNegative)
            _viewTop15 = _viewStocks.OrderBy(s => s.d.ExCol[2].Sort()).ToList();
        else if (_BTN[BTN.ExCol2] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.ExCol[2].Sort()).ToList();

        if (_BTN[BTN.ExCol3] == _btnNegative)
            _viewTop15 = _viewStocks.OrderBy(s => s.d.ExCol[3].Sort()).ToList();
        else if (_BTN[BTN.ExCol3] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.ExCol[3].Sort()).ToList();

        if (_BTN[BTN.Alarm] == _btnNegative)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.RRAlarm?.UnderP.HasValue).ThenByDescending(s => s.d.RRAlarm?.UnderP).ToList(); // top all w alarm, sorted by %
        else if (_BTN[BTN.Alarm] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.d.RRAlarm?.OverP.HasValue).ThenByDescending(s => s.d.RRAlarm?.OverP).ToList();

        if (_BTN[BTN.Order] == _btnNegative)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.Order?.SO.Type == SOrder.OrderType.Buy).ThenByDescending(s => s.d.RRAlarm?.UnderP).ToList();
        else if (_BTN[BTN.Order] == _btnPositive)
            _viewTop15 = _viewStocks.OrderByDescending(s => s.Order?.SO.Type == SOrder.OrderType.Sell).ThenByDescending(s => s.d.RRAlarm?.OverP).ToList();

        _viewTop15 = _viewTop15.Take(15).ToList();

        StateHasChanged();
    }

    protected async Task OnBtnStockMgmtLaunchAsync(StockMeta stockMeta)
    {
        var parameters = new DialogParameters
        {
            { "Market", stockMeta.marketId },
            { "Symbol", stockMeta.symbol }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = Dialog.Show<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
            OnUpdateReport();
    }

    protected void DoSort(BTN btn)
    {
        Color prev = _BTN[btn];

        foreach (BTN set in Enum.GetValues(typeof(BTN)))
            _BTN[set] = _btnNeutral;            // Only one may have set Negative/Positive on time

        if (prev == _btnNeutral)                // Rotate Btn: Neutral -> Negative -> Positive -> Negative
            _BTN[btn] = _btnNegative;
        else if ( prev == _btnNegative )
            _BTN[btn] = _btnPositive;
        else
            _BTN[btn] = _btnNegative;

        OnUpdateReport();
    }

    public class View
    {
        public OverviewStocksData d;
        public bool Active;

        public string CompanyName; // Limiting 25 to make sure doesnt go two lines

        public string OrderTT;
        public RCOrder Order = null; // Need ref for sorting, as diff groups uses diff one
    }
}
