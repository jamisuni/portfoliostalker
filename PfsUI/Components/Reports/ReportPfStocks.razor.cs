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
using static PfsUI.Components.OverviewStocks;

namespace PfsUI.Components;

public partial class ReportPfStocks
{
    /* PLAN: This report is for KEEPING EYE OF COMPANIES WANTING TO PURHACE, specially so on mainline of report:
     *  => Mainline doesnt show holdings except profit/gain
     *  => Mainline doesnt show dividents
     *  => !!!DECISION!!! FOCUS of this report is on Alarm's and keeping eye company as investment target
     *  
     * Think! Could add here extraColumns also but then overview shows highlights so hmm.. definedly not rush
     * Remember! Investment report is there for overview of gains, and StockMgmt shows way more details per currencies
     */

    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService Dialog { get; set; }
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    [Parameter] public string PfName { get; set; } // Atm this report is supported only under Portfolio so this must be set

    protected CurrencyId _homeCurrency;
    protected bool _viewCompanyNameColumn = true;

    protected bool _showGrowthColumn;
    protected bool _showAlarmOverColumn;
    protected List<ViewReportEntry> _report = null;

    protected override void OnParametersSet()
    {
        _homeCurrency = Pfs.Config().HomeCurrency;
        _viewCompanyNameColumn = Pfs.Account().GetAppCfg(AppCfgId.HideCompanyName) == 0;

        ReloadReportData();
    }

    public void ReloadReport()
    {
        ReloadReportData();
        StateHasChanged();
    }

    protected void ReloadReportData()
    {
        _showGrowthColumn = false;
        _showAlarmOverColumn = false;
        _report = new();

        List<RepDataPfStocks> reportData = Pfs.Report().GetPfStocks(PfName);

        if (reportData != null)
        {
            foreach (RepDataPfStocks inData in reportData)
            {
                ViewReportEntry outData = new()
                {
                    d = inData,
                    MC = UiF.Curr(inData.StockMeta.marketCurrency),
                    OrderDetails = null,
                    AllowRemove = inData.RRTotalHold == null && inData.HasTrades == false,
                    SymbolToolTip = string.Empty,
                };

                if (outData.d.RRAlarm != null && outData.d.RRAlarm.OverP != null)
                    _showAlarmOverColumn = true;

                if (inData.RRTotalHold != null)
                    // Profit column is shown only if group has something w it..
                    _showGrowthColumn = true;

                if (_viewCompanyNameColumn == false)
                    outData.SymbolToolTip += $"{inData.StockMeta.name}";

                if (string.IsNullOrEmpty(inData.NoteHeader) == false)
                {
                    if (_viewCompanyNameColumn == false)
                        outData.SymbolToolTip += ": ";
                    outData.SymbolToolTip += $"{inData.NoteHeader}";
                }

                if ( inData.Order != null )
                {   // Only one order is bring per Stock... triggered or closes to trigger if many.. and shown report under company name
                    if ( inData.Order.Type == SOrder.OrderType.Buy ) 
                    {
                        if (inData.Order.FillDate.HasValue == false)
                            outData.OrderDetails = $"(Buy Order for {inData.Order.Units}pcs at {inData.Order.PricePerUnit.ToString("0.00")}{outData.MC})";
                        else
                            outData.OrderDetails = $"(Buy {inData.Order.Units}pcs at {inData.Order.PricePerUnit.ToString("0.00")}{outData.MC} - PURHACED? {inData.Order.FillDate.Value.ToString("yyyy-MM-dd")})";
                    }
                    else if (inData.Order.Type == SOrder.OrderType.Sell)
                    {
                        if (inData.Order.FillDate.HasValue == false)
                            outData.OrderDetails = $"(Sell Order for {inData.Order.Units}pcs at {inData.Order.PricePerUnit.ToString("0.00")}{outData.MC})";
                        else
                            outData.OrderDetails = $"(Sell {inData.Order.Units}pcs at {inData.Order.PricePerUnit.ToString("0.00")}{outData.MC} - SOLD NOW? {inData.Order.FillDate.Value.ToString("yyyy-MM-dd")})";
                    }
                }
                _report.Add(outData);
            }
        }
        return;
    }

    private async Task ViewStockRequestedAsync(string sRef)
    {
        var stock = StockMeta.ParseSRef(sRef);

        var parameters = new DialogParameters
        {
            { "Market", stock.marketId },
            { "Symbol", stock.symbol }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };

        var dialog = Dialog.Show<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task DoAddOrderAsync(ViewReportEntry entry)
    {
        var parameters = new DialogParameters() {
            { "Market", entry.d.StockMeta.marketId },
            { "Symbol", entry.d.StockMeta.symbol },
            { "PfName", PfName },
            { "Defaults", null },
            { "Edit", false }
        };

        var dialog = Dialog.Show<DlgOrderEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task DoAddHoldinAsync(ViewReportEntry entry)
    {
        var parameters = new DialogParameters {
            { "Market", entry.d.StockMeta.marketId },
            { "Symbol", entry.d.StockMeta.symbol },
            { "PfName", PfName },
            { "Defaults", null },
            { "Edit", false }
        };

        var dialog = Dialog.Show<DlgHoldingsEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task DoSaleHoldingAsync(ViewReportEntry entry)
    {
        var parameters = new DialogParameters {
            { "Market", entry.d.StockMeta.marketId },
            { "Symbol", entry.d.StockMeta.symbol },
            { "PfName", PfName },
            { "TargetHolding", null },
            { "Defaults", new DlgSale.DefValues(MaxUnits: entry.d.RRTotalHold.Units) },
        };

        // Ala Sale Holding operation == finishing trade of buy holding, and now sell holding(s)
        var dialog = Dialog.Show<DlgSale>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task DoAddDividentAsync(ViewReportEntry entry)
    {
        var parameters = new DialogParameters {
            { "Market", entry.d.StockMeta.marketId },
            { "Symbol", entry.d.StockMeta.symbol },
            { "PfName", PfName },
            { "Holding", null },
        };

        var dialog = Dialog.Show<DlgDividentAdd>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected async Task DoRemoveStockTrackingAsync(ViewReportEntry entry)
    {
        bool? result = await Dialog.ShowMessageBox("Untrack", "Remove from this PF stock list?", yesText: "Ok", cancelText: "Cancel");

        if (result.HasValue == false || result.Value == false)
            return;

        // Unfollow-Portfolio PfName SRef
        string cmd = $"Unfollow-Portfolio PfName=[{PfName}] SRef=[{entry.d.StockMeta.marketId}${entry.d.StockMeta.symbol}]";
        Result stalkerResp = Pfs.Stalker().DoAction(cmd);

        if (stalkerResp.Ok)
            ReloadReport();
        else
            await Dialog.ShowMessageBox("Failed!", (stalkerResp as FailResult).Message, cancelText: "Dang");
    }
}

public class ViewReportEntry
{
    public RepDataPfStocks d { get; set; }

    public string MC {  get; set; }

    public bool AllowRemove { get; set; }

    public string OrderDetails { get; set; }

    public string SymbolToolTip { get; set; }
}
