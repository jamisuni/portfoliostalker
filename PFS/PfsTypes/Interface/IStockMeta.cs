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

public interface IStockMeta
{
    IEnumerable<StockMeta> GetAll(MarketId marketId = MarketId.Unknown);

    StockMeta Get(MarketId marketId, string symbol);

    StockMeta Get(string sRef);

    StockMeta GetByISIN(string ISIN);

    StockMeta AddUnknown(string sRef);
}

public interface IStockMetaUpdate // Plan: this is where all operations come, also M&A related etc
{
    const int MaxSymbolLen = 10; // some handling depends this, so need ignore longer or update this value!

    bool AddStock(MarketId marketId, string symbol, string companyName, string ISIN);

    bool UpdateCompanyName(MarketId marketId, string symbol, DateOnly date, string newCompanyName);

    bool UpdateIsin(MarketId marketId, string symbol, DateOnly date, string newIsin);

    bool RemoveStock(MarketId marketId, string symbol);

    void AddSymbolSearchMapping(string fromSymbol, MarketId toMarketId, string toSymbol, string comment);

    // Note! This is operation that needs to be called only from one place, as lot of dependencies
    bool UpdateFullMeta(string updSRef, string oldSRef, string companyName, DateOnly date, string comment);

    bool SplitStock(string sRef, DateOnly date, string comment);

    // Note! This is operation that needs to be called only from one place, as lot of dependencies
    bool CloseStock(string sRef, DateOnly date, string comment);

    void DestroyStock(string sRef);
}
