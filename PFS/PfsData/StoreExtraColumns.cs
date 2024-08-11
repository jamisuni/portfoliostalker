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

namespace Pfs.Data;

// Storage of user defined report filters
public class StoreExtraColumns : IExtraColumns
{
    protected const string _componentName = "columns";

    protected readonly IChangeEod _changeEod;

    protected struct Store
    {
        public int Date;
        public decimal Value1;
        public decimal Value2;
    }
    // Everything is set at reboot time only
    protected readonly bool _disabled = true;
    protected readonly ExtraColumnId[] _columnId = null;                // what col 0..3 matches as column content
#if false
    protected readonly Dictionary<MarketId, string> _symbols = new();   // what symbols each market supports (reboot loaded)
    protected Dictionary<string, Store[]> _cache = new();               // cache for "marketId-ExtraColumnId" -> [] symbols per previous
#endif
    /* !!!THINK!!! From current reports this mainly effects to Overview!
     * - Could add one dedicated report w more columns for custom usage, with all stocks user is tracking
     * 
     */

    public StoreExtraColumns(IPfsStatus pfsStatus, IStockMeta stockMeta, IChangeEod changeEod)
    {
        _changeEod = changeEod;



        // Simple array w column 0..3 to match it Enum content type
        _columnId = new ExtraColumnId[IExtraColumns.MaxCol];
        for (int c = 0; c < IExtraColumns.MaxCol; c++)
        {
            _columnId[c] = (ExtraColumnId)pfsStatus.GetAppCfg($"ExtraColumn{c}");

            if (_columnId[c] != ExtraColumnId.Unknown)
                _disabled = false;
        }

#if false   // this is all potentially ok cache code, need when starting to external fetch.. atm not rush..

        _cache = new();

        if (_disabled)
            return;

        foreach ( MarketId marketId in Enum.GetValues(typeof(MarketId)))
        {
            if (marketId.IsReal() == false)
                continue;

            // all users stock to that market go to '_symbols' as position info
            IEnumerable<StockMeta> marketStocks = stockMeta.GetAll(marketId);

            if (marketStocks.Count() == 0)
                continue;

            List<string> paddedStocks = marketStocks.Select(s => $"${s.symbol}".PadRight(IStockMetaUpdate.MaxSymbolLen + 2)).Order().ToList();

            // Adding all stocks as ""$symbol     "" so 12 char total but 10 for symbol itself
            _symbols.Add(marketId, string.Join(' ', paddedStocks));

            // Finally can create 'cache' that holds fetched values this comp provides for others
            foreach (ExtraColumnId cid in _columnId)
                if ( cid != ExtraColumnId.Unknown )
                    _cache.Add($"{marketId}-{cid}", new Store[marketStocks.Count()]);
        }
#endif
    }

    public RCExtraColumn Get(int col, string sRef)
    {   // Note! This expects to be called only if 'GetHeader' returned valid header

#if false
        var stock = StockMeta.ParseSRef(sRef);

        int pos = _symbols[stock.marketId].IndexOf($"${stock.symbol} ");

        if (pos < 0)
            return null;

        if (pos > 0) // so pos tells location of 'cache'
            pos = pos / (IStockMetaUpdate.MaxSymbolLen + 2);


        // !!!TODO!!! Cache usage is totally missing from here.... but with cache comes also updating need...
#endif

        switch (_columnId[col])
        {
            case ExtraColumnId.CloseMonthAgo:
                {
                    var month = _changeEod.GetMonthChange(sRef);

                    if (month.close < 0)
                        return null;

                    return new RCExtraColumn(_columnId[col], [month.changeP, month.close, month.min, month.max]);
                }

            case ExtraColumnId.CloseWeekAgo:
                {
                    var week = _changeEod.GetWeekChange(sRef);

                    if (week.close < 0)
                        return null;

                    return new RCExtraColumn(_columnId[col], [week.changeP, week.close, week.min, week.max]);
                }
        }
        return null;
    }
}
