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

using Pfs.ExtProviders;
using Pfs.Types;

namespace PfsUI;

public class BlazorPlatform : IPfsPlatform
{
    // [inject] only works for blazor components, so here we need to pass it as ""constructor injection""
    private readonly Blazored.LocalStorage.ISyncLocalStorageService _localStorage = null;

    public BlazorPlatform(Blazored.LocalStorage.ISyncLocalStorageService localStorage)
    {
        _localStorage = localStorage;
        Demo = null;
    }

    public DateTime GetCurrentUtcTime()
    {
        if (Demo.HasValue)
            return Demo.Value; // !!!LATER!!! May need UTC conversion...

        return DateTime.UtcNow;
    }

    public DateOnly GetCurrentUtcDate()
    {
        return DateOnly.FromDateTime(GetCurrentUtcTime());
    }

    public DateTime GetCurrentLocalTime()
    {
        if (Demo.HasValue)
            return Demo.Value;

        return DateTime.Now;
    }

    public DateOnly GetCurrentLocalDate()
    {
        return DateOnly.FromDateTime(GetCurrentLocalTime());
    }

    public void PermWrite(string key, string value)
    {
        _localStorage.SetItemAsString(key, value);
    }

    public string PermRead(string key)
    {
        string ret = "";

        try
        {
            ret = _localStorage.GetItemAsString(key);
        }
        catch (Exception)
        {
        }
        return ret;
    }

    public void PermRemove(string key)
    {
        try
        {
            _localStorage.RemoveItem(key);
        }
        catch (Exception)
        {
        }
    }

    public void PermClearAll()
    {
        _localStorage.Clear();
    }

    public List<string> PermGetKeys()
    {
        List<string> ret = new();

        for (int i = 0; ; i++)
        {
            string key = _localStorage.Key(i);

            if (string.IsNullOrWhiteSpace(key) == true)
                break;

            ret.Add(key);
        }
        return ret;
    }

    public List<ExtProviderId> GetClientProviderIDs(ExtProviderJobType jobType = ExtProviderJobType.Unknown)
    {
        // Decision! This doesnt NOT care about keys! So providers can be selected on use wo keys been set

        List<ExtProviderId> ret = new();

        switch (jobType)
        {
            case ExtProviderJobType.Unknown: // return all those works on WASM, used mainly to set keys as some are currency only etc

                ret.Add(ExtProviderId.Unibit);
                ret.Add(ExtProviderId.Polygon);
//                ret.Add(ExtProviderId.Tiingo);
                ret.Add(ExtProviderId.Marketstack);
                ret.Add(ExtProviderId.AlphaVantage);
//                ret.Add(ExtProviderId.Iexcloud); !!!IEXCLOUD!! postponed
                ret.Add(ExtProviderId.CurrencyAPI);
                ret.Add(ExtProviderId.TwelveData);
                break;

            case ExtProviderJobType.Intraday:

                //                ret.Add(ExtProviderId.Marketstack);
                //                ret.Add(ExtProviderId.Iexcloud);
                //                ret.Add(ExtProviderId.TwelveData);
                break;

            case ExtProviderJobType.EndOfDay:

                ret.Add(ExtProviderId.Unibit);
                ret.Add(ExtProviderId.Polygon);
//                ret.Add(ExtProviderId.Tiingo);
                ret.Add(ExtProviderId.Marketstack);
                ret.Add(ExtProviderId.AlphaVantage);
//                ret.Add(ExtProviderId.Iexcloud);
                ret.Add(ExtProviderId.TwelveData);
                // ret.Add(ExtDataProviders.Tiingo);        Doesnt work on WASM/Blazor
                break;

            case ExtProviderJobType.Currency:

                ret.Add(ExtProviderId.Polygon);
                ret.Add(ExtProviderId.CurrencyAPI);
                ret.Add(ExtProviderId.TwelveData);
                break;
        }

        return ret;
    }

    public List<ExtProviderId> GetMarketSupport(MarketId marketId)
    {
        List<ExtProviderId> ret = new();

        if (ExtTwelveData.MarketSupport(marketId))
            ret.Add(ExtProviderId.TwelveData);

        if (ExtUnibit.MarketSupport(marketId))
            ret.Add(ExtProviderId.Unibit);

        if (ExtPolygon.MarketSupport(marketId))
            ret.Add(ExtProviderId.Polygon);

        if (ExtAlphaVantage.MarketSupport(marketId))
            ret.Add(ExtProviderId.Unibit);

        if (ExtMarketstack.MarketSupport(marketId))
            ret.Add(ExtProviderId.Marketstack);

        //        if (ExtMarketDataTiingo.MarketSupport(marketId))
        //            ret.Add(ExtProviderId.Tiingo);

        // !!!IEXCLOUD!! postponed
        // if (ExtMarketDataIexcloud.MarketSupport(marketId))
        //    ret.Add(ExtProviderId.Iexcloud);

        return ret;
    }

    // Following is not part of interface, but as Blazor cant access files a demo content is available
    // here for client side. All demos on V2 are going to be frozen to specific date.

    public static DateTime? Demo { get; internal set; }

    public static (DateOnly demoDate, byte[] demoZip) SetDemo(int id)
    {
        if (id < 0 || id >= DemoDate.Count())
            return (DateOnly.MinValue, null);

        Demo = new DateTime(DemoDate[id], new TimeOnly(13, 00));

        return (DemoDate[id], Convert.FromBase64String(DemoContent[0]));
    }

    protected static readonly DateOnly[] DemoDate = [
        new DateOnly(2024, 7, 24)
    ];

    protected static readonly string[] DemoContent = [
        "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"
    ];
}
