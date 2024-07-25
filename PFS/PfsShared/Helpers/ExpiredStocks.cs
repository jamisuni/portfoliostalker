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

namespace Pfs.Shared;

public class ExpiredStocks
{
    // Returns list of all those Stalked stocks those EOD doesnt look like latest and greatest possible...even if just seconds after market has closed (only ClosingEOD's NOT IntraDays!)
    static public (int stockAmount, List<Expired> expired) GetExpiredEods(DateTime utcNow, IStockMeta stockMetaProv, ILatestEod latestEodProv, IMarketMeta marketMetaProv)
    {
        int stockAmount = 0;
        List<Expired> ret = new();

        List<MarketId> activeMarkets = marketMetaProv.GetActives().Select(m => m.ID).ToList();

        MarketStatus[] statusMarkets = marketMetaProv.GetMarketStatus();

        MarketMeta marketMeta = null;
        DateTime lastMarketCloseUtc = DateTime.MinValue;
        DateOnly lastMarketCloselocalDate = DateOnly.MinValue;

        // Looping market-by-market so dont have to recalc closings all the time 
        foreach (StockMeta stock in stockMetaProv.GetAll().OrderBy(s => s.marketId).ToList()) 
        {
            if (activeMarkets.Contains(stock.marketId) == false)
                continue;

            stockAmount++;

            if (marketMeta == null || marketMeta.ID != stock.marketId)
            {
                marketMeta = marketMetaProv.Get(stock.marketId);

                // Note! Market Local and UTC! Can trust these to have also holidays taken care!
                (lastMarketCloselocalDate, lastMarketCloseUtc) = marketMetaProv.LastClosing(marketMeta.ID);
            }

            ClosingEOD data = latestEodProv.GetFullEOD(stock.marketId, stock.symbol);

            if (data == null)
            {   // N/D case goes in w null
                ret.Add(new Expired()
                {
                    SRef = stock.GetSRef(),
                    MarketLastLocalDate = lastMarketCloselocalDate,
                    EodLocalDate = null,
                    ExpiryMins = 0,
                    MinFetchMins = 0,
                });
                continue;
            }

            if (data.Date < lastMarketCloselocalDate)
            {
                // 'date.Date' ala last markets local date we have is more than one full day older than latest market closing day then its old for sure
                ret.Add(new Expired()
                {
                    SRef = stock.GetSRef(),
                    EodLocalDate = data.Date,
                    MarketLastLocalDate = lastMarketCloselocalDate,
                    ExpiryMins = (int)(utcNow - lastMarketCloseUtc).TotalMinutes,
                    MinFetchMins = statusMarkets.FirstOrDefault(s => s.market.ID == stock.marketId)?.minFetchMins ?? 0,
                });
                continue;
            }
            // Looks like valid, no action
        }
        return (stockAmount, expired: ret);
    }

    public class Expired
    {
        public string SRef { get; set; }
        public DateOnly? EodLocalDate { get; set; }
        public DateOnly MarketLastLocalDate { get; set; }
        public int ExpiryMins { get; set; }     // This list includes all stock those late even second, leaving caller to figure out rest
        public int MinFetchMins {  get; set; }  // User defined minutes for market for time after closing as minutes after its stock EOD can be fetched

        public State GetState()
        {
            if (EodLocalDate == null)
                return State.Expired;

            if (MinFetchMins > ExpiryMins)
                return State.Pending;

            return State.Expired;
        }

        public enum State
        {
            Pending,    // market has closed, but cant fetch yet
            Expired,    // should refetch latest
        }
    }
}
