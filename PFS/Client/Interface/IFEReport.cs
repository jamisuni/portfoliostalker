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

using Pfs.Types;

namespace Pfs.Client;

public interface IFEReport
{
    string[] ListReportFilters();

    ReportFilters GetReportFilters(string customName);

    void UseReportFilters(ReportFilters reportFilters);

    void StoreReportFilters(ReportFilters reportFilters);

    Result<List<OverviewGroupsData>> GetOverviewGroups();

    List<OverviewStocksData> GetOverviewStocks();

    (RepDataInvestedHeader header, List<RepDataInvested> stocks) GetInvestedData();

    (RepDataWeightHeader header, List<RepDataWeight> stocks) GetWeightData();

    Result<RepDataDivident> GetDivident();

    List<RepDataTracking> GetTracking();

    List<RepDataPfStocks> GetPfStocks(string pfName);

    List<RepDataPfSales> GetPfSales(string pfName);

    List<RepDataExpHoldings> GetExportHoldingsData();

    List<RepDataExpSales> GetExportSalesData();

    List<RepDataExpDividents> GetExportDividentsData();

    Result<List<RepDataStMgHoldings>> GetStMgHoldings(string sRef);

    Result<List<RepDataStMgHistory>> GetStMgHistory(string sRef);
}
