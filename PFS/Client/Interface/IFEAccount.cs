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

public interface IFEAccount
{
    AccountTypeId AccountType { get; }
    List<MenuEntry> GetMenuData();
    void SaveData();

    void ClearLocally();

    Result LoadDemo(byte[] zip);

    // ***MarketMeta***

    IEnumerable<MarketMeta> GetActiveMarketsMeta();

    MarketMeta GetMarketMeta(MarketId marketId);

    MarketStatus[] GetMarketStatus();

    // ***Rates***

    Result RefetchLatestRates();

    (DateOnly date, CurrencyRate[] rates) GetLatestRatesInfo();

    Task<decimal?> GetHistoryRateAsync(CurrencyId fromCurrencyId, DateOnly date);

    // ***EODs***

    public record StockExpiredStatus(int totalStocks, int expiredStocks, int ndStocks);

    StockExpiredStatus GetExpiredEodStatus();

    FetchProgress GetFetchProgress();

    (int fetchAmount, int pendingAmount) FetchExpiredStocks();

    Task<Dictionary<ExtProviderId, Result<ClosingEOD>>> TestStockFetchingAsync(MarketId marketId, string symbol, ExtProviderId[] providers);


    // ***User Events***

    UserEventAmounts GetUserEventAmounts();

    void UpdateUserEventStatus(int id, UserEventStatus status);

    void DeleteUserEvent(int id);

    List<RepDataUserEvents> GetUserEventsData();

    // ***StockNotes***

    Note GetNote(string sRef);

    void StoreNote(string sRef, Note note);

    // **Backups***

    byte[] ExportAccountBackupAsZip();

    Result ImportAccountFromZip(byte[] zip);
}
