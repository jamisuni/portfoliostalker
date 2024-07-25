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

public partial class ReportDivident
{
    [Parameter] public string PfName { get; set; } = string.Empty; // If given then limited to specific PF
    [Inject] PfsClientAccess Pfs { get; set; }

    private List<ViewPayment> _paymentsAll = null;
    private List<ViewPayment> _paymentsView = null;

    private List<ViewYear> _yearly = null;

    private string _missingDataError = string.Empty;    // Set if report loading failed, contains FailReason from report creation

    private List<ChartSeries> _monthlyChart = new();
    public string[] _monthlyChartXLab = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    protected string _HC;

    protected override void OnParametersSet()
    {
        _HC = UiF.Curr(Pfs.Config().HomeCurrency);
        
        ReloadReport();
    }

    public void ByOwner_ReloadReport()
    {
        ReloadReport();
        StateHasChanged();
    }

    protected void ReloadReport()
    {
        DateOnly utcNow = DateOnly.FromDateTime(Pfs.Platform().GetCurrentUtcTime());

        Result<RepDataDivident> reportResp = Pfs.Report().GetDivident();

        if (!reportResp.Ok)
        {
            _missingDataError = (reportResp as FailResult<RepDataDivident>).Message;
            return;
        }

        if ( reportResp.Data.LastPayments.Count == 0 )
        {
            _missingDataError = "No divident records found!";
            return;
        }

        RepDataDivident report = reportResp.Data;

        _paymentsView = _paymentsAll = new();
        foreach (RepDataDivident.Payment payment in report.LastPayments )
        {
            ViewPayment entry = new()
            {
                d = payment,
            };

            _paymentsAll.Add(entry);
        }

        // Setup Quarterly Grid  Dictionary<DateOnly, decimal> HcTotalMonthly

        Dictionary<int, ViewYear> yearly = new();

        foreach ( KeyValuePair<DateOnly, decimal> kv in report.HcTotalMonthly )
        {
            if (yearly.ContainsKey(kv.Key.Year) == false)
            {
                yearly.Add(kv.Key.Year, new() { Year = kv.Key.Year } );
            }

            ViewYear entry = yearly[kv.Key.Year];

            entry.Total += kv.Value;
            entry.Quarterly[(kv.Key.Month-1) / 3] += kv.Value;
        }

        _yearly = yearly.Values.OrderByDescending(e => e.Year).ToList();

        // Setup Monthly Chart

        DateOnly checkMonth = new DateOnly(utcNow.AddYears(-4).Year, 1, 1); // UI limits viewed chart to this/last years only...

        List<double> monthly = new();

        for (; checkMonth < utcNow; checkMonth = checkMonth.AddMonths(+1) )
        {
            if (report.HcTotalMonthly.ContainsKey(checkMonth) == true)
                monthly.Add((double)(decimal.Round(report.HcTotalMonthly[checkMonth], 1)));
            else
                monthly.Add(0);
        }

        _monthlyChart = new()
        {
            new ChartSeries() { Name = $"{utcNow.AddYears(-4).Year}",
                                Data = monthly.GetRange(0, 12).ToArray() },

            new ChartSeries() { Name = $"{utcNow.AddYears(-3).Year}",
                                Data = monthly.GetRange(12, 12).ToArray() },

            new ChartSeries() { Name = $"{utcNow.AddYears(-2).Year}",
                                Data = monthly.GetRange(24, 12).ToArray() },

            new ChartSeries() { Name = $"{utcNow.AddYears(-1).Year}",
                                Data = monthly.GetRange(36, 12).ToArray() },

            new ChartSeries() { Name = $"{utcNow.AddYears(0).Year}",
                                Data = monthly.GetRange(48, monthly.Count() - 48).ToArray() }
        };
    }

    private void OnDivyRowClicked(TableRowClickEventArgs<ViewPayment> data)
    {
        if (_paymentsAll.Count == _paymentsView.Count)
        {   // This case click on row means that we want to focus just one of stocks
            _paymentsView = _paymentsAll.Where(p => p.d.StockMeta.GetSRef() == data.Item.d.StockMeta.GetSRef()).ToList();
        }
        else
            _paymentsView = _paymentsAll;

        StateHasChanged();
    }

    public class ViewPayment
    {
        public RepDataDivident.Payment d;

//        public string EstP;
    }

    public class ViewYear
    {
        public int Year;

        public decimal[] Quarterly;

        public decimal Total;

        public ViewYear()
        {
            Year = 0;
            Quarterly = new decimal[4] { 0,0,0,0 };
            Total = 0;
        }
    }
}
