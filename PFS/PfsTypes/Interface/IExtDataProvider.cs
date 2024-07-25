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

namespace Pfs.Types;

// Prototype for any Ext Providers to access Market Data / History information. Do NOT let exceptions out!
public interface IExtDataProvider
{
    bool IsMarketSupport(MarketId marketId);

    bool IsSupport(ProvFunctionId funcId);  

    // Allows to fetch some provider specific limits, like max ticker amount per job
    int GetBatchSizeLimit(ProvFunctionId limitID);

    public enum ProvFunctionId : int
    {
        Unknown = 0,
        LatestEod,      // All EOD providers must support this, at least for ClosingEOD information
        HistoryEod,     // Some cases this is left out for cost reasons
        Intraday,
    }

    /* FOLLOWING FUNCTIONS: 
    * - return Dictionary of data if partial/full success
    * - null if total failure or not supported
    * - Sets GetLastError() for full failure, and may set if for partial success as warning as log type string to inform what happend
    * - Caller can then compare tickers to returned tickers amounts or actual codes to see what success and what didnt
    * - Similarly caller can check ending dates of datas to see if expected data was received
    */

    // Returns latest end-of-day closing data for specified stocks
    Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers);

    // Returns end-of-day data for specific period
    Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay);

    //
    // 'Intraday', implement if available, must also implement EOD parts
    //

    // Returns intraday, meaning currently active trading valuation / last trade information 
    Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers);    // !!!THINK!!! FullEOD or just some own Time+Last?
}


