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

public record MarketMeta(MarketId ID, string MIC, string Name, CurrencyId Currency)
{
    public MarketMeta DeepCopy()
    {
        return new MarketMeta(ID, MIC, Name, Currency);
    }
}

public interface IMarketMeta
{
    IEnumerable<MarketMeta> GetActives(); // only returns market those user activated

    MarketMeta Get(MarketId marketId); // returns meta even if deactivated by user

    (DateOnly localDate, DateTime utcTime) LastClosing(MarketId marketId);

    DateTime NextClosingUtc(MarketId marketId);

    MarketStatus[] GetMarketStatus();
}
