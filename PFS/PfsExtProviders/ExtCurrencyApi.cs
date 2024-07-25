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
using Serilog;
using System.Text.Json;

namespace Pfs.ExtProviders;

public class ExtCurrencyApi : IExtProvider, IExtCurrencyProvider
{
    protected string _error = string.Empty;

    protected string _apiKey = "";

    public string GetLastError() { return _error; }

    public int GetLastCreditPrice() { return 0; }       // !!!TODO!!!

    public void SetPrivateKey(string key)
    {
        _apiKey = key;
    }

    public (int monthly, int daily) GetCreditLimit()           // !!!TODO!!!
    {
        return (0, 0);
    }

    public int CurrencyLimit(IExtCurrencyProvider.CurrencyLimitId limitID)
    {
        switch (limitID)
        {
            case IExtCurrencyProvider.CurrencyLimitId.SupportBatchFetch: return 1;
        }
        return 0;
    }

    // Note! This is latest as latest, but cant know what days data it actually is...
    public async Task<(DateTime UTC, Dictionary<CurrencyId, decimal> rates)>
        GetCurrencyLatestAsync(CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_apiKey) == true)
        {
            _error = "CurrencyAPI::GetCurrencyLatestAsync() Missing private access key!";
            return (UTC: DateTime.MinValue, rates: null);
        }

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);

            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.currencyapi.com/v3/latest?base_currency={toCurrency}&apikey={_apiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("failed: {0} from all->[[{1}]]", resp.StatusCode.ToString(), toCurrency);
                Log.Warning("CurrencyAPI::GetCurrencyLatestAsync() " + _error);
                return (UTC: DateTime.MinValue, rates: null);
            }

            string content = await resp.Content.ReadAsStringAsync();
            var extResp = JsonSerializer.Deserialize<SingleRoot>(content);

            if (extResp == null || extResp.data == null)
            {
                _error = string.Format("Failed, empty data: {0} for all->[[{1}]]", resp.StatusCode.ToString(), toCurrency);
                Log.Warning("CurrencyAPI::GetCurrencyLatestAsync() " + _error);
                return (UTC: DateTime.MinValue, rates: null);
            }

            // All seams to be enough well so lets convert data to PFS format 
            // Note! Effectivly ignoring what ever 'fromCurrency' has, as assuming client just looks that dictionary field only if asked specific one
            Dictionary<CurrencyId, decimal> ret = new();
            if (extResp.data.USD.value > 0)
                ret.Add(CurrencyId.USD, (1m / (decimal)extResp.data.USD.value).Round5());
            else
                ret.Add(CurrencyId.USD, 1);

            if (extResp.data.SEK.value > 0)
                ret.Add(CurrencyId.SEK, (1m / (decimal)extResp.data.SEK.value).Round5());
            else
                ret.Add(CurrencyId.SEK, 1);

            if (extResp.data.EUR.value > 0)
                ret.Add(CurrencyId.EUR, (1m / (decimal)extResp.data.EUR.value).Round5());
            else
                ret.Add(CurrencyId.EUR, 1);

            if (extResp.data.CAD.value > 0)
                ret.Add(CurrencyId.CAD, (1m / (decimal)extResp.data.CAD.value).Round5());
            else
                ret.Add(CurrencyId.CAD, 1);

            if (extResp.data.GBP.value > 0)
                ret.Add(CurrencyId.GBP, (1m / (decimal)extResp.data.GBP.value).Round5());
            else
                ret.Add(CurrencyId.GBP, 1);

            return (UTC: extResp.meta.last_updated_at, rates: ret);
        }
        catch (Exception e)
        {
            _error = string.Format("Connection exception {0} for all->[[{1}]]", e.Message, toCurrency);
            Log.Warning("CurrencyAPI::GetCurrencyLatestAsync() " + _error);
        }
        return (UTC: DateTime.MinValue, rates: null);
    }

    public async Task<Dictionary<CurrencyId, decimal>>
        GetCurrencyHistoryAsync(DateOnly date, CurrencyId toCurrency, CurrencyId fromCurrency = CurrencyId.Unknown)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_apiKey) == true)
        {
            _error = "CurrencyAPI::GetCurrencyHistoryAsync() Missing private access key!";
            return null;
        }

        string histDate = date.ToString("yyyy-MM-dd");

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20);
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.currencyapi.com/v3/historical?apikey={_apiKey}&base_currency={toCurrency}&date={histDate}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), fromCurrency);
                Log.Warning("CurrencyAPI::GetCurrencyHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var extResp = JsonSerializer.Deserialize<SingleRoot>(content);

            if (extResp == null || extResp.data == null)
            {
                _error = string.Format("Failed, empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), fromCurrency);
                Log.Warning("CurrencyAPI::GetCurrencyHistoryAsync() " + _error);
                return null;
            }

            // All seams to be enough well so lets convert data to PFS format 
            // Note! Effectivly ignoring what ever 'fromCurrency' has, as assuming client just looks that dictionary field only if asked specific one
            Dictionary<CurrencyId, decimal> ret = new();
            if (extResp.data.USD.value > 0)
                ret.Add(CurrencyId.USD, decimal.Round(1m / (decimal)extResp.data.USD.value, 5));
            else
                ret.Add(CurrencyId.USD, 1);

            if (extResp.data.SEK.value > 0)
                ret.Add(CurrencyId.SEK, decimal.Round(1m / (decimal)extResp.data.SEK.value, 5));
            else
                ret.Add(CurrencyId.SEK, 1);

            if (extResp.data.EUR.value > 0)
                ret.Add(CurrencyId.EUR, decimal.Round(1m / (decimal)extResp.data.EUR.value, 5));
            else
                ret.Add(CurrencyId.EUR, 1);

            if (extResp.data.CAD.value > 0)
                ret.Add(CurrencyId.CAD, decimal.Round(1m / (decimal)extResp.data.CAD.value, 5));
            else
                ret.Add(CurrencyId.CAD, 1);

            if (extResp.data.GBP.value > 0)
                ret.Add(CurrencyId.GBP, decimal.Round(1m / (decimal)extResp.data.GBP.value, 5));
            else
                ret.Add(CurrencyId.GBP, 1);

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Connection exception {0} for [[{1}]]", e.Message, fromCurrency);
            Log.Warning("CurrencyAPI::GetCurrencyHistoryAsync() " + _error);
        }
        return null;
    }

#if false
    // Note! This is special function only targeted to be used by PfsDataSrv
    public async Task<Dictionary<string, decimal>> GetCurrencyHistoryStrAsync(DateOnly date)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_apiKey) == true)
        {
            _error = "CurrencyAPI::GetCurrencyHistoryStrAsync() Missing private access key!";
            return null;
        }

        string histDate = date.ToString("yyyy-MM-dd");

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            tempHttpClient.Timeout = TimeSpan.FromSeconds(20); 
            HttpResponseMessage resp = await tempHttpClient.GetAsync($"https://api.currencyapi.com/v3/historical?date={histDate}&base_currency=USD&apikey={_apiKey}");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = string.Format("Failed: {0}", resp.StatusCode.ToString());
                Log.Warning("CurrencyAPI::GetCurrencyHistoryStrAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();

            Dictionary<string, decimal> rates = new();

            // !!!CODE!!! JSON Manual parsing --- as creates massive class w automatic converters
            using var doc = JsonDocument.Parse(content); 
            JsonElement jsonMeta = doc.RootElement.GetProperty("meta");
            JsonElement jsonData = doc.RootElement.GetProperty("data");

            foreach (var property in jsonData.EnumerateObject())
            {
                JsonElement jsonSubData = jsonData.GetProperty(property.Name);

                rates.Add(jsonSubData.GetProperty("code").ToString(),       // EUR
                          jsonSubData.GetProperty("value").GetDecimal());   // 1.2345
            }

            return rates;
        }
        catch (Exception e)
        {
            _error = string.Format("Connection exception {0}", e.Message);
            Log.Warning("CurrencyAPI::GetCurrencyHistoryStrAsync() " + _error);
        }
        return null;
    }
#endif

    // !!!REMEMBER!!! JSON!!! https://json2csharp.com/json-to-csharp
    public class SingleMeta
    {
        public DateTime last_updated_at { get; set; }
    }

    public class CAD
    {
        public string code { get; set; }
        public double value { get; set; }
    }

    public class EUR
    {
        public string code { get; set; }
        public double value { get; set; }
    }

    public class SEK
    {
        public string code { get; set; }
        public double value { get; set; }
    }

    public class USD
    {
        public string code { get; set; }
        public double value { get; set; }
    }

    public class GBP
    {
        public string code { get; set; }
        public double value { get; set; }
    }

    public class SingleData
    {
        public CAD CAD { get; set; }
        public EUR EUR { get; set; }
        public SEK SEK { get; set; }
        public USD USD { get; set; }
        public GBP GBP { get; set; }
    }

    public class SingleRoot
    {
        public SingleMeta meta { get; set; }
        public SingleData data { get; set; }
    }
}
