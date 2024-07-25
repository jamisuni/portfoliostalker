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

using Pfs.Shared.Stalker;
using Pfs.Types;

namespace Pfs.Reports;

public class RepGenExpDividents
{
    static public List<RepDataExpDividents> GenerateReport(
                                IReportFilters reportParams, IReportPreCalc collector, IStockMeta stockMetaProv, StalkerData stalkerData)
    {
        List<RepDataExpDividents> ret = new();

        IEnumerable<RCStock> reportStocks = collector.GetStocks(reportParams, stalkerData);

        if (reportStocks.Count() == 0)
            return null;

        foreach (RCStock stock in reportStocks)
        {
            if (stock.Dividents == null || stock.Dividents.Count == 0)
                continue;

            StockMeta sm = stockMetaProv.Get(stock.Stock.SRef);

            if (sm == null)
                sm = stockMetaProv.AddUnknown(stock.Stock.SRef);

            foreach (KeyValuePair<DateOnly, RCDivident> div in stock.Dividents)
            {
                RepDataExpDividents entry = new()
                {
                    StockMeta = sm,
                    Div = div.Value,
                };
                ret.Add(entry);
            }
        }
        return ret;
    }
}
