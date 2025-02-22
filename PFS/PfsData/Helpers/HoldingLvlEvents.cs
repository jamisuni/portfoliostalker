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

using Pfs.Data.Stalker;
using Pfs.Types;
using System.Collections.ObjectModel;

namespace Pfs.Data;

public class HoldingLvlEvents
{
    static public void CheckAndCreateNegPosEvents(string sRef, int period, FullEOD latestEod, IEodHistory eodHistoryProv, ILatestRates latestRatesProv,
                                                  IMarketMeta marketMetaProv, StalkerData stalkerData, IUserEvents userEventsCreator)
    {
        /* Events to help noticing cases where owned holdings avrg price or specific holdings level is break to up or down by 
         * latest EOD closing valuation. Idea here is to get events telling when example holding break to loosing side in case
         * may wanna sell it off to minimize looses. All these events are per portfolio if same stock is owned multiple PF's.
         * 
         * Following code calculates purhace price per portfolio & largest holding to given stock. Then continues by counting
         * from specied many previous days a valuation amounts for those holdings. Different events are created per these:
         * 
         * 1) 'NEG' (avrg) history valuations are higher than purhace price, but last EOD dropped whole owning to loosing side
         * 2) 'POS' (avrg) history valuations on loosing side, but last EOD jumped over avrg purhace price
         * 
         * 3) 'NEG' (oldest) history valuations are higher than purhace price, but last EOD dropped oldest holding to loosing side
         * 4) 'POS' (oldest) history valuations on loosing side, but last EOD jumped over oldest holdings purhace price
         * 
         * Warning! These calculations currently use 'latestRate' in conversion to home currency also for historical EOD's that
         *          may some situations cause some incorrectness to alarm triggering. Fix would require to hold historical rates.
         */

        // Going to need currencyRate to homeCurrency conversions
        (MarketId marketId, string symbol) = StockMeta.ParseSRef(sRef);
        CurrencyId marketCurrency = marketMetaProv.Get(marketId).Currency;
        decimal currencyRate = latestRatesProv.GetLatest(marketCurrency);

        // Get history for this stock, and see if has at least half days w data fetched
        (DateOnly latestData, decimal[] closingsMc) = eodHistoryProv.GetLastClosings(sRef, period);

        if (closingsMc == null)
            return; // happens if doesnt have latest EOD

        int validCount = closingsMc.Where(c => c > 0).Count();
        if (validCount == 0 || period / 2 + 1 > validCount)
            return;
        
        foreach (SPortfolio pf in stalkerData.Portfolios())
        {
            ReadOnlyCollection<SHolding> holdings = stalkerData.PortfolioHoldings(pf.Name, sRef);

            if (holdings.Any() == false )
                continue;

            // avrg holding

            decimal totalHcInvestment = holdings.Sum(h => h.HcInvested);
            decimal totalShares = holdings.Sum(h => h.Units);
            decimal avrgHcPricePerUnit = totalHcInvestment / totalShares;

            if ( avrgHcPricePerUnit > latestEod.Close * currencyRate  &&
                 closingsMc.Where(c => c > 0 && avrgHcPricePerUnit > c * currencyRate).Count() == 1) 
            {
                // 1) 'NEG' history valuations are higher than purhace price, but last EOD dropped whole owning to loosing side
                userEventsCreator.CreateAvrgOwning2NegEvent(sRef, pf.Name, latestEod.Date);
            }

            if (avrgHcPricePerUnit < latestEod.Close * currencyRate &&
                 closingsMc.Where(c => c > 0 && avrgHcPricePerUnit < c * currencyRate ).Count() == 1)
            {
                // 2) 'POS' history valuations on loosing side, but last EOD jumped over avrg purhace price
                userEventsCreator.CreateAvrgOwning2PosEvent(sRef, pf.Name, latestEod.Date);
            }

            // oldest holding

            if (holdings.Count() == 1)
                continue;

            SHolding oldestHolding = holdings.MinBy(h => h.PurhaceDate);

            if (oldestHolding.HcPriceWithFeePerUnit > latestEod.Close * currencyRate &&
                closingsMc.Where(c => c > 0 && oldestHolding.HcPriceWithFeePerUnit > c * currencyRate).Count() == 1)
            {
                // 3) 'NEG' (oldest) history valuations are higher than purhace price, but last EOD dropped oldest holding to loosing side
                userEventsCreator.CreateAvrgOwning2NegEvent(sRef, pf.Name, latestEod.Date);
            }

            if (oldestHolding.HcPriceWithFeePerUnit < latestEod.Close * currencyRate &&
                closingsMc.Where(c => c > 0 && oldestHolding.HcPriceWithFeePerUnit < c * currencyRate).Count() == 1)
            {
                // 4) 'POS' history valuations on loosing side, but last EOD jumped over oldest holdings purhace price
                userEventsCreator.CreateAvrgOwning2PosEvent(sRef, pf.Name, latestEod.Date);
            }
        }
    }
}
