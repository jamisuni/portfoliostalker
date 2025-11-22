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
                ret.Add(ExtProviderId.EodHD);
                ret.Add(ExtProviderId.Marketstack);
                ret.Add(ExtProviderId.AlphaVantage);
                ret.Add(ExtProviderId.FMP);
                ret.Add(ExtProviderId.CurrencyAPI);
                ret.Add(ExtProviderId.TwelveData);
                break;

            case ExtProviderJobType.Intraday:

                //                ret.Add(ExtProviderId.Marketstack);
                //                ret.Add(ExtProviderId.FMP);
                //                ret.Add(ExtProviderId.TwelveData);
                break;

            case ExtProviderJobType.EndOfDay:

                ret.Add(ExtProviderId.Unibit);
                ret.Add(ExtProviderId.Polygon);
                ret.Add(ExtProviderId.EodHD);
                ret.Add(ExtProviderId.Marketstack);
                ret.Add(ExtProviderId.AlphaVantage);
                ret.Add(ExtProviderId.FMP);
                ret.Add(ExtProviderId.TwelveData);
                // ret.Add(ExtProviderId.Tiingo);        2025-Nov: Doesnt work on WASM/Blazor, has CORS issues
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

        if (ExtFmp.MarketSupport(marketId))
            ret.Add(ExtProviderId.FMP);

        if (ExtEodHD.MarketSupport(marketId))
            ret.Add(ExtProviderId.EodHD);

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
        new DateOnly(2024, 8, 17)
    ];

    protected static readonly string[] DemoContent = [
        "UEsDBAoAAAAAABaOElnV3EDZGgAAABoAAAAKAAAAY2ZnYXBwLnR4dDxQRlM+CiAgPEFwcENmZyAvPgo8L1BGUz4KUEsDBBQAAAAIABaOEllQWT06wwAAAIsCAAANAAAAY2ZnbWFya2V0LnR4dK3STQuCQBAG4Hu/YtnzgDqplGiwkNEhtVoPdhRbQooMv6B/n18VgWgHbzOHeZgXXnO/4asZIaYTpleRZ/VcbS7ja3YgLMrjUlg0TwtByTa5xefwmVkUZVQNLh6AhpuUgAvApbEWEaAKqFHixHeLKguZEukNnrg9IcccO5iQ8/mIxooLaJ3pRTkoagtqgHof6DnBdkhsz/Xqo9aAudIxqP4yg0qTT6mtMWnH7fGEqP9DBbZ/ZOPhvorcB5nSp3Gm1JTwBVBLAwQUAAAACAAWjhJZ5i/Dd8kHAACoFQAABwAAAGVvZC50eHSFWMuOFDEMvPMVCHEcmsRJ7EQCJB6LQLxZQMANCZA4cYD/F1WV7h4Ew7DaKSeOY8d52Enfevnw8s6Vq1dvXXz/fPXBp59fbl+zZPVG6jeyX2ML2sD/dPXy9Zevt6+9v3jz+u71x5epXrt68eLB79KHspi3QRK9k4ycf2O2UQ6lj6h+7eqjbz9+3r5GyVLQVrJNMma/4epQVSuzu5c8SZB0c5GIVfXRUI5J5iByS4WklqZaiNSoIj2LWUV6uMiQToP1a1dv/j0Fzz9cXly/+/aE/3mp6JoWKEfZm8qdWA+WSnXfXbegFBDgixcWeyd6HGwsUYgdgMajniFlqyXry6hASBCzCzsxhVQJs2CiTNqQyL/cu3v54O6r6/ef3b+8e8JHjMeEldgLsQnskEvNxSw2J2taTF0GIcMDjNiILZNT2BEDEadIxsTJU+FqDWraAGL4QMirtWx64MogWpW8JL0Tsbo1Y4+ddfTZq7+9bEu1DRoL/HGSi0cum3ttyeVQlw6LmOqthK3Y4NkGGVUMgaUhLbvevjcAMPq9x2ApE4Kgkag0zjry/OHF357ksqR2uJHnP6usbR7kzo2WtTGyryjwTnbCmqK9LlldBcOlZdVsaHOi2cHa4gLssrx0NGIhhIX7n35mLFwlViNGPevRyw8vn/7tkscyiJxLxxZoLJcB5HEYxVPq+zlzTaXnBZHFcXTQ0ZZoQBp3jLsTww5e1Ir5hzznYirczGEfoZwODdu2UFUEy4F10nFrgSlg2QZQHSu1xfljdvni4eO/XYyDLyMfsAAYHbbVrFo1H97b5ltw6rFU8Iaz6poQ7Bfy6ga179W8aoXeXgE4QphEVNGbJVbRjE3JGktFEiaJc8Hw9amdlysOo0hvIA06WFMxYUAdQX8PFWIa2pAF2NeWVdJMNaMmBnwRp8KMqDBrPts21btpY3AB4aSAQFkmgfcgGS7OYQWIL4k1xicQOE2C7qrB+hnvnzw7sYJQD9PAnIU6AKP7yvIWPpJt3lM6dFzQhxFOZSeiyEahScnv6LuJtVMrKjdiHds5zAijk3/UVoQmmVBrLme8fPro/QkvDecPGNJpjCYgTaMz5wBqRHjsftpQoCgJUU798pR3yffCJoxVukohEVPJUSLJV82bbQZ+EuRTB6k4l2RqxpksTG2ZkapwZ3MIlVYhCZXKw9P4uTV+fvHmb++7LaXvsZVVMI/BNeqCYwVrOM+wcgjHFiUWlbuJjWOm/N+xCo1YpXhX3xMzRcBXI7pPJH9Aj6qpCmlnVZ4Ky+2MR+8+/uWQEmcmEtbMm8imhdEjjVGOuZ07C8jbEm8qSvZClRt7wyWVi8rij6lus2UYqjCQqufVYk3nfSidS/to6jX7lqlhGjnt4ZvL99efPXx8asPi8BOHIIgxGZh8t7QH1zWZcUgg3GnzCsCsp17UMPe7YvMstM0CAxCxTQxizcRQ0uxFZUmOobISKEQYpv7t1pPXr/9wKzHgB3+VGSMzG6A6E23garwnQ/Ike/xzrOKWYrzuUChGTWUopWxQxfvHherFs/ePcFIu35zKBgPT0kRCIW6Wy5Fk3K3qHhEPWReQZo1ksCt3OLFlYh1Em3qKDFj+XWdVT1YUGhjTxSCktZEqcqY85023IRkCnnPxxZPHp67GWHIDxgQxelGZ5lJqpda6O4g7lN4ptVgj0VsJD5ImErNCKbxnSKSqjb4q3M21IKviV0Ls2or0OTGJ1UqfRKrmc8eR+c/fvi5enljHMBwJkTZJcKHyMtt07JuFp7Jn9qi6M844hdokhhVUxyAxDH6Syax2ZMLfvOrezTsWmJJJIj53gOPFQ+K6JjiWNqQsrQNtUmZUxmfeVPafx8HFmxMrDQd1hy/Q7SC6kDSeedZ0bcrVx0h53891KNHUjlGBRGPoqAOTMcQcUJf01mvcrwGCtEitHesFgvO4Kd/sV15rh5T1BgIRKkObVPqQhAVWhPsBNVidyArmgJ0Ri87l/QcXJ3Z6BAYMoltaYXLiHua6YS9yjnl1QbooezIsDV2w7TAS7sEKqxWECR0ErnYQvP9WZhEzsQMffyBwZNV9tN/5vAJBKtEwpAwZdw5GD0huDaYT5BYSREC2OWaYIhsZ5576jx6cmAE3GWtD+c/h1bGSdBuqNbXfPnU0TFKHDLMIvZrOFU0K8h/9RtyZxEmwqJSgMnVzX1WvxjlwJUnHfHUQ3pxJ5H7jy1ndy7Rjg0w9mB2TMQft7M4vB2fcv3f33okj0HWCOgaCvFl1XdE0Bh94fKnk0fLufYTasDMbbidreQC49zrHyI5j6FaTqUqMGlPfai0YuNm/Afl21tVJnExGJ0yBoluSAVnoZy9Cj+8/PrHAfF0QMbN0rJFUkXnRLHhI7DEOTCwdcH7ymZ+CBOpu6laK0MRX2bPU7dYSkiNQKzcXLkFSKMU2OV2SU/PUJjT/963h8u2JKMZ7ghARRZ8AGhO8yg3RqEXqu4eMIBVMft4QiQNYCFpVMatyVNikAY6ju1T5VLUZwhrOTgxgWBOU4SPK6mtYVbVOEWIi6lXdMLrz3z7eXJ64oK+vOsYToNJBw5ERhx8wcoljhAbTpliHGEZPbEXlTCxD2I98r8RoU+Fmjh8CgNRzxCGU/k4IU5FjXBX0INY9H926iU+yd67cuqkvtL8AUEsDBBQAAAAIAEOOEln1S9m2UgAAAHYAAAALAAAAZmlsdGVycy50eHRtzTEKgDAMheHdU4TskgtEwSWTNIW6ODp0EKqDunh7TaGduuWHjzz2EsYOgGVPT7xuu2uB24444KRLr2d6EbzkQqCW0/A7N6/mrIpjqs+Z8t4HUEsDBBQAAAAIABaOElno8DqbzgMAAOoGAAAJAAAAbm90ZXMudHh0xVRtb9MwEP5MfsUhNqmV2jCyImASoG4wXqTBSjUQQnxw4ktiNbGD7TSEX89jp4yXP0BVtXF8d89zd8/d0X/6JF/uvVtvX6w3R1ebe8+yk2y1XPfVGW1NI8mUJZXGUmta1n5BlkXTjFSYHpe12DPtuMP7vPeUM2tyhspG+PDP3zsuvNIVVYxbUexooMYMOUIkS7oS9ht7ESDYOhKaWLhx6c2yd7ygojG9XObCsVyQ6Vgv19dvqENs8GkjqaJ33rTB2RtSzvV8eKV+sKRCWEmdNZUVrQv2CZGvWVliLYEBv5Q+KV+T8g4JSraahC1q5UG7t+DwG6AAvUb0uqj/iTsggIIflTzQwLxzFMulfe0WCeGi7eGUs/dsSTT41cKrPQfOXuxQHrDCyQqpvDJaNGRN7/mMRAcUUdQwidmF/1zoXaiVpIYrUYyBSMHOGXBs1I6BeKmQ2p5ml2+2H+fRFE/xOF/QUKuintoX0JlGFnYK2KqmAT74lyRN04hY119Zi7/yTtG/Nx4CcLhw3hpd3RaLQs8OQEoXTS/Z0Xlj0P7ZdgMOb6+vjK1Q0BmecL7J4TS7OX/5AYfIxFgOCNkmO/3wcntGr4HjVKVZhkKb3kbaEJhn7cA5dmHCCJQLVF9pqKEzA9tYPu9w6PoGXhfC1bTuutuEZF94mvXwaVAESRiBR/Pkzh0iypUnqYJAof4Y1woY+7FjKDw4l7BoxZgzaeOJYVp4ls/TNMVkcBfAhW8xHyOBpxMtU5Y+Or05IuFi/R4cQxUYKzlOkNvzC3Qb7xDf7Nli2mpV1YDrtUS4OGtBMhruTYluAd/u2COfLngWIb/BkOR8GsM9TDVXk+ouLy4n2NVnBLIaOir9hPz0Gb2DiCVGHIOax2oOf6SfQyAgEEelCUiVwTEWXGCcQnnKPrYD39MNdkVnLCq7fHhyfFgGaKAGUxs49TyferyKPV6lT26OzmiT0av1+ppeXm9DakcnaXaK6RFoYD6G40mW0ofJPxo8yB6m2aMrwKwepo+P6fP9z/M/HE5R7KuUZoMYYX6bQ1ELXbGc2gzQxa91dND0O/aTUDrj4lhOWGl2Pmltr6zv4yr8wfZQaxgFnWG9CV1AHzUjyGx1cvx3j+7Ok7v0e82+x2rDZpumphTOE6hO6820ndCKXSyxRUrIeNDhEasiytctYqohI2zrsB3HsK0Gch2zTGkrJChCRSMoAkgCGsGmSREOrJS+HdyoLNA2A+L9murKmiFkpnk4GE57U4wIZCZpbn0Idx4SuABnzKQIJYPZNgaBf1Tl3SS59zVJkp9QSwMEFAAAAAgAFo4SWTMLGH2KAAAA4wAAAAkAAAByYXRlcy50eHRNz7EOwiAQBuC9T0HYWw6KlCZIom3VxKWB9AGMYVSTqoNvL700FAYGPu6/OzOevC0IMZfXI3TfeQ7P+88OkzMsf8Ev7vYJb9LHe08FCFmCLnlDF4vaHXri0KBSSqgWKGGrxbzVeAV4NvPDNdWBVnqX2eS3zBaUrDM7H8eUyZtapn6G4aC2MAyX+wNQSwMEFAAAAAgASo4SWYbUxa52DgAAAlwAAAsAAABzdGFsa2VyLnR4dO1cW08jNxR+76+YRlRqHwi+XyToikLSRSyQktDbWwqzJSokqxDa8u97PEnGHo898STQJ7aRiuOMPx+f+7E9h4P+8PuvsuxwMJsvPs8eJrMn03S/yC7Hj/lR5/hq1DFdRedwMbv9Kxte55+POiefroa9073z6+tONpqPb/+aTP886izmz3nx+9UT0HOXr8ZeDzJ+yLPB2d1RR1OJicJYdbKb6WTxdNShHHWywWA+uc2h0TU9g34Of6MuopKY5ul4AW2CCN5Hep8Q+OpyZr7qZFfzyZ+rgRSCgYYFiKCcI8kwtFcDs66i0FoPrAiDljMuhqEVfLUed3ANfeanQgjzfbV5kEwde33qMBcIccExt+TxLjyOHQKJQNKlkO5jsk/wzhRipDkGorgsSeRVEhVzSGRaYpdEso9EncRyoJJEmIPgTBoUSyKrEIhZhUC2j+CjQwRKSuBrp1Uh7/BgLbDrdiHyQQW4PB6eHv+0d3JxMjzeWgUwlkgLxpV2pMRZQdKVdgUpUYR4K6iBkd4KluOsFxAeY1LyQujXA+su03YFsWYwckVEEAweEBHcRUgbfXGbS3ItfaeTvyd3+XSRDcYvg3xuJmRQCNDS+xc6HQIwAl51zA8f4YFqBxGd7OR5Ps+nty9HnZvhaScrUY2AH2wLC9SBftESttpBeBDWDKWZor4+HBhu7iRCFz9tLz9IcCqAu2sVNGQ4Kii71LUyUvsayEGEwvJDrJGBx0DX4eNqINYVI4MlU54OqogO6kJbnVaDiaFIUw32Dlsf4dLHulQ79JHC4LjshE9EP+jb0Ue5+d5t7mplLvu9Jhn5OHu4g46qlAye5/fj23IhiRQUCWIXkjnrSEBQhHaWEkwq4bW1JChia1CFfJpoEHBAMcU+tvag2kFUXDEJTzcIIVgNPjEAWxhYWYe1pLJdYDGpwVoHLZuo5dvDMuCkt8i2oxEWc7oTrM9b29G8yFzVjO5KuBsEXkhwrYhx61mFJ/COa8UMfg/NagiBRU3c7UDWfiG8rby/8ZocHqwMQxtTM/ht8GkHf0TAYGOlbdSLlV11zbpi6Y/W6qO055BA8Jm36uVAa3sNxkxQzWkZ9BpgCAq4tdcKfGLFXpPCXtOQvVaam59Wmwdxjyu1VjAB63GJ43BplzkUgghyXnO52A96y4HKkI1wLSmWTsyrSBd8vbIkQsogaZVEjCNxvV6mAG5zV5c0vOqf7eiTEILchAlll1KVKmoWoaKihuveSkKqpiKywpHLYKl9ctezSxVsjThRQhgRW0Mgd67CSJ+dqxR+Nif3MfXmageybFdaIsy0m6xyWfK8zm0eFGjGjGBUmnWBjnNBviYXlpSBumJBCXEok11XmoF2YwiHVZePLH01fg6d5g7S/Nuwt3d8vXN4pbgWEqySlQ/HKsCqdaV2zAL2YysOrIwsIqnEVpgnORvcJSwYXREdiDcK8WQNjp+RRMfv49ooSgXjHARq0ZRuqXRcGYqvgrisEFoSxVVayx1wV3FUiesFFbwhrmOpcR3u0lAwESNXNsJShl89mCjU6mbrQEJpmDcYQxZ0DQRKU8RJVsAR+vZWAMWR6llplACCEYm1E0cUtcD1sEjxisVdppNBi0RWKZ9tJqkpJJaiFhWa8iMUP0pGekVBFpdbplVjeaIeyyggi3CU5tU4l8mxjOvVsOQY6yJYsSNzE+yUIyPm+TZDa2okk7bSBJanVoAqlpqXS+2V1lC8AIVetRBktOX8Ysd4ShKtKfDIib151ZMz7HpyQanLTlSQzJMCKq7T3BCsuiC8ZqqQMfwUlcvudoBlFnEJ5yzVI0SgcVHi00HlIo3QhOp0aBnAZWHcQotkAy5LrzEEcaW31NbKNNLL0G70YhSmt2Axb2CxSnW+VVwbkHpe0OaWjbhathAtKeuiRTwWe4ZSNMQ5rUgOQ8sYtGr0/QqlhhxRaIzCC75kdIMJZW0ELABtvHFQtukGXmss08uFsH0ShGYe1TaWb4ZGYmdoGYbexGukUsP4GHTMbtMNSg37Sy14HYJmMV6zTbwmuA20DEDH9Jpt4jUlLUxKELqw3jgIrZqppvxNYvpPH3/dLUoB5mL4R6iNUtwgRVGn3oAhaxZeYG80LxKjVEIUitJCcDBEJOA0jUQHnSYN7UtaVJ7qrCO4vK7aNqFpCk5SzXgEV4MahTOOxuCEsVRDGsbF8AnSi022FcdVOH2dcQ2XFPy1xqTaQZpMKEk1oRFc4a+z7cCN9f4dcYG/OJzmNEYmWqd6qyBuwV8ZjA1MPBbHFak2G2BDBSf4hAtdNJhA273odDUK4gpvmW1H4zLT5LQmgltnr93Y1A1egqSrbwB3ma2FNzBhQk0+OTXgBVwRLLAhGdvBbBArnBzjB3EL/sa263DT9jSim71xvagsYDdACaXjrtIpKivl15TTPKVGafv17xL/LvGvLPFbxJ+XvdFu8ac56cMUVsoemSqKXs7JOOfInRbV7dulWKecghGY77jnGJgpfr2ZrmqzmnF4kjm1WUGhTuhspXNZPxqIabg2WzznNnfbl/v59x15zYiC/7RJrErSq8crpbuNULj/gbcvJhO4DdsVqacgheDBtBqFrQputCpMpFqVNXA9qUbhzLbZnCWH3zFc+EQy6mYzilLj4BiwhMUO59PNwFRuY8826ziHA46w5e8ccHQ2jt2zJEzTQjz9emPkeCNxDxUIcHvV47+k9amCZZVy6DRTd14EC9Urg/6MbOKDIuln0AQnvIYc0zXSqGu4a3jUanONwBNQtqTcclY5toeB0XDMtxL27Kotq8bOrqrXZa7JzYuzRk5z+yMVo+Gvexf9s+33fjGc3mKM6fDeLxylEO7CoYpS4OURsk1bv7BhTDE25+7t1i/sjwlnT7JwBkMv4pHBWxPc/NK2UvfGsAoWnhCPFJ5oRTpPjkE6LWhyoh7BFb4+2g6K4riqxUn8IK6O4WqwbXFcoVMz9TAuJh6u7WhaZzidlW5/gnUnD9Z2NMISnO73gmWnMOwG7kqWfPAnDKvDsBuYK1tENXXUOmttRxOxbSSK4GDNiYTulGzSXJlcUgzi1nlrO5rJFamZYRDXY65/2qvBYtAW3K3jeuz1YlkSx20TORDSMgOP09tmlUOoAj6xA+KqSapaBSvYHIPSyjnVrKvnrbh7OUQz79x2021F7d2z4YVftd4cS8fpUviXeM0GtsswfO80U8/hv9uMd5vxbjO2sBltsoBfe6Pr472zIWJNicDV/C6fe4lA/vDgHEcEaSzvE5urgp+qtoEiL09ZjtiyTIQo5XBhBHc8YIssmHvREFVr7eHjpnakRLuEJJbhS2IsdklMN93WSpciBNeDw5eTaEyOREOtRCbUfutMYETAeVTpVDtJlQnQdDI/aAjpsUHFtzzIOxvS2KARguO42GEDq7ABZklcLnB7rd8WM2N3Xdg7F5K4ICRUBglcyY1yAUOJxmND/QIjfgU2/O+L0b6KCkV+BpebtSOz0rsmLjGu2G9F6ueK/FJqZaxhAUQZgV1ZTNwLWuZ4O7SrYw+9w0T2hSLpdbXDg/J1LJHXs1wN3+71LILCfXqkrU/kKP7+EqhAmk6vwBtxidy5G8AoZhRyAndBpft6FpMSeEXM2P4T8AGZDq99ECdQUigkhS8/kGIaA3v/37v41nSlT7wVgVB+MiNX2i0I5G9AIOcKrpEITV0CtXQJFNDwbLjejrw2AejVxa8fYbt4ONr1bh+U8OGqLWh58CY5VpVLMkgV7Bz4xjJ+k/xg2/3wJYFX52fHOxLImSCSSeG8G6JJUIrt8go/o5rews+ET/wSEducrOQxvZvrdPfOgtuROLodSbdGCp7nBaSk/UdA2sZXtvQgg9kC5jEZP3QaX1HTG9WEDCbX9MjlaDhq+cigN0h+whwP+OH4h/RZmQdOe61+/vG01c9/Pjs5S33A7JENb0K/rvOt0gQ+r4eCP+PXmS1qfruYzbPl/446Fy/Lv546WX+SP4D6X/fORk/uPI8fxvPHbPTyBcTjZgrZNCTd+d/5g3GQrDB2S03/4ea3DNMvt+XDVvaiZ3dazGuU395PZw+zP19ik7v6252b2RiU67kNe58+ZVf9fnbWz4x6pdHHcVe51EEF47SXQJ2tdbSgrzfqJy770gDbaYnzXtKSmILJ+rmiqmJqES34BTcS29AzzeeJvFqxys7sQ/bL/cuHTH+T3a1M6ddpS2Mu1pP1SKez5z8e8psvX6cSCJcZWhB4mn8Gy5wnTkzAK6QqwgSydd7LFrPscfxXnk0WGfkmYZ7LzfQ2fOjPZndpczTXqMsZThY5TBHz896HhGmVry5rMbHNCl2dXsHa+vSyb43Hy79LmGUZAraxh0aOU6eIfdXELFXB1i/2erOpmZckVqaWqvs2rmwxtZPZ4+PzdHI7Xkxm07QZLt8sYvn74cOHbDq+z/4ZG+WAm/HZv8Byfp5igcuX0rRTlcnUCGWSzcK4q33vkkYnFC5LOn8B2n7Mpyfz8dN9spn6+fcWNG3DiOXLsKyoKCMp2YFhwvle6izBU6Zg2bW47p187J2cp47/8TRpfG4i/C0gbKCbAiPMLqJdsXQAiHFTxsfmbOq2K3V8kwQBCBX7QBLtg/Pm06S1KkmxnpC00mrzSrIUIAZn6sro9I/nl3SgIhxPWrUKMSd72eQpK6KZm72slRyY9CgFD4e8YBpRZVKSgkMYDFtlEqB8fXF2mWymIB1Lkgeuuoy5JEX5VP65zHpWpm6Nj4oUtmoDUc0A9rH5avr0/Gim0CdOHNen1qX22Toi73PHMfTFUefHIt4wPXIdXfUVwI6nz5/Ht4vnOeRv8JU2Xy3yOeTS5rcYpjK4hzUY/zCZmTZZpVrmbwp/z15WP+RuaGS+AJirxX0+d5RxSNb5+sN4uqSzf3Xd+7l3vaTw8mrfOKUlged5/oVB2eFlPcDhQbl0hwfFu7//A1BLAwQUAAAACAAWjhJZJydotEEDAAAADQAADQAAAHN0b2NraGlzdC50eHSdl21P2zAQx9/vU5yqatoDHaGDMW0wKeRhjUjaUqcMNvHCNKZYpHZwHLHu088OCRZsyKRS39zJze/ufPe3fTAN0bdXAAdI8sVNQiQe0VJqj/L5juPsDOofpOuCHPbcLBuTux7Mi+ywdxakM7cfIWe3B5P8iWPM5f16kgEjd1Dq728Bwyty+IuiayxICX1IBcFlJdZwxFkGQ+f9WsDci1IEQRpe9GC7DWXoDHcHzv5gZ68JZV4SkeDiuVi8eIICX3lmUfg4Gs7gUvAbIoCuCi4kXK6hUh9TsJflPT5HQd+da5CxLBm7bJnz7zzPwC2vMZP0oiNuFtQ8Y9qAOfmNWSYohhnBOQSlxJJAcFtRSVXpI7boEIKLfPek7yUecnUYT1yWUDy+WuBSgsdF0YGp8zxOoppnTAvrmLJM7WzCxRIzneTADzoy49FZzTSmhRl/HGEhaAkpWVwznvNl5wKn6KyfhHWuxrJgVfPnBGKCryDkPOtIbPYvOblnGttKFbdKJbY07UMHnC7lOEhrmjFtnZPzKgtzpRUbJTcO26ExDgtxrByhEgWlTiUEjIjlGuLY64yenk/jlm08FvgUr6dqVEdKJihbbrahaNK2kfFYWwnxkD7q3o229/SnBhvLAj0lgv5RYqzkYVUxusCSctYxaT0px7OZ5hrLJhFYcKFVseSVWDxM6r8nzX5DVKQIReOXINUKLBVUctB/UR3s7n7e29+Lh86nTy9Pa5KcjdR4oLTpX+Ow928pSa20XNQF7QqdHEetxBuHDcpvKN4MquuH6qP0wbD1asUWXDSz2bVNGxlIUS1ETzy2yspSyQLd6Bzz6400loXlE6Ik7zVeFV/1eBSYrTsCR74GGssCHPEVAZ8UXHabv1bbgmmDMw6b1pGipB7viNPJHLlHTYca23oZopf4EsN3waui1VeIZdYB3ZyPQdrCjceG19dqmOZYXnGxqvUG3rx724Fd62nkNcJubAtXL4Gp4AURkj4ndJ8HzrCBquO2JJr5cH9+RunatdkXSOoudbXi/VDas1S13YKF6tkc6u+71XIL1H/UjYz8h73z0YjsWAX98qeFkdr2UTFH/sN7on1OtPkebD954Bxs1y+fv1BLAwQUAAAACAAWjhJZYJjs0ZgCAAD+BgAADQAAAHN0b2NrbWV0YS50eHSNlV1vmzAUhu/3K6womvZdm498aOkkAmSJAoTGJEp354KbWiU2NaAt+/WDbkCStYg7uPDj97znPccTf4a/vQFggjMRPro0I+Vf9Q/wmt5f93Z2sDb6Cwy1HvDIgV732AZbIJCUpLk8TgWPgAI/9sACL7zrXg9cvQDxbrHdNzYVweD7WHwXcQSM9IHwjFWnNxiqOlJGChyM2khru0bF9BfhkWQErCmJgZ1mJKPAfspZxmgKFjw8gSNdGSIEx+gVuIEt46ZvuiY2qgtMcQhJmgFTyKQhKRCq0EMQjVtkLt1FRVkyHlEJXCH3hJeiPlt2Q9PGmj6YFrRhC82Z7yqao86JlCwFAQ0fuIjF/r9Sdahoalmq/jIywLu+O6v1uSSJKXAouQczIaIzmGnoA20MdQSHo1bf3JsGJ5+KOH0qMV9ORA2RphR1alpLnZ4d1N7HIo9mMZH0vDg0GiPdLfS099Gb1THx6M+iMJlJmqbA5lTuj8BxzAY50DR1rCIIYSvSv/WdiumTo18kbl7kmPH9hf9DqOnwFkG13TK8anqAxYyddfTSvpGqwcEMQQW12Lf9UfG2VLLfgoMiwYecs5BkTPALmWNF1dTt6x0xnRW2rf5yva6TTKSQ5bClIpchvUiKNtKHulOM7+Bl3srdzYsO4+CkM2lGn6erwJYKK1gbYLVcGDVAPDLSHVAGH29qz3MeCvkvEWd2tycrwEFTQJYWuWJ/V0SHPWjVtVuUFtF+Sw7J17JLCeHHDufnVnV+Lg4UWDQRWSm9k3Lf9uv80iRlpuhwtLx1akyNZumyO3JHwHcp8qQaAOBkUScJrh3UpPLRAX5MsnshD89ZAu8+vO8gZ7sw68Epv4EvRUJltfMvCJOrkyducvX87v0BUEsBAj8ACgAAAAAAFo4SWdXcQNkaAAAAGgAAAAoAJAAAAAAAAAAgAAAAAAAAAGNmZ2FwcC50eHQKACAAAAAAAAEAGAAfVKe4ffHaATWmtb198doBAJo2jn3x2gFQSwECPwAUAAAACAAWjhJZUFk9OsMAAACLAgAADQAkAAAAAAAAACAAAABCAAAAY2ZnbWFya2V0LnR4dAoAIAAAAAAAAQAYAMO8rbh98doBkpC2vX3x2gEAmjaOffHaAVBLAQI/ABQAAAAIABaOElnmL8N3yQcAAKgVAAAHACQAAAAAAAAAIAAAADABAABlb2QudHh0CgAgAAAAAAABABgAFDuzuH3x2gHjU7e9ffHaAQCaNo598doBUEsBAj8AFAAAAAgAQ44SWfVL2bZSAAAAdgAAAAsAJAAAAAAAAAAgAAAAHgkAAGZpbHRlcnMudHh0CgAgAAAAAAABABgA1k8J6X3x2gHWTwnpffHaAQCaNo598doBUEsBAj8AFAAAAAgAFo4SWejwOpvOAwAA6gYAAAkAJAAAAAAAAAAgAAAAmQkAAG5vdGVzLnR4dAoAIAAAAAAAAQAYAGXpvbh98doBc7O4vX3x2gEAmjaOffHaAVBLAQI/ABQAAAAIABaOElkzCxh9igAAAOMAAAAJACQAAAAAAAAAIAAAAI4NAAByYXRlcy50eHQKACAAAAAAAAEAGACCGcO4ffHaAakBub198doBAJo2jn3x2gFQSwECPwAUAAAACABKjhJZhtTFrnYOAAACXAAACwAkAAAAAAAAACAAAAA/DgAAc3RhbGtlci50eHQKACAAAAAAAAEAGABjyjbxffHaAWPKNvF98doBAJo2jn3x2gFQSwECPwAUAAAACAAWjhJZJydotEEDAAAADQAADQAkAAAAAAAAACAAAADeHAAAc3RvY2toaXN0LnR4dAoAIAAAAAAAAQAYABfvzbh98doB8eu5vX3x2gEAmjaOffHaAVBLAQI/ABQAAAAIABaOEllgmOzRmAIAAP4GAAANACQAAAAAAAAAIAAAAEogAABzdG9ja21ldGEudHh0CgAgAAAAAAABABgAKEbTuH3x2gEiYbq9ffHaAQCaNo598doBUEsFBgAAAAAJAAkAQgMAAA0jAAAAAA=="
    ];
}
