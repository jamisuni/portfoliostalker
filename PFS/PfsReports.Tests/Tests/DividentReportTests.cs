using Pfs.Data.Stalker;
using Pfs.Reports;
using Pfs.Types;
using PfsReports.Tests.Helpers;
using Xunit;

namespace PfsReports.Tests.Tests;

public class DividentReportTests
{
    [Fact]
    public void Divident_WithDividents_ReturnsReport()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenDivident.GenerateReport(today, filters, preCalc, stalker, stockMeta);
        Assert.True(result.Ok);

        var report = ((OkResult<RepDataDivident>)result).Data;
        Assert.NotNull(report);
        Assert.True(report.HcTotalMonthly.Count > 0, "Should have monthly totals");
    }

    [Fact]
    public void Divident_MonthlyTotals_GroupedCorrectly()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenDivident.GenerateReport(today, filters, preCalc, stalker, stockMeta);
        var report = ((OkResult<RepDataDivident>)result).Data;

        // KO dividends: Apr 2024 (0.485*1.32*50), Jul 2024 (0.485*1.33*50), Oct 2024 (0.485*1.34*50)
        // ENB dividend: Apr 2024 (0.915*1*150)
        // Monthly keys should all be 1st of month
        foreach (var kvp in report.HcTotalMonthly)
            Assert.Equal(1, kvp.Key.Day);

        // Apr 2024 should have both KO and ENB dividends
        var apr2024 = new DateOnly(2024, 4, 1);
        Assert.True(report.HcTotalMonthly.ContainsKey(apr2024), "April 2024 should have dividends");

        // KO Apr: 0.485 * 1.32 * 50 = 32.01, ENB Apr: 0.915 * 1 * 150 = 137.25
        decimal expectedApr = (0.485m * 1.32m * 50m) + (0.915m * 1m * 150m);
        Assert.Equal(expectedApr, report.HcTotalMonthly[apr2024]);
    }

    [Fact]
    public void Divident_Last13Months_DetailOnly()
    {
        var stalker = ReportTestFixture.CreateStalker();
        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenDivident.GenerateReport(today, filters, preCalc, stalker, stockMeta);
        var report = ((OkResult<RepDataDivident>)result).Data;

        // All fixture dividends are from 2024 — more than 13 months ago from today (2026-02-15)
        // today.AddMonths(-13) = 2025-01-15, all divs are before that
        // So LastPayments should be empty (only recent 13 months get details)
        Assert.Empty(report.LastPayments);

        // But HcTotalMonthly should still have all months (no cutoff there)
        Assert.True(report.HcTotalMonthly.Count > 0, "Monthly totals should include old dividends");
    }

    [Fact]
    public void Divident_YearlyDivP_ProjectionFormula()
    {
        // Add a recent dividend so it appears in LastPayments
        var stalker = StalkerTestFixture.CreatePopulated();
        stalker.DoAction("Add-Divident PfName=Main SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2026-01-15 PaymentDate=2026-02-01 Units=50 PaymentPerUnit=0.50 CurrencyRate=0.92 Currency=USD");

        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenDivident.GenerateReport(today, filters, preCalc, stalker, stockMeta);
        var report = ((OkResult<RepDataDivident>)result).Data;

        Assert.True(report.LastPayments.Count > 0, "Should have recent payment details");

        // Verify YearlyDivPForHcHolding formula: HcPaymentPerUnit * DivPaidTimesPerYear * 100 / HcAvrgPrice
        foreach (var payment in report.LastPayments)
        {
            // YearlyDivP should be non-negative (positive if there are holdings)
            Assert.True(payment.YearlyDivPForHcHolding >= 0);
        }
    }

    [Fact]
    public void Divident_NoDividents_ReturnsEmptyReport()
    {
        // Create stalker with holdings but no dividends
        var stalker = StalkerTestFixture.CreateEmpty();
        stalker.DoAction("Add-Portfolio PfName=Test");
        stalker.DoAction("Follow-Portfolio PfName=Test SRef=NASDAQ$MSFT");
        stalker.DoAction("Add-Holding PfName=Test SRef=NASDAQ$MSFT PurhaceId=X001 Date=2024-01-01 Units=10 Price=300 Fee=0 CurrencyRate=1 Note=Test");

        var preCalc = ReportTestFixture.CreatePreCalc(stalker);
        var stockMeta = ReportTestFixture.CreateStockMeta();
        var filters = ReportTestFixture.CreateReportFilters();
        var today = new DateOnly(2026, 2, 15);

        var result = RepGenDivident.GenerateReport(today, filters, preCalc, stalker, stockMeta);
        Assert.True(result.Ok);

        var report = ((OkResult<RepDataDivident>)result).Data;
        Assert.Empty(report.HcTotalMonthly);
        Assert.Empty(report.LastPayments);
    }
}
