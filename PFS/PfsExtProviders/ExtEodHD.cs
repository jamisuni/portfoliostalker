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
using static Pfs.Types.IExtDataProvider;

namespace Pfs.ExtProviders;

// https://eodhd.com/
public class ExtEodHD : IExtProvider, IExtDataProvider
{
    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _eodHDApiKey = "";

    public ExtEodHD(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _eodHDApiKey = key;
    }

    public int GetLastCreditPrice() { return 0; }

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
                return true;
        }
        return false;
    }

    public bool IsMarketSupport(MarketId marketId)
    {
        return MarketSupport(marketId);
    }

    public static bool MarketSupport(MarketId marketId)
    {
        switch (marketId)   // Warning! See 'GetMIC' below, thats needs hardcoding atm
        {
            case MarketId.NASDAQ:
            case MarketId.NYSE:
            case MarketId.AMEX:
            case MarketId.TSX:
            case MarketId.TSXV:
            case MarketId.OMXH:
            case MarketId.OMX:
            case MarketId.XETRA:
            case MarketId.LSE:
                return true;
        }
        return false;
    }

    const int _maxTickers = 1; // free account doesnt support batch fetching
    
    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case IExtDataProvider.ProvFunctionId.LatestEod:
            case IExtDataProvider.ProvFunctionId.HistoryEod: return _maxTickers;

            case IExtDataProvider.ProvFunctionId.Intraday: return 1;
        }
        Log.Warning($"ExtEodHD():Limit({limitID}) missing implementation");
        return 1;
    }

    /* Note! 
        * - 
        *    
        *    
        *    
        */

    public async Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_eodHDApiKey) == true)
        {
            _error = "EodHD:GetEodLatestAsync() Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        if (tickers.Count() > _maxTickers)
        {
            _error = "Failed, requesting too many tickers!";
            Log.Warning("EodHD:GetEodLatestAsync() " + _error);
            return null;
        }

        string joinedMsTickers = JoinPfsTickers(marketId, tickers);

        try
        {
            // 2025-Nov: Couldnt find request that would return latest eod w basic open/close/etc info, so getting bit extra days
            string from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7).ToString("yyyy-MM-dd"); // Just to be sure getting latest data
            string url = $"https://eodhd.com/api/eod/{joinedMsTickers}?api_token={_eodHDApiKey}&fmt=json&from={from}";

            HttpClient tempHttpClient = new HttpClient();

            HttpResponseMessage resp = await tempHttpClient.GetAsync(url);

            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _error = string.Format("Failed: {0} for [[{1}]]", resp.StatusCode.ToString(), joinedMsTickers);
                Log.Warning("EodHD:GetEodLatestAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var dailyItems = JsonSerializer.Deserialize<List<EodHDDaily>>(content);

            if (dailyItems == null || dailyItems.Count() == 0)
            {
                _error = string.Format("Failed, Empty data: {0} for [[{1}]]", resp.StatusCode.ToString(), joinedMsTickers);
                Log.Warning("EodHD:GetEodLatestAsync() " + _error);
                return null;
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            ret[tickers[0]] = new FullEOD()
            {
                Date = DateOnly.Parse(dailyItems.Last().date),
                Close = dailyItems.Last().close,
                High = dailyItems.Last().high,
                Low = dailyItems.Last().low,
                Open = dailyItems.Last().open,
                PrevClose = -1,
                Volume = dailyItems.Last().volume,
            };

            return ret;
        }
        catch (Exception e)
        {
            _error = string.Format("Failed, connection exception {0} for [[{1}]]", e.Message, joinedMsTickers);
            Log.Warning("EodHD:GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        return null;
    }

    static public string TrimToPfsTicker(MarketId marketId, string marketstackTicker)
    {
        return marketstackTicker.Replace("." + GetEodHDMarketId(marketId), "");
    }

    static public string ExpandToMsTicker(MarketId marketId, string pfsTicker)
    {
        return string.Format("{0}.{1}", pfsTicker, GetEodHDMarketId(marketId));
    }

    static public string GetEodHDMarketId(MarketId marketId)
    {
        switch ( marketId )
        {
            case MarketId.NASDAQ:
                return "US";
            case MarketId.NYSE:
                return "US";
            case MarketId.AMEX:
                return "US";
            case MarketId.LSE:
                return "LSE";
            case MarketId.TSX:
                return "TO";
            case MarketId.OMXH:
                return "HE";
            case MarketId.OMX:
                return "ST";
            case MarketId.XETRA:
                return "XETRA";
            case MarketId.TSXV:
                return "V";
        }
        return null;
    }

    static public string JoinPfsTickers(MarketId marketId, List<string> pfsTickers)
    {
        return string.Join(',', pfsTickers.ConvertAll<string>(s => ExpandToMsTicker(marketId, s)));
    }

    // !!!REMEMBER!!! JSON!!! https://json2csharp.com/json-to-csharp

    public class EodHDDaily
    {
        public string date { get; set; }
        public decimal open { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public decimal close { get; set; }
        public decimal adjusted_close { get; set; }
        public int volume { get; set; }
    }

#if false // https://eodhd.com/api/exchanges-list/?api_token=MYKEY&fmt=json

[
    {
        "Name": "USA Stocks",
        "Code": "US",
        "OperatingMIC": "XNAS, XNYS, OTCM",
        "Country": "USA",
        "Currency": "USD",
        "CountryISO2": "US",
        "CountryISO3": "USA"
    },
    {
        "Name": "London Exchange",
        "Code": "LSE",
        "OperatingMIC": "XLON",
        "Country": "UK",
        "Currency": "GBP",
        "CountryISO2": "GB",
        "CountryISO3": "GBR"
    },
    {
        "Name": "Toronto Exchange",
        "Code": "TO",
        "OperatingMIC": "XTSE",
        "Country": "Canada",
        "Currency": "CAD",
        "CountryISO2": "CA",
        "CountryISO3": "CAN"
    },
    {
        "Name": "NEO Exchange",
        "Code": "NEO",
        "OperatingMIC": "NEOE",
        "Country": "Canada",
        "Currency": "CAD",
        "CountryISO2": "CA",
        "CountryISO3": "CAN"
    },
    {
        "Name": "TSX Venture Exchange",
        "Code": "V",
        "OperatingMIC": "XTSX",
        "Country": "Canada",
        "Currency": "CAD",
        "CountryISO2": "CA",
        "CountryISO3": "CAN"
    },
    {
        "Name": "Berlin Exchange",
        "Code": "BE",
        "OperatingMIC": "XBER",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Hamburg Exchange",
        "Code": "HM",
        "OperatingMIC": "XHAM",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "XETRA Stock Exchange",
        "Code": "XETRA",
        "OperatingMIC": "XETR",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Dusseldorf Exchange",
        "Code": "DU",
        "OperatingMIC": "XDUS",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Hanover Exchange",
        "Code": "HA",
        "OperatingMIC": "XHAN",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Munich Exchange",
        "Code": "MU",
        "OperatingMIC": "XMUN",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Stuttgart Exchange",
        "Code": "STU",
        "OperatingMIC": "XSTU",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Frankfurt Exchange",
        "Code": "F",
        "OperatingMIC": "XFRA",
        "Country": "Germany",
        "Currency": "EUR",
        "CountryISO2": "DE",
        "CountryISO3": "DEU"
    },
    {
        "Name": "Luxembourg Stock Exchange",
        "Code": "LU",
        "OperatingMIC": "XLUX",
        "Country": "Luxembourg",
        "Currency": "EUR",
        "CountryISO2": "LU",
        "CountryISO3": "LUX"
    },
    {
        "Name": "Vienna Exchange",
        "Code": "VI",
        "OperatingMIC": "XWBO",
        "Country": "Austria",
        "Currency": "EUR",
        "CountryISO2": "AT",
        "CountryISO3": "AUT"
    },
    {
        "Name": "Euronext Paris",
        "Code": "PA",
        "OperatingMIC": "XPAR",
        "Country": "France",
        "Currency": "EUR",
        "CountryISO2": "FR",
        "CountryISO3": "FRA"
    },
    {
        "Name": "Euronext Brussels",
        "Code": "BR",
        "OperatingMIC": "XBRU",
        "Country": "Belgium",
        "Currency": "EUR",
        "CountryISO2": "BE",
        "CountryISO3": "BEL"
    },
    {
        "Name": "Madrid Exchange",
        "Code": "MC",
        "OperatingMIC": "BMEX",
        "Country": "Spain",
        "Currency": "EUR",
        "CountryISO2": "ES",
        "CountryISO3": "ESP"
    },
    {
        "Name": "SIX Swiss Exchange",
        "Code": "SW",
        "OperatingMIC": "XSWX",
        "Country": "Switzerland",
        "Currency": "CHF",
        "CountryISO2": "CH",
        "CountryISO3": "CHE"
    },
    {
        "Name": "Euronext Lisbon",
        "Code": "LS",
        "OperatingMIC": "XLIS",
        "Country": "Portugal",
        "Currency": "EUR",
        "CountryISO2": "PT",
        "CountryISO3": "PRT"
    },
    {
        "Name": "Euronext Amsterdam",
        "Code": "AS",
        "OperatingMIC": "XAMS",
        "Country": "Netherlands",
        "Currency": "EUR",
        "CountryISO2": "NL",
        "CountryISO3": "NLD"
    },
    {
        "Name": "Iceland Exchange",
        "Code": "IC",
        "OperatingMIC": "XICE",
        "Country": "Iceland",
        "Currency": "ISK",
        "CountryISO2": "IS",
        "CountryISO3": "ISL"
    },
    {
        "Name": "Irish Exchange",
        "Code": "IR",
        "OperatingMIC": "XDUB",
        "Country": "Ireland",
        "Currency": "EUR",
        "CountryISO2": "IE",
        "CountryISO3": "IRL"
    },
    {
        "Name": "Helsinki Exchange",
        "Code": "HE",
        "OperatingMIC": "XHEL",
        "Country": "Finland",
        "Currency": "EUR",
        "CountryISO2": "FI",
        "CountryISO3": "FIN"
    },
    {
        "Name": "Oslo Stock Exchange",
        "Code": "OL",
        "OperatingMIC": "XOSL",
        "Country": "Norway",
        "Currency": "NOK",
        "CountryISO2": "NO",
        "CountryISO3": "NOR"
    },
    {
        "Name": "Copenhagen Exchange",
        "Code": "CO",
        "OperatingMIC": "XCSE",
        "Country": "Denmark",
        "Currency": "DKK",
        "CountryISO2": "DK",
        "CountryISO3": "DNK"
    },
    {
        "Name": "Stockholm Exchange",
        "Code": "ST",
        "OperatingMIC": "XSTO",
        "Country": "Sweden",
        "Currency": "SEK",
        "CountryISO2": "SE",
        "CountryISO3": "SWE"
    },
    {
        "Name": "Victoria Falls Stock Exchange",
        "Code": "VFEX",
        "OperatingMIC": "VFEX",
        "Country": "Zimbabwe",
        "Currency": "ZWL",
        "CountryISO2": "ZW",
        "CountryISO3": "ZWE"
    },
    {
        "Name": "Zimbabwe Stock Exchange",
        "Code": "XZIM",
        "OperatingMIC": "XZIM",
        "Country": "Zimbabwe",
        "Currency": "ZWL",
        "CountryISO2": "ZW",
        "CountryISO3": "ZWE"
    },
    {
        "Name": "Lusaka Stock Exchange",
        "Code": "LUSE",
        "OperatingMIC": "XLUS",
        "Country": "Zambia",
        "Currency": "ZMW",
        "CountryISO2": "ZM",
        "CountryISO3": "ZMB"
    },
    {
        "Name": "Uganda Securities Exchange",
        "Code": "USE",
        "OperatingMIC": "XUGA",
        "Country": "Uganda",
        "Currency": "UGX",
        "CountryISO2": "UG",
        "CountryISO3": "UGA"
    },
    {
        "Name": "Dar es Salaam Stock Exchange",
        "Code": "DSE",
        "OperatingMIC": "XDAR",
        "Country": "Tanzania",
        "Currency": "TZS",
        "CountryISO2": "TZ",
        "CountryISO3": "TZA"
    },
    {
        "Name": "Rwanda Stock Exchange",
        "Code": "RSE",
        "OperatingMIC": "RSEX",
        "Country": "Rwanda",
        "Currency": "RWF ",
        "CountryISO2": "RW",
        "CountryISO3": "RWA"
    },
    {
        "Name": "Prague Stock Exchange",
        "Code": "PR",
        "OperatingMIC": "XPRA",
        "Country": "Czech Republic",
        "Currency": "CZK",
        "CountryISO2": "CZ",
        "CountryISO3": "CZE"
    },
    {
        "Name": "Botswana Stock Exchange",
        "Code": "XBOT",
        "OperatingMIC": "XBOT",
        "Country": "Botswana",
        "Currency": "BWP",
        "CountryISO2": "BW",
        "CountryISO3": "BWA"
    },
    {
        "Name": "Nigerian Stock Exchange",
        "Code": "XNSA",
        "OperatingMIC": "XNSA",
        "Country": "Nigeria",
        "Currency": "NGN",
        "CountryISO2": "NG",
        "CountryISO3": "NGA"
    },
    {
        "Name": "Egyptian Exchange",
        "Code": "EGX",
        "OperatingMIC": "NILX",
        "Country": "Egypt",
        "Currency": "EGP",
        "CountryISO2": "EG",
        "CountryISO3": "EGY"
    },
    {
        "Name": "Malawi Stock Exchange",
        "Code": "MSE",
        "OperatingMIC": "XMSW",
        "Country": "Malawi",
        "Currency": "MWK",
        "CountryISO2": "MW",
        "CountryISO3": "MWI"
    },
    {
        "Name": "Ghana Stock Exchange",
        "Code": "GSE",
        "OperatingMIC": "XGHA",
        "Country": "Ghana",
        "Currency": "GHS",
        "CountryISO2": "GH",
        "CountryISO3": "GHA"
    },
    {
        "Name": "Nairobi Securities Exchange",
        "Code": "XNAI",
        "OperatingMIC": "XNAI",
        "Country": "Kenya",
        "Currency": "KES",
        "CountryISO2": "KE",
        "CountryISO3": "KEN"
    },
    {
        "Name": "Casablanca Stock Exchange",
        "Code": "BC",
        "OperatingMIC": "XCAS",
        "Country": "Morocco",
        "Currency": "MAD",
        "CountryISO2": "MA",
        "CountryISO3": "MAR"
    },
    {
        "Name": "Stock Exchange of Mauritius",
        "Code": "SEM",
        "OperatingMIC": "XMAU",
        "Country": "Mauritius",
        "Currency": "MUR",
        "CountryISO2": "MU",
        "CountryISO3": "MUS"
    },
    {
        "Name": "Tel Aviv Stock Exchange",
        "Code": "TA",
        "OperatingMIC": "XTAE",
        "Country": "Israel",
        "Currency": "ILS",
        "CountryISO2": "IL",
        "CountryISO3": "ISR"
    },
    {
        "Name": "Korea Stock Exchange",
        "Code": "KO",
        "OperatingMIC": "XKRX",
        "Country": "Korea",
        "Currency": "KRW",
        "CountryISO2": "KR",
        "CountryISO3": "KOR"
    },
    {
        "Name": "KOSDAQ",
        "Code": "KQ",
        "OperatingMIC": "XKOS",
        "Country": "Korea",
        "Currency": "KRW",
        "CountryISO2": "KR",
        "CountryISO3": "KOR"
    },
    {
        "Name": "Budapest Stock Exchange",
        "Code": "BUD",
        "OperatingMIC": "XBUD",
        "Country": "Hungary",
        "Currency": "HUF",
        "CountryISO2": "HU",
        "CountryISO3": "HUN"
    },
    {
        "Name": "Warsaw Stock Exchange",
        "Code": "WAR",
        "OperatingMIC": "XWAR",
        "Country": "Poland",
        "Currency": "PLN",
        "CountryISO2": "PL",
        "CountryISO3": "POL"
    },
    {
        "Name": "Philippine Stock Exchange",
        "Code": "PSE",
        "OperatingMIC": "XPHS",
        "Country": "Philippines",
        "Currency": "PHP",
        "CountryISO2": "PH",
        "CountryISO3": "PHL"
    },
    {
        "Name": "Shanghai Stock Exchange",
        "Code": "SHG",
        "OperatingMIC": "XSHG",
        "Country": "China",
        "Currency": "CNY",
        "CountryISO2": "CN",
        "CountryISO3": "CHN"
    },
    {
        "Name": "Jakarta Exchange",
        "Code": "JK",
        "OperatingMIC": "XIDX",
        "Country": "Indonesia",
        "Currency": "IDR",
        "CountryISO2": "ID",
        "CountryISO3": "IDN"
    },
    {
        "Name": "National Stock Exchange of India",
        "Code": "NSE",
        "OperatingMIC": "XNSE",
        "Country": "India",
        "Currency": "INR",
        "CountryISO2": "IN",
        "CountryISO3": "IND"
    },
    {
        "Name": "Athens Exchange",
        "Code": "AT",
        "OperatingMIC": "ASEX",
        "Country": "Greece",
        "Currency": "EUR",
        "CountryISO2": "GR",
        "CountryISO3": "GRC"
    },
    {
        "Name": "Shenzhen Stock Exchange",
        "Code": "SHE",
        "OperatingMIC": "XSHE",
        "Country": "China",
        "Currency": "CNY",
        "CountryISO2": "CN",
        "CountryISO3": "CHN"
    },
    {
        "Name": "Australian Securities Exchange",
        "Code": "AU",
        "OperatingMIC": "XASX",
        "Country": "Australia",
        "Currency": "AUD",
        "CountryISO2": "AU",
        "CountryISO3": "AUS"
    },
    {
        "Name": "Chilean Stock Exchange",
        "Code": "SN",
        "OperatingMIC": "XSGO",
        "Country": "Chile",
        "Currency": "CLP",
        "CountryISO2": "CL",
        "CountryISO3": "CHL"
    },
    {
        "Name": "Johannesburg Exchange",
        "Code": "JSE",
        "OperatingMIC": "XJSE",
        "Country": "South Africa",
        "Currency": "ZAR",
        "CountryISO2": "ZA",
        "CountryISO3": "ZAF"
    },
    {
        "Name": "Karachi Stock Exchange",
        "Code": "KAR",
        "OperatingMIC": "XKAR",
        "Country": "Pakistan",
        "Currency": "PKR",
        "CountryISO2": "PK",
        "CountryISO3": "PAK"
    },
    {
        "Name": "Thailand Exchange",
        "Code": "BK",
        "OperatingMIC": "XBKK",
        "Country": "Thailand",
        "Currency": "THB",
        "CountryISO2": "TH",
        "CountryISO3": "THA"
    },
    {
        "Name": "Colombo Stock Exchange",
        "Code": "CM",
        "OperatingMIC": "XCOL",
        "Country": "Sri Lanka",
        "Currency": "LKR",
        "CountryISO2": "LK",
        "CountryISO3": "LKA"
    },
    {
        "Name": "Vietnam Stocks",
        "Code": "VN",
        "OperatingMIC": "HSTC",
        "Country": "Vietnam",
        "Currency": "VND",
        "CountryISO2": "VN",
        "CountryISO3": "VNM"
    },
    {
        "Name": "Kuala Lumpur Exchange",
        "Code": "KLSE",
        "OperatingMIC": "XKLS",
        "Country": "Malaysia",
        "Currency": "MYR",
        "CountryISO2": "MY",
        "CountryISO3": "MYS"
    },
    {
        "Name": "Bucharest Stock Exchange",
        "Code": "RO",
        "OperatingMIC": "XBSE",
        "Country": "Romania",
        "Currency": "RON",
        "CountryISO2": "RO",
        "CountryISO3": "ROU"
    },
    {
        "Name": "Buenos Aires Exchange",
        "Code": "BA",
        "OperatingMIC": "XBUE",
        "Country": "Argentina",
        "Currency": "ARS",
        "CountryISO2": "AR",
        "CountryISO3": "ARG"
    },
    {
        "Name": "Sao Paulo Exchange",
        "Code": "SA",
        "OperatingMIC": "BVMF",
        "Country": "Brazil",
        "Currency": "BRL",
        "CountryISO2": "BR",
        "CountryISO3": "BRA"
    },
    {
        "Name": "Mexican Exchange",
        "Code": "MX",
        "OperatingMIC": "XMEX",
        "Country": "Mexico",
        "Currency": "MXN",
        "CountryISO2": "MX",
        "CountryISO3": "MEX"
    },
    {
        "Name": "London IL",
        "Code": "IL",
        "OperatingMIC": "XLON",
        "Country": "UK",
        "Currency": "USD",
        "CountryISO2": "GB",
        "CountryISO3": "GBR"
    },
    {
        "Name": "Zagreb Stock Exchange",
        "Code": "ZSE",
        "OperatingMIC": "XZAG",
        "Country": "Croatia",
        "Currency": "EUR",
        "CountryISO2": "HR",
        "CountryISO3": "HRV"
    },
    {
        "Name": "Taiwan Stock Exchange",
        "Code": "TW",
        "OperatingMIC": "XTAI",
        "Country": "Taiwan",
        "Currency": "TWD",
        "CountryISO2": "TW",
        "CountryISO3": "TWN"
    },
    {
        "Name": "Taiwan OTC Exchange",
        "Code": "TWO",
        "OperatingMIC": "ROCO",
        "Country": "Taiwan",
        "Currency": "TWD",
        "CountryISO2": "TW",
        "CountryISO3": "TWN"
    },
    {
        "Name": "Bolsa de Valores de Lima",
        "Code": "LIM",
        "OperatingMIC": "XLIM",
        "Country": "Peru",
        "Currency": "PEN",
        "CountryISO2": "PE",
        "CountryISO3": "PER"
    },
    {
        "Name": "Government Bonds",
        "Code": "GBOND",
        "OperatingMIC": null,
        "Country": "Unknown",
        "Currency": "Unknown",
        "CountryISO2": "",
        "CountryISO3": ""
    },
    {
        "Name": "Money Market Virtual Exchange",
        "Code": "MONEY",
        "OperatingMIC": null,
        "Country": "Unknown",
        "Currency": "Unknown",
        "CountryISO2": "",
        "CountryISO3": ""
    },
    {
        "Name": "Europe Fund Virtual Exchange",
        "Code": "EUFUND",
        "OperatingMIC": null,
        "Country": "Unknown",
        "Currency": "EUR",
        "CountryISO2": "",
        "CountryISO3": ""
    },
    {
        "Name": "Cryptocurrencies",
        "Code": "CC",
        "OperatingMIC": "CRYP",
        "Country": "Unknown",
        "Currency": "USD",
        "CountryISO2": "",
        "CountryISO3": ""
    },
    {
        "Name": "FOREX",
        "Code": "FOREX",
        "OperatingMIC": "CDSL",
        "Country": "Unknown",
        "Currency": "Unknown",
        "CountryISO2": "",
        "CountryISO3": ""
    }
]
#endif
}
