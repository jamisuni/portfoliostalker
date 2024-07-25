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

using Pfs.Config;
using Pfs.ExtProviders;
using Pfs.Types;

namespace Pfs.ExtFetch;

public class FetchRates : IFetchRates
{
    protected readonly IPfsStatus _pfsStatus;
    protected readonly IPfsFetchConfig _fetchConfig;    // ExtProv selection for Rates
    protected readonly IPfsProvConfig _provConfig;      // ExtProv priv key

    public FetchRates(IPfsStatus pfsStatus, IPfsFetchConfig fetchConfig, IPfsProvConfig provConfig)
    {
        _pfsStatus = pfsStatus;
        _fetchConfig = fetchConfig;
        _provConfig = provConfig;
    }
    protected record RateProvider(ExtProviderId provId, IExtProvider extProv, IExtCurrencyProvider currencyProv);

    protected RateProvider CreateProvider()
    {   // KeepItSimple! Atm can just create this every time as need.. functionality is simple and rarely used

        ExtProviderId rateProvId = _fetchConfig.GetRatesProv();

        if (rateProvId == ExtProviderId.Unknown)
            return null;

        string privKey = _provConfig.GetPrivateKey(rateProvId);

        if (string.IsNullOrWhiteSpace(privKey))
            return null;

        switch (rateProvId)
        {
            case ExtProviderId.CurrencyAPI:
                {
                    ExtCurrencyApi currencyApi = new();
                    currencyApi.SetPrivateKey(privKey);
                    return new RateProvider(rateProvId, currencyApi, currencyApi);
                }
        }
        return null;
    }

    public Result FetchLatest(CurrencyId toCurrency)
    {
        RateProvider rp = CreateProvider();

        if (rp == null)
            return new FailResult("No provider selected for currency fetching");

        _workerThread = Task.Run(() => RunFetchAsWorkerThread(rp, toCurrency));
        _workerThread.ConfigureAwait(false);

        return new OkResult();
    }

    private Task _workerThread;

    protected async Task RunFetchAsWorkerThread(RateProvider rp, CurrencyId toCurrency)
    {
        var resp = await rp.currencyProv.GetCurrencyLatestAsync(toCurrency);

        if (resp.UTC == DateTime.MinValue)
            return;

        List<CurrencyRate> rates = new();

        foreach ( KeyValuePair<CurrencyId, decimal> kvp in resp.rates )
            rates.Add(new CurrencyRate(kvp.Key, kvp.Value));

        _ = _pfsStatus.SendPfsClientEvent(PfsClientEventId.ReceiveRates,
                            new ReceiveRatesArgs(DateOnly.FromDateTime(resp.UTC), toCurrency, rates.ToArray()));
    }

    // "Instant" return of single rate for latest specific date 
    public async Task<decimal?> GetRateAsync(CurrencyId toCurrency, CurrencyId fromCurrency, DateOnly? date = null)
    {
        RateProvider rp = CreateProvider();

        if (rp == null)
            return null;

        if ( date == null)
        {
            var latest = await rp.currencyProv.GetCurrencyLatestAsync(toCurrency, fromCurrency);

            if (latest.UTC == DateTime.MinValue || latest.rates.ContainsKey(fromCurrency) == false)
                return null;

            return latest.rates[fromCurrency];
        }

        var resp = await rp.currencyProv.GetCurrencyHistoryAsync(date.Value, toCurrency, fromCurrency);

        if (resp == null || resp.ContainsKey(fromCurrency) == false)
            return null;

        return resp[fromCurrency];
    }
}
