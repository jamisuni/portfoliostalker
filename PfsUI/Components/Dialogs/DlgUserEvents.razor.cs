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
using System.Collections.Immutable;

namespace PfsUI.Components;

public partial class DlgUserEvents
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    protected override void OnInitialized()
    {
    }


    private List<ViewReportUserEventsData> _viewReport;

    protected override void OnParametersSet()
    {
        RefreshReport();
    }

    protected void RefreshReport()
    {
        _viewReport = new();
        List<RepDataUserEvents> reportData = Pfs.Account().GetUserEventsData();

        foreach (RepDataUserEvents inData in reportData)
        {
            ViewReportUserEventsData outData = new()
            {
                d = inData,
            };

            switch (outData.d.Type)
            {
                case UserEventType.OrderBuyExpired:
                    outData.Desc = $"Buy {inData.Order.Units.To()}pcs with {inData.Order.PricePerUnit.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    outData.Operation1 = "Re-open";
                    break;

                case UserEventType.OrderSellExpired:
                    outData.Desc = $"Sell {inData.Order.Units.To()}pcs with {inData.Order.PricePerUnit.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    outData.Operation1 = "Re-open";
                    break;

                case UserEventType.OrderBuy:
                    outData.Desc = $"Order purhace done?  {inData.Order.Units.To()}pcs with {inData.Order.PricePerUnit.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    outData.Operation1 = "To Holding";
                    break;

                case UserEventType.OrderSell:
                    outData.Desc = $"Order sold now? {inData.Order.Units.To()}pcs with {inData.Order.PricePerUnit.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    outData.Operation1 = "Process Sale";
                    break;

                case UserEventType.AlarmOver:
                    if (inData.Alarm.DayHigh.HasValue && inData.Alarm.DayClosed < inData.Alarm.AlarmValue)
                        outData.Desc = $"Stock visited at {inData.Alarm.DayHigh.Value.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)} " +
                                       $"over alarm {inData.Alarm.AlarmValue.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}, " +
                                       $"but closed under it at {inData.Alarm.DayClosed.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    else
                        outData.Desc = $"Stock closed to {inData.Alarm.DayClosed.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)} " +
                                        $"over alarm level {inData.Alarm.AlarmValue.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    break;

                case UserEventType.AlarmUnder:
                    if (inData.Alarm.DayLow.HasValue && inData.Alarm.DayClosed > inData.Alarm.AlarmValue)
                        outData.Desc = $"Stock visited at {inData.Alarm.DayLow.Value.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)} " +
                                        $"under alarm {inData.Alarm.AlarmValue.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}, " +
                                        $"but closed over it at {inData.Alarm.DayClosed.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    else
                        outData.Desc = $"Stock closed to {inData.Alarm.DayClosed.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)} " +
                                        $"under alarm level {inData.Alarm.AlarmValue.To00()}{UiF.Curr(inData.StockMeta.marketCurrency)}";
                    break;

                case UserEventType.OwningAvrgPositive:
                    outData.Desc = $"{inData.PfName} {inData.StockMeta.GetSRef()} [{inData.StockMeta.name}] total holding is back to profit!";
                    outData.Operation1 = null;
                    break;

                case UserEventType.OwningAvrgNegative:
                    outData.Desc = $"{inData.PfName} {inData.StockMeta.GetSRef()} [{inData.StockMeta.name}] total holding is falling on loosing!";
                    outData.Operation1 = null;
                    break;

                case UserEventType.OwningOldestPositive:
                    outData.Desc = $"{inData.PfName} {inData.StockMeta.GetSRef()} [{inData.StockMeta.name}] oldest holding is back to profit, wanna sell?";
                    outData.Operation1 = null;
                    break;

                case UserEventType.OwningOldestNegative:
                    outData.Desc = $"{inData.PfName} {inData.StockMeta.GetSRef()} [{inData.StockMeta.name}] oldest holding is falling loose, wanna sell?";
                    outData.Operation1 = null;
                    break;

                case UserEventType.OrderTrailingSell:
                    outData.Desc = $"{inData.PfName} {inData.StockMeta.GetSRef()} [{inData.StockMeta.name}] trailing sell is triggered by drop% of {inData.Alarm.AlarmDropP.Value.To00()} from highs!";
                    outData.Operation1 = null;
                    break;

                case UserEventType.OrderTrailingBuy:
                    outData.Desc = $"{inData.PfName} {inData.StockMeta.GetSRef()} [{inData.StockMeta.name}] trailing buy is triggered by recover% of {inData.Alarm.AlarmRecoverP.Value.To00()} from lows!";
                    outData.Operation1 = null;
                    break;
            }

            // Get Icon per Event Mode
            outData.Icon = StatusIcons.Single(m => m.Item1 == outData.d.Status).Item2;

            _viewReport.Add(outData);
        }
    }

    private async Task ViewStockRequestedAsync(StockMeta stockMeta)
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

    protected async Task EvOperation1Async(ViewReportUserEventsData data)
    {
        switch (data.d.Type)
        {
            case UserEventType.OrderBuyExpired:     // RE-OPEN (really use as default and create new)
            case UserEventType.OrderSellExpired:
                {
                    SOrder defOrder = new()
                    {
                        LastDate = Pfs.Platform().GetCurrentLocalDate().FridayOnTwoWeeksAhead(),
                        PricePerUnit = data.d.Order.PricePerUnit,
                        SRef = data.d.StockMeta.GetSRef(),
                        Type = data.d.Order.Type,
                        Units = data.d.Order.Units,
                    };

                    var parameters = new DialogParameters() {
                        { "Market", data.d.StockMeta.marketId },
                        { "Symbol", data.d.StockMeta.symbol },
                        { "PfName", data.d.PfName },
                        { "Defaults", defOrder },
                        { "Edit",   false }
                    };

                    var dialog = await LaunchDialog.ShowAsync<DlgOrderEdit>("", parameters);
                    var result = await dialog.Result;

                    if (!result.Canceled)
                    {
                        // Getting OK means user 'Re-open' or actually created new matching Order.. so delete Event.. 
                        Pfs.Account().DeleteUserEvent(data.d.Id);

                        RefreshReport();
                        StateHasChanged();
                    }
                }
                break;

            case UserEventType.OrderBuy:      // TO HOLDING (uses Order info to Add Holding, and removed event/order)
                {
                    SHolding defaults = new()
                    {
                        PricePerUnit = data.d.Order.PricePerUnit,
                        Units = data.d.Order.Units,
                        PurhaceDate = data.d.Date,
                    };

                    var parameters = new DialogParameters {
                        { "Market", data.d.StockMeta.marketId },
                        { "Symbol", data.d.StockMeta.symbol },
                        { "PfName", data.d.PfName },
                        { "Defaults", defaults },
                        { "Edit", false },
                    };

                    var dialog = await LaunchDialog.ShowAsync<DlgHoldingsEdit>("", parameters);
                    var result = await dialog.Result;

                    if (!result.Canceled)
                    {
                        // Filled Buy order converted to Holding, so event is handled and can be removed...
                        Pfs.Account().DeleteUserEvent(data.d.Id);

                        // And Stock Order is also obsolete now that its filled, so also that can be removed
                        string cmd = $"Delete-Order PfName=[{data.d.PfName}] SRef=[{data.d.StockMeta.GetSRef()}] Price=[{data.d.Order.PricePerUnit}]";
                        Pfs.Stalker().DoAction(cmd);

                        RefreshReport();
                        StateHasChanged();
                    }
                }
                break;

            case UserEventType.OrderSell:
                {
                    var parameters = new DialogParameters {
                        { "Market", data.d.StockMeta.marketId },
                        { "Symbol", data.d.StockMeta.symbol },
                        { "PfName", data.d.PfName },
                        { "TargetHolding", null },
                        { "Defaults", new DlgSale.DefValues(Units: data.d.Order.Units, PricePerUnit: data.d.Order.PricePerUnit, Date: data.d.Date ) },
                    };

                    // Ala Sale Holding operation == finishing trade of buy holding, and now sell holding(s)
                    var dialog = await LaunchDialog.ShowAsync<DlgSale>("", parameters);
                    var result = await dialog.Result;

                    if (!result.Canceled)
                    {
                        // Filled Sell order converted to Sale, so event is handled and can be removed...
                        Pfs.Account().DeleteUserEvent(data.d.Id);

                        // And Stock Order is also obsolete now that its filled, so also that can be removed
                        string cmd = $"Delete-Order PfName=[{data.d.PfName}] SRef=[{data.d.StockMeta.GetSRef()}] Price=[{data.d.Order.PricePerUnit}]";
                        Pfs.Stalker().DoAction(cmd);

                        RefreshReport();
                        StateHasChanged();
                    }
                }
                break;
        }
    }

    protected void OnBtnSwapMode(ViewReportUserEventsData data)
    {
        UserEventStatus status = UserEventStatus.Read;

        // Unread -> Read -> Starred -> UnreadImp -> Read -> Starred -> UnreadImp -> etc
        switch (data.d.Status)
        {
            case UserEventStatus.Read: status = UserEventStatus.Starred; break;
            case UserEventStatus.Starred: status = UserEventStatus.UnreadImp; break;
            case UserEventStatus.Unread: status = UserEventStatus.Read; break;
            case UserEventStatus.UnreadImp: status = UserEventStatus.Read; break;
        }

        Pfs.Account().UpdateUserEventStatus(data.d.Id, status);
        RefreshReport();
        StateHasChanged();
    }

    protected void OnBtnDeleteEvent(ViewReportUserEventsData data)
    {
        Pfs.Account().DeleteUserEvent(data.d.Id);
        RefreshReport();
        StateHasChanged();
    }

    protected void OnBtnResetOrder(ViewReportUserEventsData data)
    {
        // Set-Order PfName SRef Price
        string cmd = $"Set-Order PfName=[{data.d.PfName}] SRef=[{data.d.StockMeta.GetSRef()}] Price=[{data.d.Order.PricePerUnit}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
        {
            Pfs.Account().DeleteUserEvent(data.d.Id);
            RefreshReport();
            StateHasChanged();
        }
    }

    protected void OnBtnDeleteAllEvents()
    {
        Pfs.Account().DeleteUserEvent(0);
        RefreshReport();
        StateHasChanged();

        if (_viewReport.Count() == 0)
            MudDialog.Close(DialogResult.Cancel());
    }

    protected class ViewReportUserEventsData
    {
        public RepDataUserEvents d;

        public string Icon;

        public string Desc;

        public string Operation1;
    }

    protected readonly static ImmutableArray<Tuple<UserEventStatus, string>> StatusIcons = ImmutableArray.Create(new Tuple<UserEventStatus, string>[]
    {
        new Tuple<UserEventStatus, string>( UserEventStatus.Unread,     Icons.Material.Filled.Email),
        new Tuple<UserEventStatus, string>( UserEventStatus.UnreadImp,  Icons.Material.Filled.MarkEmailUnread),
        new Tuple<UserEventStatus, string>( UserEventStatus.Read,       Icons.Material.Filled.Check),
        new Tuple<UserEventStatus, string>( UserEventStatus.Starred,    Icons.Material.Filled.Star),
    });
}
