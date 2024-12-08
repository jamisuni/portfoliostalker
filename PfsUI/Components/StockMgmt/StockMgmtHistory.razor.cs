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

public partial class StockMgmtHistory
{
    private EventHandler evChanged;
    public event EventHandler EvChanged
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

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    protected List<ViewEntry> _viewReport;

    protected string _errMsg = "";

    protected bool _viewDividentColumn;
    protected CurrencyId _homeCurrency;

    protected override void OnParametersSet()
    {
        RefreshReport();
    }

    protected void RefreshReport()
    {
        _viewReport = new();
        _homeCurrency = Pfs.Config().HomeCurrency;

        Result<List<RepDataStMgHistory>> reportData = Pfs.Report().GetStMgHistory($"{Market}${Symbol}");

        if ( reportData.Fail )
        {
            _errMsg = (reportData as FailResult<List<RepDataStMgHistory>>).Message;
            return;
        }

        List<string> batchId = new(); // giving unique 1.. for each original buy batch to help visual mapping

        foreach (RepDataStMgHistory inData in reportData.Data)
        {
            ViewEntry outData = new()
            {
                d = inData,
                MC = UiF.Curr(inData.RCEod?.MarketCurrency),
                HC = UiF.Curr(_homeCurrency)
            };

            if (outData.d.Own != null)
                Local_FormatOwn(outData);

            else if ( outData.d.Buy != null )
                Local_FormatBuy(outData);

            else if ( outData.d.Sold != null )
                Local_FormatSold(outData);

            else if ( outData.d.Total != null )
                Local_FormatTotal(outData);

            else if ( outData.d.History != null )
                Local_FormatHistory(outData);

            _viewReport.Add(outData);
        }
        return;

        void Local_FormatOwn(ViewEntry entry)
        {
            int batch = Local_GetBatch(entry.d.Own.Holding.PurhaceId);

            entry.Hdr = $"{batch.ToString("00")}: {entry.d.Date.ToString("yyyy-MMM-dd")} ";

            if (entry.d.Own.Holding.Units < entry.d.Own.Holding.OriginalUnits)
                entry.Hdr += $"OWNS {entry.d.Own.Holding.Units}/{entry.d.Own.Holding.OriginalUnits}pcs ";
            else
                entry.Hdr += $"OWNS {entry.d.Own.Holding.Units}pcs ";

            entry.Hdr += $"inv {entry.d.Own.HcInv.To()}{entry.HC} ";

            entry.Hdr += $"w {entry.d.Own.Holding.McPriceWithFeePerUnit.To00()}{entry.MC} ";

            if (entry.d.Own.HcGrowth > entry.d.Own.HcInv)
                entry.Hdr += $" growth +{(entry.d.Own.HcGrowth / entry.d.Own.HcInv * 100).ToP()} +{entry.d.Own.HcGrowth.To()}{entry.HC} ";
            else
                entry.Hdr += $" growth {(entry.d.Own.HcGrowth / entry.d.Own.HcInv * 100).ToP()} {entry.d.Own.HcGrowth.To()}{entry.HC} ";

            if (entry.d.Own.Holding.AnyDividents())
            {
                entry.Divident = RRHoldingDivident.Create(entry.d.Own.Holding);

                entry.Hdr += $"divs {(entry.d.TotalDivident.HcDiv / entry.d.Own.HcInv * 100).ToP()} {entry.d.TotalDivident.HcDiv.To()}{entry.HC} ";
            }
        }

        void Local_FormatBuy(ViewEntry entry)   // No dividents shown here separately as pieces may have sold different times
        {
            int batch = Local_GetBatch(entry.d.Buy.Holding.PurhaceId);

            entry.Hdr = $"{batch.ToString("00")}: {entry.d.Date.ToString("yyyy-MMM-dd")} ";

            decimal units = entry.d.Buy.Holding.OriginalUnits;

            // Think! Or is it sum of sold positions?

            if (entry.d.Buy.Holding.Units < entry.d.Buy.Holding.OriginalUnits)
            {
                units -= entry.d.Buy.Holding.Units;
                entry.Hdr += $"buy {units}/{entry.d.Buy.Holding.OriginalUnits}pcs ";
            }
            else
                entry.Hdr += $"buy {units}pcs ";

            decimal hcInv = entry.d.Buy.Holding.HcPriceWithFeePerUnit * units;

            entry.Hdr += $"inv {hcInv.To()}{entry.HC} ";

            entry.Hdr += $"w {entry.d.Buy.Holding.McPriceWithFeePerUnit.To00()}{entry.MC} ";

            foreach (SHolding sale in entry.d.Buy.Sales)
            {
                string saleMsg = $"{sale.Sold.SaleDate.ToString("yyyy-MMM-dd")} sold ";
                saleMsg += $"{sale.Units.To()}pcs ";
                saleMsg += $"at {sale.Sold.McPriceWithFeePerUnit.To00()}{entry.MC} with profit .... ";

                entry.Extras.Add(saleMsg) ;
            }
        }

        void Local_FormatSold(ViewEntry entry)
        {
            int batch = Local_GetBatch(entry.d.Sold.Holding.PurhaceId);

            entry.Hdr = $"{batch.ToString("00")}: {entry.d.Date.ToString("yyyy-MMM-dd")} ";

            entry.Hdr += $"sold {entry.d.Sold.Holding.Units}pcs ";

            entry.Hdr += $"inv {entry.d.Sold.HcInv.To()}{entry.HC} ";

            entry.Hdr += $"profit {(entry.d.Sold.HcSold - entry.d.Sold.HcInv).To()}{entry.HC} ";

            entry.Hdr += $"buy {entry.d.Sold.Holding.McPriceWithFeePerUnit.To00()}{entry.MC} ";

            entry.Hdr += $"sold {entry.d.Sold.Holding.Sold.McPriceWithFeePerUnit.To00()}{entry.MC} ";

            if (entry.d.Sold.Holding.AnyDividents())
            {
                entry.Divident = RRHoldingDivident.Create(entry.d.Sold.Holding);

                entry.Hdr += $"divs {(entry.d.TotalDivident.HcDiv / entry.d.Sold.HcInv * 100).ToP()} {entry.d.TotalDivident.HcDiv.To()}{entry.HC} ";
            }
        }

        void Local_FormatTotal(ViewEntry entry)
        {
            entry.Hdr = $"Total: Sale Profit={entry.d.Total.HcProfit.To()}{entry.HC} ";

            if (entry.d.Total.HcDiv > 0)
                entry.Hdr += $"Divs={entry.d.Total.HcDiv.To()}{entry.HC} ";

            if (entry.d.Total.HcInv > 0)
            {
                entry.Hdr += $"Owning {entry.d.Total.HcInv.To()}{entry.HC} w Growth={entry.d.Total.HcGrowth.To()}{entry.HC} ";
            }
        }

        void Local_FormatHistory(ViewEntry entry)
        {
            switch ( entry.d.History.Type )
            {
                case StockMetaHistType.AddNew:
                    // This needs to be first, and really date is nothing usefull for this
                    entry.d.Date = DateOnly.MinValue;
                    entry.Hdr = $"{entry.d.History.UpdSRef} [{entry.d.History.Note}]";
                    break;

                case StockMetaHistType.UpdName:
                    entry.Hdr = $"{entry.d.History.UpdSRef} [{entry.d.History.Note}]";
                    break;

                case StockMetaHistType.UpdISIN:
                    entry.Hdr = $"{entry.d.History.UpdSRef} [{entry.d.History.Note}]";
                    break;

                case StockMetaHistType.UpdSRef:
                    entry.Hdr = $"{entry.d.History.OldSRef}=>{entry.d.History.UpdSRef} [{entry.d.History.Note}]";
                    break;

                case StockMetaHistType.UserMap:
                    entry.Hdr = $"UserMapped {StockMeta.ParseSRef(entry.d.History.OldSRef).symbol} to be handled as {entry.d.History.UpdSRef} [{entry.d.History.Note}]";
                    break;

                case StockMetaHistType.Close:
                    entry.Hdr = $"CLOSED {entry.d.History.OldSRef}=>{entry.d.History.UpdSRef} [{entry.d.History.Note}]";
                    break;

                default:
                    entry.Hdr = $"StockMgmt-History.razor.cs missing implementation for {entry.d.History.Type.ToString()}";
                    break;
            }
        }

        int Local_GetBatch(string id)
        {
            int batch = batchId.IndexOf(id);

            if (batch >= 0)
                return batch + 1;

            batchId.Add(id);
            return batchId.IndexOf(id) + 1;
        }
    }

    protected class ViewEntry
    {
        public RepDataStMgHistory d;

        public string Hdr = string.Empty;

        public string MC;

        public string HC;

        public List<string> Extras = new();

        public List<RRHoldingDivident> Divident = null;
    }
}
