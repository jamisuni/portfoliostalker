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

using Pfs.Config;
using Pfs.Reports;
using Pfs.Data;
using Pfs.Types;

namespace Pfs.Client;

public class FEReport : IFEReport
{
    protected IPfsPlatform _pfsPlatform;
    protected IPfsStatus _pfsStatus;
    protected ClientStalker _clientStalker;
    protected IMarketMeta _marketMetaProv;
    protected IStockMeta _stockMetaProv;
    protected IEodLatest _latestEodProv;
    protected ILatestRates _latestRatesProv;
    protected StoreReportFilters _storeReportFilters;
    protected IPfsFetchConfig _fetchConfig;
    protected IExtraColumns _extraColumns;
    protected StoreStockMetaHist _storeStockMetaHist;
    protected IStockNotes _stockNotes;

    public FEReport(ClientStalker clientStalker, IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, IMarketMeta marketMetaProv,
                    IStockMeta stockMetaProv, IEodLatest latestEodProv, ILatestRates latestRatesProv, StoreReportFilters storeReportFilters, IPfsFetchConfig fetchConfig,
                    IExtraColumns extraColumns, StoreStockMetaHist storeStockMetaHist, IStockNotes stockNotes)
    {
        _clientStalker = clientStalker;
        _pfsPlatform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _marketMetaProv = marketMetaProv;
        _stockMetaProv = stockMetaProv;
        _latestEodProv = latestEodProv;
        _latestRatesProv = latestRatesProv;
        _storeReportFilters = storeReportFilters;
        _fetchConfig = fetchConfig;
        _extraColumns = extraColumns;
        _storeStockMetaHist = storeStockMetaHist;
        _stockNotes = stockNotes;
    }

    protected ReportFilters _currentReportFilters = null;

    public string[] ListReportFilters()
    {
        return _storeReportFilters.List();
    }

    public ReportFilters GetReportFilters(string customName)
    {
        if (customName == ReportFilters.DefaultTag || customName == ReportFilters.CurrentTag && _currentReportFilters == null)
            _currentReportFilters = ReportFilters.Default.DeepCopy();

        else if (customName != ReportFilters.CurrentTag)
            _currentReportFilters = _storeReportFilters.Get(customName).DeepCopy();

        return _currentReportFilters.DeepCopy();
    }

    public void UseReportFilters(ReportFilters reportFilters)
    {
        _currentReportFilters = reportFilters.DeepCopy();
    }

    public void StoreReportFilters(ReportFilters reportFilters)
    {
        _storeReportFilters.Store(reportFilters);
    }

    protected IReportPreCalc GetPreCalcData(ReportId reportId, ReportFilters filter, string pfName = "")
    {
        // Most reports use same pre-calculated data and continue from that with report specific calculations (filter effects to Pfs & Sectors to be included)
        return new ReportPreCalc(pfName, filter, _pfsPlatform, _latestEodProv, _stockMetaProv, _marketMetaProv, _latestRatesProv, _clientStalker);
    }

    public (RepDataInvestedHeader header, List<RepDataInvested> stocks) GetInvestedData()
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.Invested, filter);

        return RepGenInvested.GenerateReport(filter, preCalc, _stockMetaProv, _clientStalker, _stockNotes);
    }

    public (RepDataWeightHeader header, List<RepDataWeight> stocks) GetWeightData()
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.Weight, filter);

        return RepGenWeight.GenerateReport(_pfsPlatform.GetCurrentLocalDate(), filter, preCalc, _stockMetaProv, _clientStalker, _stockNotes, _pfsStatus);
    }

    public Result<RepDataDivident> GetDivident()
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.Divident, filter);

        return RepGenDivident.GenerateReport(_pfsPlatform.GetCurrentLocalDate(), filter, preCalc, _clientStalker, _stockMetaProv);
    }

    // Default Filter -reports

    public Result<List<OverviewGroupsData>> GetOverviewGroups()
    {
        ReportFilters filter = ReportFilters.Default;
        IReportPreCalc preCalc = GetPreCalcData(ReportId.Overview, filter);

        return ReportOverviewGroups.GenerateReport(filter, preCalc, _pfsStatus, _clientStalker, _latestRatesProv);
    }

    public List<OverviewStocksData> GetOverviewStocks()
    {
        ReportFilters filter = ReportFilters.Default;
        IReportPreCalc preCalc = GetPreCalcData(ReportId.Overview, filter);

        return ReportOverviewStocks.GenerateReport(filter, preCalc, _pfsStatus, _clientStalker, _stockMetaProv, _marketMetaProv, _extraColumns, _stockNotes);
    }

    // Portfolio Specific -reports

    public List<RepDataPfStocks> GetPfStocks(string pfName)
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        filter.Set(FilterId.PfName, pfName);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.PfStocks, filter);

        return RepGenPfStocks.GenerateReport(filter, preCalc, _clientStalker, _stockNotes);
    }

    public List<RepDataPfSales> GetPfSales(string pfName)
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        filter.Set(FilterId.PfName, pfName);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.PfSales, filter);

        return RepGenPfSales.GenerateReport(filter, preCalc, _stockMetaProv, _clientStalker, _stockNotes);
    }

    // Exports

    public List<RepDataExpHoldings>  GetExportHoldingsData()
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.ExpHoldings, filter);

        return RepGenExpHoldings.GenerateReport(_pfsPlatform.GetCurrentLocalDate(), filter, preCalc, _stockMetaProv, _clientStalker);
    }

    public List<RepDataExpSales> GetExportSalesData()
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.ExpSales, filter);

        return RepGenExpSales.GenerateReport(filter, preCalc, _stockMetaProv, _clientStalker);
    }

    public List<RepDataExpDividents> GetExportDividentsData()
    {
        ReportFilters filter = GetReportFilters(ReportFilters.CurrentTag);
        IReportPreCalc preCalc = GetPreCalcData(ReportId.ExpSales, filter);

        return RepGenExpDividents.GenerateReport(filter, preCalc, _stockMetaProv, _clientStalker);
    }

    // Non Collection base -reports

    public List<RepDataTracking> GetTracking()
    {
        return RepGenTracking.GenerateReport(_clientStalker, _marketMetaProv, _stockMetaProv, _latestEodProv, _fetchConfig, _stockNotes);
    }

    public Result<List<RepDataStMgHoldings>> GetStMgHoldings(string sRef)
    {
        return RepGenStMgHoldings.GenerateReport(sRef, _clientStalker, _stockMetaProv, _latestEodProv, _latestRatesProv);
    }

    public Result<List<RepDataStMgHistory>> GetStMgHistory(string sRef)
    {
        return RepGenStMgHistory.GenerateReport(sRef, _clientStalker, _stockMetaProv, _latestEodProv, _latestRatesProv, _storeStockMetaHist);
    }
}
