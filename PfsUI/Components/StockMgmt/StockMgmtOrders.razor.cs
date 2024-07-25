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

using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;
using MudBlazor;

using Pfs.Types;

namespace PfsUI.Components;

// Super simple component to be used on stock report's dropdown to view orders wo editing etc (used as per specific portfolio under are atm)
public partial class StockMgmtOrders
{
    private EventHandler evChanged;
    public event EventHandler EvChanged // Called if something is edited/added/deleted
    {
        add
        {
            if (evChanged == null || !evChanged.GetInvocationList().Contains(value))
            {
                evChanged += value;
            }
        }
        remove
        {
            evChanged -= value;
        }
    }

    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService Dialog { get; set; }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    protected List<ViewStockOrders> _orders = new();

    protected override void OnInitialized()
    {
        RefreshReport();
    }

    protected void RefreshReport()
    {
        _orders = new();
        List<string> portfolios = Pfs.Stalker().GetPortfolios().Select(p => p.Name).ToList();

        StockMeta stockMeta = Pfs.Stalker().GetStockMeta(Market, Symbol);

        foreach (string pfName in portfolios)
        {
            ReadOnlyCollection<SOrder> orders = Pfs.Stalker().StockOrderList(pfName, Market, Symbol);

            foreach (SOrder order in orders)
            {
                _orders.Add(new ViewStockOrders()
                {
                    Order = order,
                    PfName = pfName,
                    Currency = UiF.Curr(stockMeta.marketCurrency),
                });
            }
        }
    }

    private async Task OnRowClickedAsync(TableRowClickEventArgs<ViewStockOrders> args)
    {
        await OnEditOrderAsync(args.Item);
    }

    public async Task FromOwner_AddNewOrderAsync()
    {
        var parameters = new DialogParameters() {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", null },
            { "Defaults", null },
            { "Edit", false }
        };

        var dialog = Dialog.Show<DlgOrderEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            evChanged?.Invoke(this, EventArgs.Empty);

            RefreshReport();
            StateHasChanged();
        }
    }

    private async Task OnEditOrderAsync(ViewStockOrders viewOrder)
    {
        var parameters = new DialogParameters() {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", viewOrder.PfName },
            { "Defaults", viewOrder.Order },
            { "Edit", true }
        };

        var dialog = Dialog.Show<DlgOrderEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            evChanged?.Invoke(this, EventArgs.Empty);

            RefreshReport();
            StateHasChanged();
        }
    }

    protected class ViewStockOrders
    {
        public string PfName { get; set; }

        public SOrder Order { get; set; }

        public string Currency { get; set; }
    }
}
