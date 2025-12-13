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
using static Pfs.ExtProviders.ExtCurrencyApi;

namespace PfsUI.Components;

public partial class ReportWeight
{
    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    private List<ViewReportWeightData> _viewReport = null;

    protected bool _viewCompanyNameColumn = true;
    protected CurrencyId _homeCurrency;
    protected string _HC = string.Empty;

    protected string _headerTextCompany = string.Empty;
    protected string _headerTextCurrent = string.Empty;

    protected bool _showTargetWeightTab = true;

    protected int _sectorId = -1;
    protected string[] _weights = Array.Empty<string>();

    protected override void OnParametersSet()
    {
        _viewReport = null;
        _homeCurrency = Pfs.Config().HomeCurrency;
        _HC = UiF.Curr(_homeCurrency);
        _viewCompanyNameColumn = Pfs.Account().GetAppCfg(AppCfgId.HideCompanyName) == 0;

        string[] sectors = Pfs.Stalker().GetSectorNames();

        if (sectors.Contains("Weight") == false)
            _showTargetWeightTab = false;
        else
        {
            _sectorId = sectors.IndexOf("Weight");
            _weights = Pfs.Stalker().GetSectorFieldNames(_sectorId).ToArray();
        }

        ReloadReport();
    }

    public void ByOwner_ReloadReport()
    {
        ReloadReport();
        StateHasChanged();
    }

    protected void ReloadReport()
    {
        (RepDataWeightHeader header, List <RepDataWeight> reportData) = Pfs.Report().GetWeightData();

        _viewReport = new();

        if (reportData == null || header == null)
            return; // "Nothing found"

        foreach (RepDataWeight inData in reportData)
        {
            ViewReportWeightData outData = new()
            {
                d = inData,
                MC = UiF.Curr(inData.RCEod.MarketCurrency),
                SymbolToolTip = string.Empty,
                HcTakenAgainstVal = inData.HcTakenAgainstVal,
                HcTakenAgainstInv = inData.HcTakenAgainstInv,
            };

            if (_viewCompanyNameColumn == false)
                outData.SymbolToolTip += $"{inData.StockMeta.name}";

            if (string.IsNullOrEmpty(inData.NoteHeader) == false)
            {
                if (_viewCompanyNameColumn == false)
                    outData.SymbolToolTip += ": ";
                outData.SymbolToolTip += $"{inData.NoteHeader}";
            }

            outData.AvrgTime = Local_FormatAvrgTime((int)inData.AvrgTimeAsMonths);

            foreach (RepDataWeightHoldingSub holding in inData.SubHoldings)
            {
                ViewSubData subData = new()
                {
                    Portfolio = holding.RCHolding.PfName,
                    PurhaceDate = holding.RCHolding.SH.PurhaceDate,
                    SoldDate = null,
                    OriginalUnits = holding.RCHolding.SH.OriginalUnits,
                    Units = holding.RCHolding.SH.Units,
                    McBuyPrice = holding.RCHolding.SH.McPriceWithFeePerUnit,
                    McSoldPrice = null,
                    HcInvested = holding.RCTotalHold.HcInvested,
                    HcGrowth = holding.RCTotalHold.HcGrowthAmount,
                    HcDividents = holding.RRHoldingsTotalDiv != null ? holding.RRHoldingsTotalDiv.ViewHcDiv : 0,
                };
                outData.subs.Add(subData);
            }

            foreach (RepDataWeightTradeSub trade in inData.SubTrades)
            {
                ViewSubData subData = new()
                {
                    Portfolio = trade.RCTrade.PfName,
                    PurhaceDate = trade.RCTrade.ST.PurhaceDate,
                    SoldDate = trade.RCTrade.ST.Sold?.SaleDate,
                    OriginalUnits = trade.RCTrade.ST.OriginalUnits,
                    Units = trade.RCTrade.ST.Units,
                    McBuyPrice = trade.RCTrade.ST.McPriceWithFeePerUnit,
                    McSoldPrice = trade.RCTrade.ST.Sold?.McPriceWithFeePerUnit,
                    HcInvested = trade.RCTrade.ST.HcInvested,
                    HcGrowth = trade.RCTrade.ST.HcSoldProfit,
                    HcDividents = trade.HcTradeDividents
                };
                outData.subs.Add(subData);
            }

            outData.subs = outData.subs.OrderByDescending(s => s.PurhaceDate).ToList();

            _viewReport.Add(outData);
        }

        _headerTextCompany = $"Company (total {_viewReport.Count()})";
        _headerTextCurrent = $"Curr {header.TotalCurrentP.ToP()}";
        return;

        string Local_FormatAvrgTime(int months)
        {
            if (months < 12)
                return months.ToString() + "m";

            if (months < 36)
                return (months / 12).ToString() + "y" + (months % 12 != 0 ? (months % 12).ToString() + "m" : "");

            return (months / 12).ToString() + "y";
        }
    }

    private void OnRowClicked(TableRowClickEventArgs<ViewReportWeightData> data)
    {
        if (data == null || data.Item == null || data.Item.subs.Count == 0) return;

        data.Item.ShowDetails = !data.Item.ShowDetails;
    }

    private void OnTargetWeightChanged(ViewReportWeightData row, string newValue)
    {
        row.d.TargetP = newValue;

        string cmd = $"Follow-Sector SRef=[{row.d.StockMeta.GetSRef()}] SectorId=[{_sectorId}] FieldId=[{_weights.IndexOf(newValue)}]";

        Result stalkerResp = Pfs.Stalker().DoAction(cmd);
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

        var dialog = await LaunchDialog.ShowAsync<StockMgmtDlg>("", parameters, maxWidth);
        var result = await dialog.Result;

        if (!result.Canceled)
            ReloadReport();
    }

    protected class ViewReportWeightData
    {
        public RepDataWeight d;

        public string AvrgTime;

        public string MC;

        public bool ShowDetails;

        public string SymbolToolTip;

        public decimal HcTakenAgainstInv;
        public decimal HcTakenAgainstVal;

        public List<ViewSubData> subs = new();
    }

    protected class ViewSubData
    {
        public string Portfolio;

        public DateOnly PurhaceDate;

        public DateOnly? SoldDate;

        public decimal OriginalUnits;

        public decimal Units;

        public decimal McBuyPrice;

        public decimal? McSoldPrice;

        public decimal HcInvested;

        public decimal HcGrowth;

        public decimal HcDividents;
    }
}
