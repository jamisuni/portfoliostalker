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

public interface IFEConfig
{
    public CurrencyId HomeCurrency { get; set; }

    MarketCfg GetMarketCfg(MarketId marketId);

    bool SetMarketCfg(MarketId marketId, MarketCfg marketCfg);

    // ***Providers***

    Dictionary<ExtProviderId, string> GetProvPrivKeys();

    void SetProvPrivKey(ExtProviderId provId, string privKey);

    // ***Rates***

    ExtProviderId GetActiveRatesProvider();

    ExtProviderId[] GetAvailableRatesProviders();

    Result SetActiveRatesProvider(ExtProviderId provId);

    // ***EODs***

    IReadOnlyCollection<ProvFetchCfg> GetEodFetchCfg();

    void SetEodFetchCfg(ProvFetchCfg[] allCfgs);

    void SetEodFetchDedicatedProviderForSymbol(MarketId marketId, string symbol, ExtProviderId providerId);

    IEnumerable<ExtProviderId> GetActiveEodProviders(MarketId marketId);
}
