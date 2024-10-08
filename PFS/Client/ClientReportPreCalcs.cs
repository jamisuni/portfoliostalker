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

using Pfs.Reports;
using Pfs.Types;

namespace Pfs.Client;

public class ClientReportPreCalcs
{
    protected readonly IPfsPlatform _pfsPlatform;
    protected readonly IPfsStatus _pfsStatus;
    protected readonly IEodLatest _latestEodProv;
    protected readonly IStockMeta _stockMetaProv;
    protected readonly IMarketMeta _marketMetaProv;
    protected readonly ILatestRates _latestRatesProv;
    protected readonly ClientStalker _stalkerData;

    //protected record ReportCalculation(ReportFilters filter, string limitPfName, ReportPreCalc preCalc);

    //    protected Dictionary<string, ReportCalculation> _preCalc = new(); This someday later, maybe

    protected ReportPreCalc _calc = null;
    protected ReportFilters _filter;
    protected string _pfName;
    protected ReportId _reportId;

    public ClientReportPreCalcs(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, IEodLatest latestEodProv, IStockMeta stockMetaProv, IMarketMeta marketMetaProv, ILatestRates latestRatesProv, ClientStalker stalkerData)
    {
        _pfsPlatform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _latestEodProv = latestEodProv;
        _stockMetaProv = stockMetaProv;
        _marketMetaProv = marketMetaProv;
        _latestRatesProv = latestRatesProv;
        _stalkerData = stalkerData;
    }

    public void InitClean()
    {
        //        _preCalc = new();
        _pfName = "";
        _reportId = ReportId.Unknown;
        _calc = null;
    }

    /* Important! Plan was that this would be collecting and holding few different pre-calculated report things, those 
     * some would be used example overview/invested reports (ala all stocks), some for portfolio stocks, etc 
     * => target been that when moving from report to report most time no need for recalculation but can just use
     *    cached version from here. PROBLEM! is that knowing when to invalidate these can depend from so many things
     *    like new PF, edit pf name, add stock, move stock... not just when new eod is received. It end up not been 
     *    worth of going this complex way yet, maybe never.. well see
     * Anyway here is place to do caching per report groups.. but to keep things simple... atm this simply just 
     * holds name of report, and refresh happens by visiting some other report 
     * => each change from overview => invested causes recalculation... not worth of going fancy yet but maybe someday!
     */

    public IReportPreCalc Get(ReportId reportId, ReportFilters currentFilter, string pfName = "")
    {
        switch (reportId)
        {
            case ReportId.PfStocks: // delete stock from list would not get updates, so skip precalc for this..
            case ReportId.PfSales:  // delete existing trade didnt get updates, so skip cache and recalc always
                break;

            default:
                if (_calc != null && reportId == _reportId && _pfName == pfName && _filter.IsCompatible(currentFilter))
                    return _calc;
                break;
        }

        _reportId = reportId;
        _pfName = pfName;
        _filter = currentFilter.DeepCopy();

        _calc = new ReportPreCalc(pfName, currentFilter, _pfsPlatform, _latestEodProv, _stockMetaProv, _marketMetaProv, _latestRatesProv, _stalkerData);

        return _calc;

#if false
        ReportCalculation ret = null;

        if ( _preCalc.ContainsKey(key) ) 
            ret = _preCalc[key];

        if (ret != null && string.IsNullOrEmpty(pfName) == false && ret.limitPfName != pfName)
            // Portfolio specific ones needs also check it same PF
            ret = null;

        if (ret != null && ret.filter.IsCompatible(currentFilter))
            return ret.preCalc;

        ReportCalculation entry = new (currentFilter.DeepCopy(), pfName,
                                       new ReportPreCalc(pfName, currentFilter, _pfsPlatform, _latestEodProv, _stockMetaProv, _marketMetaProv, _latestRatesProv, _stalkerData));

        if (_preCalc.ContainsKey(key))
            _preCalc[key] = entry;
        else
            _preCalc.Add(key, entry);

        return _preCalc[key].preCalc;
#endif
    }
}
