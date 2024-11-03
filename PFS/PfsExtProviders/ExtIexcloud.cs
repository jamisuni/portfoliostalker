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

using Serilog;

using Pfs.Types;
using static Pfs.Types.IExtDataProvider;
using CsvHelper;
using System.Globalization;
using System.Runtime.Serialization;

namespace Pfs.ExtProviders;

// (https://iexcloud.io/) Very extensive API, covering hefty amount of different use cases
public class ExtIexcloud : IExtProvider, IExtDataProvider // => shut down on August 31, 2024
{
    protected readonly IPfsStatus _pfsStatus;

    protected string _error = string.Empty;

    protected string _iexcloudApiKey = "";
    
    //SANDBOX: protected string _iexcloudUrl = $"https://sandbox.iexapis.com/stable/";
    protected string _iexcloudUrl = $"https://cloud.iexapis.com/stable/";

    /* Provided functionalities: 
     * 
     * - They API looks very professional, .... 
     * 
     * 2024-Apr: Havent never really test this well.. and looks like they did major rework somewhere as 
     *           cant even find from documentation anymore those functions I used sometimes (below). 
     *           Disabled now as looks like they dont have any kind of freebie account mode anymore to 
     *           test and really looks focused to more areas of market info I dont need...
     *           => Lets see someday when has extra time if has energy to reread all their over
     *              confusing documentation.. but absolute not priority atm
     *           => postponed! to someday far far on future... sad as maybe best market coverage of all!
     */

    public ExtIexcloud(IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;
    }

    public string GetLastError() { return _error; }

    public void SetPrivateKey(string key)
    {
        _iexcloudApiKey = key;
    }

    public int GetLastCreditPrice() { return 0; }

    public bool IsSupport(ProvFunctionId funcId)
    {
        switch (funcId)
        {
            case ProvFunctionId.LatestEod:
                return true;

            case ProvFunctionId.Intraday:
                return true;

            case ProvFunctionId.HistoryEod:
                return false; // too hefty credit consumption
        }
        return false;
    }

    public bool IsMarketSupport(MarketId marketId)
    {
        return MarketSupport(marketId);
    }

    public static bool MarketSupport(MarketId marketId)
    {
        switch (marketId)
        {
            case MarketId.NASDAQ:
            case MarketId.NYSE:
            case MarketId.AMEX:
            case MarketId.TSX:
            case MarketId.XETRA:
            case MarketId.OMXH:
            case MarketId.OMX:
            case MarketId.LSE:
                return true;
        }
        return false;
    }

    public int GetBatchSizeLimit(ProvFunctionId limitID)
    {
        switch (limitID)
        {
            case ProvFunctionId.LatestEod: return _maxTickers;
            case ProvFunctionId.HistoryEod: return 1;
            case ProvFunctionId.Intraday: return _maxTickers;
        }
        Log.Warning($"ExtMarketDataIexcloud():Limit({limitID}) missing implementation");
        return 1;
    }

    const int _maxTickers = 100;

    public async Task<Dictionary<string, FullEOD>> GetIntradayAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_iexcloudApiKey) == true)
        {
            _error = "iexcloud::GetIntradayAsync() Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        string iexcloudJoinedTickers = JoinPfsTickers(marketId, tickers);

        if (string.IsNullOrEmpty(iexcloudJoinedTickers) == true)
        {
            _error = "Failed, too many tickers!";
            Log.Warning("iexcloud::GetIntradayAsync() " + _error);
            return null;
        }

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp = await tempHttpClient.GetAsync(_iexcloudUrl + $"tops?symbols={iexcloudJoinedTickers}&token={_iexcloudApiKey}&format=csv");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"iexcloud failed: {resp.StatusCode} for [[{iexcloudJoinedTickers}]]";
                Log.Warning("iexcloud::GetIntradayAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var dailyItems = csv.GetRecords<IexcloudIntradayFormat>().ToList();

            if (dailyItems == null || dailyItems.Count() == 0)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{iexcloudJoinedTickers}]]";
                Log.Warning("iexcloud::GetIntradayAsync() " + _error);
                return null;
            }
            else if (dailyItems.Count() < amountOfReqTickers)
            {
                _error = $"Warning, requested {amountOfReqTickers} got just {dailyItems.Count()} for [[{iexcloudJoinedTickers}]]";
                Log.Warning("iexcloud::GetIntradayAsync() " + _error);

                // This is just warning, still got data so going to go processing it...
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            foreach (var item in dailyItems)
            {
                string pfsTicker = TrimToPfsTicker(marketId, item.symbol);

                ret[pfsTicker] = new FullEOD()
                {
                    Date = DateOnly.FromDateTime(DateTime.UnixEpoch.AddMilliseconds(item.lastSaleTime)),
                    Close = item.lastSalePrice,
                    High = item.lastSalePrice,
                    Low = item.lastSalePrice,
                    Open = item.lastSalePrice,
                    PrevClose = -1,
                    Volume = (int)(item.volume),
                };
            }

            return ret;
        }
        catch (Exception e)
        {
            _error = $"Failed! Connection exception {e.Message} for [[{iexcloudJoinedTickers}]]";
            Log.Warning("iexcloud::GetIntradayAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, FullEOD>> GetEodLatestAsync(MarketId marketId, List<string> tickers)
    {
        _error = string.Empty;

        if (string.IsNullOrEmpty(_iexcloudApiKey) == true)
        {
            _error = "iexcloud::GetEodLatestAsync() Missing private access key!";
            return null;
        }

        int amountOfReqTickers = tickers.Count();

        string iexcloudJoinedTickers = JoinPfsTickers(marketId, tickers);

        if (string.IsNullOrEmpty(iexcloudJoinedTickers) == true)
        {
            _error = "Failed, too many tickers!";
            Log.Warning("iexcloud::GetEodLatestAsync() " + _error);
            return null;
        }

        try
        {
            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp = await tempHttpClient.GetAsync(_iexcloudUrl + $"stock/market/previous?symbols={iexcloudJoinedTickers}&token={_iexcloudApiKey}&format=csv");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"iexcloud failed: {resp.StatusCode} for [[{iexcloudJoinedTickers}]]";
                Log.Warning("iexcloud::GetEodLatestAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var dailyItems = csv.GetRecords<IexcloudDailyFormat>().ToList();

            if (dailyItems == null || dailyItems.Count() == 0)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{iexcloudJoinedTickers}]]";
                Log.Warning("iexcloud::GetEodLatestAsync() " + _error);
                return null;
            }
            else if (dailyItems.Count() < amountOfReqTickers)
            {
                _error = $"Warning, requested {amountOfReqTickers} got just {dailyItems.Count()} for [[{iexcloudJoinedTickers}]]";
                Log.Warning("iexcloud::GetEodLatestAsync() " + _error);

                // This is just warning, still got data so going to go processing it...
            }

            // All seams to be enough well so lets convert data to PFS format

            Dictionary<string, FullEOD> ret = new Dictionary<string, FullEOD>();

            foreach (var item in dailyItems)
            {
                string pfsTicker = TrimToPfsTicker(marketId, item.symbol);

                ret[pfsTicker] = new FullEOD()
                {
                    Date = DateOnly.FromDateTime(new DateTime(item.date.Year, item.date.Month, item.date.Day)),
                    Close = item.close,
                    High = item.high,
                    Low = item.low,
                    Open = item.open,
                    PrevClose = -1,
                    Volume = (int)(item.volume),
                };
            }

            return ret;
        }
        catch ( Exception e )
        {
            _error = $"Failed! Connection exception {e.Message} for [[{iexcloudJoinedTickers}]]";
            Log.Warning("iexcloud::GetEodLatestAsync() " + _error);
        }
        return null;
    }

    public async Task<Dictionary<string, List<FullEOD>>> GetEodHistoryAsync(MarketId marketId, List<string> tickers, DateTime startDay, DateTime endDay)
    {
        // https://intercom.help/iexcloud/en/articles/4063720-historical-stock-prices-on-iex-cloud
        // - Free plans can access up to five years of historical data, while access to the full 15 years is included with paid plans. 

        _error = string.Empty;

        if (string.IsNullOrEmpty(_iexcloudApiKey) == true)
        {
            _error = "iexcloud::GetEodHistoryAsync() Missing private access key!";
            return null;
        }

        if (tickers.Count != 1)
        {
            _error = "Failed, limiting history for 1 ticker on time!";
            Log.Warning("iexcloud::GetEodLatestAsync() " + _error);
            return null;
        }

        string ticker = ExpandToIexcloudTicker(marketId, tickers[0]);

        string start = string.Empty; 

        if ( startDay.AddDays(14) >= DateTime.UtcNow )
            // If max 2 weeks requested then we just get that.. otherwise go max.. => As of 2021-Oct-20 returns still full year (not sure from cost)
            start = startDay.ToString("yyyyMMdd");

        try
        {
            // "Filter Results" allows to light up download size, by limiting responses fields to only interesting ones
            // string fields = "symbol,date,open,high,low,close,volume"; <== As of 2021-Oct-20 doesnt work w CSV, returns always as JSON! So bah...

            HttpClient tempHttpClient = new HttpClient();
            HttpResponseMessage resp;
            
            if ( start == string.Empty )
                resp = await tempHttpClient.GetAsync(_iexcloudUrl + $"stock/{ticker}/chart/max?token={_iexcloudApiKey}&format=csv");
            else
                // Note! if less than few weeks requested then well get w exact date... still seams to return everything for that year...
                resp = await tempHttpClient.GetAsync(_iexcloudUrl + $"stock/{ticker}/chart/{start}?token={_iexcloudApiKey}&chartByDay=true&format=csv");

            if (resp.IsSuccessStatusCode == false)
            {
                _error = $"Failed: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("iexcloud:GetEodHistoryAsync() " + _error);
                return null;
            }

            string content = await resp.Content.ReadAsStringAsync();
            var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            var historyResp = csv.GetRecords<IexcloudHistoryFormat>().ToList();

            if (historyResp == null || historyResp.Count() == 0)
            {
                _error = $"Failed, empty data: {resp.StatusCode} for [[{tickers[0]}]]";
                Log.Warning("iexcloud::GetEodHistoryAsync() " + _error);
                return null;
            }

            Dictionary<string, List<FullEOD>> ret = new Dictionary<string, List<FullEOD>>();

            List<FullEOD> ext = historyResp.ConvertAll(s => new FullEOD()
            {
                Date = DateOnly.FromDateTime(s.date),
                Close = s.close,
                High = s.high,
                Low = s.low,
                Open = s.open,
                PrevClose = -1,
                Volume = (int)(s.volume),
            });

            //ext.Reverse();
            ret.Add(tickers[0], ext);

            return ret;
        }
        catch (Exception e)
        {
            _error = $"Failed, connection exception {e.Message} for [[{tickers[0]}]]";
            Log.Warning("iexcloud::GetEodHistoryAsync() " + _error);
        }
        return null;
    }

    /*
    symbol,sector,securityType,bidPrice,bidSize,askPrice,askSize,lastUpdated,lastSalePrice,lastSaleSize,lastSaleTime,volume,seq
    AAPL, leyclcecthniooregnot, sc,0,0,0,0,1665606476796,149.867,2,1635163848969,917546,

    symbol,sector,securityType,bidPrice,bidSize,askPrice,askSize,lastUpdated,lastSalePrice,lastSaleSize,lastSaleTime,volume,seq
    T,communications,cs,0,0,0,0,1638825907417,23.28,10,1638824550980,1244004,
    */
    [DataContract]
    private class IexcloudIntradayFormat
    {
        [DataMember] public string symbol { get; set; }

        [DataMember] public decimal lastSalePrice { get; set; }

        [DataMember] public long lastSaleTime { get; set; }

        [DataMember] public decimal volume { get; set; }
    }

    /*
    https://sandbox.iexapis.com/stable/stock/market/previous?symbols=aapl,fb,tsla&token=Tpk_9c096b80157b417684b1a15a35eb2bb4&format=csv

    close,high,low,open,symbol,volume,id,key,subkey,date,updated,changeOverTime,marketChangeOverTime,
    uOpen,uClose,uHigh,uLow,uVolume,fOpen,fClose,fHigh,fLow,fVolume,label,change,changePercent
    155.97,155.97,148.34,150.68,AAPL,78341972,HEA_RTORISPIISLCC,LAPA,,2021-10-19,1667966223872,0.015546818102904933,0.015667598642630603,153.26,150.46,151.35,149.14,77635202,149.03,150.96,156.54,149.71,77718659,"Oct 19, 21",2.219557532610653,0.0155
    354.27,354.27,342.91,343.828,FB,18933588,ITRHIEARCPIOCSS_L,BF,,2021-10-19,1636000042281,0.01397385310881877,0.01406054593632582,356.022,348.92,346.22,341.49,18814089,353.954,345.6,351.66,352.51,19696098,"Oct 19, 21",4.850844536959803,0.0143
    890.49,916.32,890.49,916.32,TSLA,17702681,CP_IHELSIRIOCTSRA,STAL,,2021-10-19,1663202878895,-0.006750885396396326,-0.006902778709568532,913.7,905.38,890.98,883.82,17791105,917.38,867.36,914.43,895.7,17517969,"Oct 19, 21",-6.089323165380029,-0.0069
    */

    [DataContract]
    private class IexcloudDailyFormat
    {
        [DataMember]
        public string symbol { get; set; }

        [DataMember]
        public DateTime date { get; set; }

        [DataMember]
        public decimal open { get; set; }

        [DataMember]
        public decimal high { get; set; }

        [DataMember]
        public decimal low { get; set; }

        [DataMember]
        public decimal close { get; set; }

        [DataMember]
        public decimal volume { get; set; }
    }

    [DataContract]
    private class IexcloudHistoryFormat
    {
        [DataMember]
        public string symbol { get; set; }

        [DataMember]
        public DateTime date { get; set; }

        [DataMember]
        public decimal open { get; set; }

        [DataMember]
        public decimal high { get; set; }

        [DataMember]
        public decimal low { get; set; }

        [DataMember]
        public decimal close { get; set; } // adjusted for splits and that is the one you will want to use. (uClose would be unadjusted one)

        [DataMember]
        public decimal volume { get; set; }
    }

    static public string TrimToPfsTicker(MarketId marketID, string iexcloudTicker)
    {
        string iexcloudTickerEnding = GetTickerEnding(marketID);

        if (string.IsNullOrWhiteSpace(iexcloudTickerEnding) == false && iexcloudTicker.EndsWith(iexcloudTickerEnding) == true)
            return iexcloudTicker.Substring(0, iexcloudTicker.Length - iexcloudTickerEnding.Length);

        return iexcloudTicker;
    }

    static public string ExpandToIexcloudTicker(MarketId marketID, string pfsTicker)
    {
        string iexcloudTickerEnding = GetTickerEnding(marketID);

        if (string.IsNullOrWhiteSpace(iexcloudTickerEnding) == false && pfsTicker.EndsWith(iexcloudTickerEnding) == false)
            return pfsTicker + iexcloudTickerEnding;

        return pfsTicker;
    }

    static public string JoinPfsTickers(MarketId marketID, List<string> pfsTickers)
    {
        if (pfsTickers.Count > _maxTickers)
            // Coding error, should have divided this task to multiple parts
            return string.Empty;

        return string.Join(',', pfsTickers.ConvertAll<string>(s => ExpandToIexcloudTicker(marketID, s)));
    }

    static protected string GetTickerEnding(MarketId marketID)
    {
        switch (marketID)
        {
            case MarketId.TSX: return "-CT";
            case MarketId.XETRA: return "-GY";
            case MarketId.OMXH: return "-FH";
            case MarketId.OMX: return "-SS";
            case MarketId.LSE: return "-LN";

            default: // Not supported atm
                return "";
        }
    }
}

#if false // last fetch done 2021-Oct-20 https://cloud.iexapis.com/stable/ref-data/exchanges?token=ADDTOKENHERE&format=csv

exchange,region,description,mic,exchangeSuffix
ETR,DE,Xetra,XETR,-GY
HEL,FI,Nasdaq Helsinki Ltd,XHEL,-FH
OME,SE,Nasdaq Stockholm Ab,XSTO,-SS
TSE,CA,Toronto Stock Exchange,XTSE,-CT

TSX,CA,Tsx Venture Exchange,XTSX,-CV            <= Maybe this would be TSXV
BSEX,AZ,Baku Stock Exchange,BSEX,-BSEX
DUB,IE,Euronext Dublin,XDUB,-ID
HKHKSG,HK,Stock Exchange Of Hong Kong Limited Shanghai Hong Kong S,SHSC,-H1
CAR,VE,Caracas Stock Exchange,BVCA,-VS
COL,LK,Colombo Stock Exchange,XCOL,-SL
ESMSE,ES,Bolsa De Madrid,XMAD,-SN
DFM,AE,Dubai Financial Market,XDFM,-DB
SAT,SE,Spotlight Stock Market,XSAT,-KA
BSP,BR,Bm Fbovespa S A Bolsa De Valores Mercadorias E Futuros,BVMF,-BS
NWTC,NO,Norwegian Over The Counter Market,NOTC,-NS
BRV,CI,Bourse Regionale Des Valeurs Mobilieres,XBRV,-BC
WBO,AT,Wiener Boerse Ag Dritter Markt Third Market,XWBO,-AV
USAMEX,US,Nyse Mkt Llc,XASE,-UA
GHA,GH,Ghana Stock Exchange,XGHA,-GN
MSW,MW,Malawi Stock Exchange,XMSW,-MW
MAL,MT,Malta Stock Exchange,XMAL,-MV
CSX,KH,Cambodia Securities Exchange,XCSX,-KH
DHA,BD,Dhaka Stock Exchange Ltd,XDHA,-BD
BAH,BH,Bahrain Bourse,XBAH,-BI
BDA,BM,Bermuda Stock Exchange Ltd,XBDA,-BH
KUW,KW,Kuwait Stock Exchange,XKUW,-KK
BUL,BG,Bulgarian Stock Exchange,XBUL,-BU
CLBEC,CL,La Bolsa Electronica De Chile,XBCL,-CE
BVC,CV,Cape Verde Stock Exchange,XBVC,-VR
STU,DE,Boerse Stuttgart,XSTU,-GS
HKHKSZ,HK,Stock Exchange Of Hong Kong Limited Shenzhen Hong Kong S,SZSC,-H2
RSEX,RW,Rwanda Stock Exchange,RSEX,-RW
PAE,PS,Palestine Securities Exchange,XPAE,-PS
NAS,US,Nasdaq All Markets,XNAS,
BOT,BW,Botswana Stock Exchange,XBOT,-BG
NSE,IN,National Stock Exchange Of India,XNSE,-IS
CNSGHK,CN,Shanghai Stock Exchange Shanghai Hong Kong Stock Connect,XSSC,-C1
BOM,IN,Bse Ltd,XBOM,-IB
MIL,IT,Borsa Italiana S P A,XMIL,-IM
BAA,BS,Bahamas International Securities Exchange,XBAA,-BM
STC,VN,Hochiminh Stock Exchange,XSTC,-VM
ESBSE,ES,Bolsa De Barcelona,XBAR,-SB
USPAC,US,Nyse Arca,ARCX,-UP
ESBBSE,ES,Bolsa De Valores De Bilbao,XBIL,-SO
ALG,DZ,Algiers Stock Exchange,XALG,-AG
STE,UZ,Republican Stock Exchange,XSTE,-ZU
BUE,AR,Bolsa De Comercio De Buenos Aires,XBUE,-AF
PINX,US,Otc Markets,OTCM,-UV
KSE,KG,Kyrgyz Stock Exchange,XKSE,-KB
BUD,HU,Budapest Stock Exchange,XBUD,-HB
CSE,DK,Nasdaq Copenhagen A S,XCSE,-DC
UAX,UA,Ukrainian Exchange,UKEX,-UK
BRA,SK,Bratislava Stock Exchange,XBRA,-SK
ASX,AU,Asx All Markets,XASX,-AT
CIE,GG,The International Stock Exchange,XCIE,-GU
CAY,KY,Cayman Islands Stock Exchange,XCAY,-KY
GTG,GT,Bolsa De Valores Nacional Sa,XGTG,-GL
ULA,MN,Mongolian Stock Exchange,XULA,-MO
BSE,RO,Spot Regulated Market Bvb,XBSE,-RE
PHL,US,Nasdaq Omx Phlx,XPHL,
ROCO,TW,Taipei Exchange,ROCO,-TT
QUI,EC,Bolsa De Valores De Quito,XQUI,-EQ
POR,US,Portal,XPOR,
BKK,TH,Market For Alternative Investment,XBKK,-TB
KRKSDQ,KR,Korea Exchange Kosdaq,XKOS,-KQ
TAI,TW,Taiwan Stock Exchange,XTAI,-TT
CAI,EG,Egyptian Exchange,XCAI,-EC
RIS,LV,Nasdaq Riga As,XRIS,-LG
KRKNX,KR,Korea New Exchange,XKON,-KE
NMS,US,Nasdaq Nms Global Market,XNMS,
NAI,KE,Nairobi Stock Exchange,XNAI,-KN
KLS,MY,Bursa Malaysia,XKLS,-MK
KRX,KR,Korea Exchange Stock Market,XKRX,-KP
BRU,BE,Euronext Brussels,XBRU,-BB
JKT,ID,Indonesia Stock Exchange,XIDX,-IJ
SEPE,UA,Stock Exchange Perspectiva,SEPE,-SEPE
SHE,CN,Shenzhen Stock Exchange,XSHE,-CS
DUS,DE,Boerse Duesseldorf,XDUS,-GD
LUS,ZM,Lusaka Stock Exchange,XLUS,-ZL
CAS,MA,Casablanca Stock Exchange,XCAS,-MC
ADS,AE,Abu Dhabi Securities Exchange,XADS,-DH
MNX,ME,Montenegro Stock Exchange,XMNX,-ME
PTY,PA,Bolsa De Valores De Panama S A,XPTY,-PP
SAU,SA,Saudi Stock Exchange,XSAU,-AB
SES,SG,Singapore Exchange,XSES,-SP
LDN,GB,Euronext London,XLDN,-LD
DSX,CM,Douala Stock Exchange,XDSX,-DE
ICE,IS,Nasdaq Iceland Hf,XICE,-RF
LSM,LY,Libyan Stock Market,XLSM,-LY
BRN,CH,Bx Swiss Ag,XBRN,-SR
BER,DE,Boerse Berlin,XBER,-GB
CYS,CY,Cyprus Stock Exchange,XCYS,-CY
UGA,UG,Uganda Securities Exchange,XUGA,-UG
FRA,DE,Boerse Frankfurt,XFRA,-GF
NSA,NG,The Nigerian Stock Exchange,XNSA,-NL
TRN,TT,Trinidad And Tobago Stock Exchange,XTRN,-TP
HAN,DE,Boerse Hannover,XHAN,-GI
BEL,RS,Belgrade Stock Exchange,XBEL,-SG
IST,TR,Borsa Istanbul,XIST,-TI
TAL,EE,Nasdaq Tallinn As,XTAL,-ET
GSE,GE,Georgia Stock Exchange,XGSE,-GG
LAO,LA,Lao Securities Exchange,XLAO,-LS
MIC,RU,Moscow Exchange All Markets,MISX,-RX
AMM,JO,Amman Stock Exchange,XAMM,-JR
LIT,LT,Ab Nasdaq Vilnius,XLIT,-LH
ARM,AM,Nasdaq Omx Armenia,XARM,-AY
DSMD,QA,Qatar Exchange,DSMD,-QD
ATH,GR,Athens Stock Exchange,ASEX,-GA
ESVSE,ES,Bolsa De Valencia,XVAL,-SA
NAM,NA,Namibian Stock Exchange,XNAM,-NW
BLB,BA,Banja Luka Stock Exchange,XBLB,-BK
DIFX,AE,Nasdaq Dubai,DIFX,-DU
KAR,PK,The Pakistan Stock Exchange Limited,XKAR,-PK
BNV,CR,Bolsa Nacional De Valores S A,XBNV,-CR
NGM,SE,Nordic Growth Market,XNGM,-NG
RURTS,RU,Moscow Exchange Derivatives Market,RTSX,-RR
ZAG,HR,Zagreb Stock Exchange,XZAG,-ZA
AMS,NL,Euronext Amsterdam,XAMS,-NA
LJU,SI,Ljubljana Stock Exchange,XLJU,-SV
KZAIX,KZ,Astana International Exchange Ltd,AIXK,-KX
USBATS,US,Cboe Bzx U S Equities Exchange,BATS,-UF
BOL,BO,Bolsa Boliviana De Valores S A,XBOL,-VB
TAE,IL,Tel Aviv Stock Exchange,XTAE,-IT
CNQ,CA,Canadian National Stock Exchange,XCNQ,-CF
SSE,BA,Sarajevo Stock Exchange,XSSE,-BT
GUA,EC,Bolsa De Valores De Guayaquil,XGUA,-EG
MAE,MK,Macedonian Stock Exchange,XMAE,-MS
NEP,NP,Nepal Stock Exchange,XNEP,-NK
JAM,JM,Jamaica Stock Exchange,XJAM,-JA
MAU,MU,Stock Exchange Of Mauritius Ltd,XMAU,-MP
LUX,LU,Luxembourg Stock Exchange,XLUX,-LX
IQS,IQ,Iraq Stock Exchange,XIQS,-IQ
LIM,PE,Bolsa De Valores De Lima,XLIM,-PE
SWX,CH,Six Swiss Exchange,XSWX,-SE
BOG,CO,Bolsa De Valores De Colombia,XBOG,-CX
JPOSE,JP,Osaka Exchange,XOSE,-JO
HAM,DE,Boerse Hamburg,XHAM,-GH
NZE,NZ,New Zealand Exchange Ltd,XNZE,-NZ
DSE,SY,Damascus Securities Exchange,XDSE,-SY
JPTSE,JP,Japan Exchange Group,XJPX,-JT
MUS,OM,Muscat Securities Market,XMUS,-OM
CIS,US,Nyse National Inc,XCIS,
PHS,PH,Philippine Stock Exchange Inc,XPHS,-PM
OSL,NO,Oslo Bors,XOSL,-NO
LIS,PT,Euronext Lisbon,XLIS,-PL
SHG,CN,Shanghai Stock Exchange,XSHG,-CG
MNT,UY,Bolsa De Valores De Montevideo,XMNT,-UY
TUN,TN,Bourse De Tunis,XTUN,-TU
BAB,BB,Barbados Stock Exchange,XBAB,-BA
BEY,LB,Bourse De Beyrouth Beirut Stock Exchange,XBEY,-LB
INMCX,IN,Metropolitan Stock Exchange Of India Limited,MCXX,-IG
MEX,MX,Bolsa Mexicana De Valores Mexican Stock Exchange,XMEX,-MM
INR,US,Finra,FINR,
PRA,CZ,Prague Stock Exchange,XPRA,-CK
TEH,IR,Tehran Stock Exchange,XTEH,-IE
HSTC,VN,Hanoi Stock Exchange,HSTC,-VH
DAR,TZ,Dar Es Salaam Stock Exchange,XDAR,-TZ
ZAA2X,ZA,A 2 X,A2XX,-AJ
PLU,GB,Aquis Stock Exchange,NEXX,-PZ
NEC,AU,National Stock Exchange Of Australia Limited,XNEC,-AO
WAR,PL,Warsaw Stock Exchange Equities Main Market,XWAR,-PW
NYS,US,New York Stock Exchange Inc,XNYS,-UN
KAZ,KZ,Kazakhstan Stock Exchange,XKAZ,-KZ
SPS,FJ,South Pacific Stock Exchange,XSPS,-FS
VPA,PY,Bolsa De Valores Y Productos De Asuncion Sa,XVPA,-PN
SCSSE,SC,Merj Exchange Limited,TRPX,-SZ
PFTS,UA,Pfts Stock Exchange,PFTS,-UZ
LON,GB,London Stock Exchange,XLON,-LN
HKG,HK,Hong Kong Exchanges And Clearing Ltd,XHKG,-HK
CNSZHK,CN,Shenzhen Stock Exchange Shenzhen Hong Kong Stock Connect,XSEC,-C2
CHG,BD,Chittagong Stock Exchange Ltd,XCHG,-C3
MOL,MD,Moldova Stock Exchange,XMOL,-MB
SWA,SZ,Swaziland Stock Exchange,XSWA,-SD
BCSE,BY,Belarus Currency And Stock Exchange,BCSE,-RB
SVA,SV,El Salvador Stock Exchange,XSVA,-EL
PAR,FR,Euronext Paris,XPAR,-FP
AUSSX,AU,Sydney Stock Exchange Limited,APXL,-PF
NEOE,CA,Neo Exchange Neo L Market By Order,NEOE,-QH
MUN,DE,Boerse Muenchen,XMUN,-GM
ZIM,ZW,Zimbabwe Stock Exchange,XZIM,-ZH

#endif
