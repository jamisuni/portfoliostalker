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

using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pfs.Types;

namespace PfsUI.Components;

// Step-by-Step wizard to setup minimal tuff for new client so that can see some action easily...
public partial class DlgSetupWizard
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    protected bool _fullscreen { get; set; } = false;

    protected string _nextButton { get; set; } = "Next";

    List<MarketMeta> _pfsAllSupportedMarkets = null;

    protected string _overviewOfSetup = string.Empty;

    protected int _tabActivePanel = 0;
    protected bool[] _tabDisabled = new bool[(int)ProgressID.MarkerOfLast];

    protected enum ProgressID : int
    {
        Markets = 0,
        Currency,
        Provider,
        Stock,
        Overview,

        MarkerOfLast,
    }

    // Setup Selections

    protected class Setup
    {
        public string Entry { get; set; }
        public CurrencyId Currency { get; set; }

        public ExtProviderId ProviderId { get; set; }
    }

    protected List<Setup> _setup = null;

    protected override void OnInitialized()
    {
        // All markets available on selection list
        _pfsAllSupportedMarkets = new();
        foreach ( MarketId marketId in Enum.GetValues(typeof(MarketId)))
            if ( marketId.IsReal() )
                _pfsAllSupportedMarkets.Add(Pfs.Account().GetMarketMeta(marketId));

        foreach (ProgressID id in Enum.GetValues(typeof(ProgressID)))
        {
            if (id == ProgressID.MarkerOfLast)
                continue;

            _tabDisabled[(int)id] = true;
        }
        _tabDisabled[0] = false;
    }

    protected async Task DlgLaunchDemoAsync(int demoRef)
    {
        if (Pfs.Account().AccountType != AccountTypeId.Offline)
            return;

        NavigationManager.NavigateTo(NavigationManager.BaseUri + $"?demo={demoRef + 1}", true);
    }

    #region MARKETS

    protected string _selMarkets = string.Empty;

    protected MarketId[] GetMarketIds()
    {
        if (string.IsNullOrWhiteSpace(_selMarkets))
            return null;

        string[] marketIDs = _selMarkets.Split(',');

        List<MarketId> ret = new();

        foreach (string m in marketIDs)
            ret.Add((MarketId)Enum.Parse(typeof(MarketId), m.TrimStart().TrimEnd()));

        return ret.ToArray();
    }

    #endregion

    #region CURRENCY

    protected bool _requireCurrencyProvider = false;
    protected ExtProviderId _currencyProviderId = ExtProviderId.Unknown;
    protected ProviderCfg _currencyProviderCfg = null;
    protected string _currencyProviderKey = string.Empty;
    protected int _currencyProviderProposal = 0;

    protected CurrencyId _homeCurrency = CurrencyId.EUR;
    protected CurrencyId HomeCurrency
    {
        get { return _homeCurrency; }

        set
        {
            _homeCurrency = value;

            CheckIfCurrencyProviderRequired();
        }
    }

    protected static readonly ReadOnlyDictionary<ExtProviderId, ProviderCfg> _currencyProvidersDesc = new ReadOnlyDictionary<ExtProviderId, ProviderCfg>(new Dictionary<ExtProviderId, ProviderCfg>
    {
#if false
        [ExtProviderId.Polygon] = new ProviderCfg()
        {
            Desc = "POLYGON.IO. Speed capped to 5 request per minute, but that doesnt harm too much this case.",
        },
#endif
        [ExtProviderId.CurrencyAPI] = new ProviderCfg()
        {
            Desc = "CURRENCYAPI.COM. Fast but Free -account is limited to 300 request per month.",
        },
        [ExtProviderId.TwelveData] = new ProviderCfg()
        {
            Desc = "TWELVEDATA.COM. 2022-Jun, under testing!.",
        },
    });

    protected void OnBtnAlternativeCurrencyProvider()
    {
        _currencyProviderId = _currencyProvidersDesc.ToList()[_currencyProviderProposal % _currencyProvidersDesc.Count()].Key;
        _currencyProviderCfg = _currencyProvidersDesc[_currencyProviderId];

        _currencyProviderProposal++;
    }

    protected void CheckIfCurrencyProviderRequired()
    {
        // If all selected markets are same currency, as selected home currency then no
        foreach (Setup s in _setup)
        {
            if (s.Currency != _homeCurrency)
            {
                _requireCurrencyProvider = true;
                return;
            }
        }
        _requireCurrencyProvider = false;
    }

    #endregion

    #region PROVIDER

    protected ExtProviderId _providerId = ExtProviderId.Unknown;
    protected ProviderCfg _providerCfg = null;
    protected string _providerKey = string.Empty;
    protected int _providerProposal = 0;

    protected static readonly ReadOnlyDictionary<ExtProviderId, ProviderCfg> _providersDesc = new ReadOnlyDictionary<ExtProviderId, ProviderCfg>(new Dictionary<ExtProviderId, ProviderCfg>
    {
        [ExtProviderId.Unibit] = new ProviderCfg()
        {
            Desc = "Unibit.ai. USA + TSX + Most markets. Free account is OK option as its super fast, and  "
                 + "has excelent coverage of markets. Its limited per monthly max credit use, but as only "
                 + "fetching end of day data their free plan should be ok for 150 stocks!. "
                 + "Sadly, there is often days that bunch of stocks End-Of-Day valuation are many hours late!",
        },
        [ExtProviderId.AlphaVantage] = new ProviderCfg()
        {
            Desc = "Alphavantage.co. USA + TSX. Free account is OK option to try out, but please remember they "
                 + "enforce 5 tickers per minute fetch speed for free account. This gets slow if has 50+ "
                 + "stocks tracking as initial loading is going to take good while each day (10min+). "
                 + "This actually is one of trusted providers I prefer to use on my PrivSrv. ",
        },
        [ExtProviderId.Polygon] = new ProviderCfg()
        {
            Desc = "Polygon.io. US markets only. Free account is OK option to try out, but please remember they "
                 + "enforce 5 tickers per minute fetch speed for free account. ",
        },
        [ExtProviderId.TwelveData] = new ProviderCfg()
        {
            Desc = "twelvedata.com. Under testing",
        },
    });

    protected void OnBtnAlternativeProvider()
    {
        _providerProposal++;
        SetProviderProposal();
    }

    protected bool SetProviderProposal()
    {
        // This is ones we have configuring here on Setup Wizard to be available for setup
        List<ExtProviderId> suppProviders = _providersDesc.Keys.ToList();

        // And these are ones UI per current settings supports
        List<ExtProviderId> uisProviders = Pfs.Platform().GetClientProviderIDs(ExtProviderJobType.EndOfDay);

        // Create simple dictionary w providerID - bool to eliminate all those cant do whats asked by user...
        Dictionary<ExtProviderId, bool> candidates = new();

        foreach (ExtProviderId providerID in suppProviders )
        {
            if (uisProviders.Contains(providerID) == false)
                continue;

            candidates.Add(providerID, true);
        }

        // These are markets we want our proposed providers to support
        MarketId[] reqMarketIDs = GetMarketIds();

        // Loop markets one by one, and eliminate candidates those cant do that market
        foreach (MarketId marketId in reqMarketIDs)
        {
            List<ExtProviderId> supports = Pfs.Platform().GetMarketSupport(marketId);

            // Loop actual enum, as we wanna be able to edit 'candidates' list inside here
            foreach (ExtProviderId providerId in Enum.GetValues(typeof(ExtProviderId)))
            {
                if (candidates.ContainsKey(providerId) == false)
                    // Not even candidate, so skip
                    continue;

                if (candidates[providerId] == false )
                    // Already rejected
                    continue;

                if (supports.Contains(providerId) == false)
                    candidates[providerId] = false;
            }
        }

        List<ExtProviderId> availableProviders = candidates.Where(x => x.Value == true).Select(x => x.Key).ToList();

        if ( availableProviders.Count() == 0)
            // Note! There always should be at least one candidate left, as otherwise would not add its market..
            return false;

        _providerId = availableProviders[_providerProposal % availableProviders.Count()];
        _providerCfg = _providersDesc[_providerId];
        return true;
    }

    protected class ProviderCfg
    {
        public string Desc { get; set; }
    }

#endregion

    #region STOCKS

    public string _pfName = "My Portfolio";
    public MarketId _addStockToMarket = MarketId.Unknown;
    public string _addStockSymbol = string.Empty;
    public string _addStockName = string.Empty;

    #endregion

    protected void OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;

        MudDialog.Options.FullWidth = _fullscreen;
        MudDialog.SetOptions(MudDialog.Options);
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    private async Task OnBtnNext()
    {
        switch ((ProgressID)_tabActivePanel)
        {
            case ProgressID.Markets:
                {
                    if (string.IsNullOrWhiteSpace(_selMarkets) == true)
                        return;

                    MarketId[] marketIDs = GetMarketIds();

                    if (marketIDs == null || marketIDs.Count() > 4)
                    {
                        await Dialog.ShowMessageBox("Lets keep this simple!", "Max 4 selections, as you can add later more", yesText: "Ok");
                        return;
                    }

                    _setup = new();

                    foreach (MarketId marketId in marketIDs)
                    {
                        Setup s = new()
                        {
                            Entry = marketId.ToString(),
                            Currency = _pfsAllSupportedMarkets.Single(m => m.ID == marketId).Currency,
                            ProviderId = ExtProviderId.Unknown,
                        };

                        _setup.Add(s);
                    }

                    _tabDisabled[(int)ProgressID.Markets] = true;
                    _tabDisabled[(int)ProgressID.Currency] = false;

                    CheckIfCurrencyProviderRequired();
                    _tabActivePanel = (int)ProgressID.Currency;

                    // Just calls this one to get one set by default
                    OnBtnAlternativeCurrencyProvider();
                }
                break; // => _setup -list is created w row per selected market

            case ProgressID.Currency:
                {
                    if (_requireCurrencyProvider == true && _currencyProviderKey.Length != GetKeyLength(_currencyProviderId))
                    {
                        await Dialog.ShowMessageBox("Missing key!", "For this market / currency selection we do need that key, please.", yesText: "Ok");
                        return;
                    }

                    if (SetProviderProposal() == false)
                    {
                        await Dialog.ShowMessageBox("Coding error!", "Please report case for developers, interesting failure...", yesText: "Ok");
                        return;
                    }

                    _tabDisabled[(int)ProgressID.Currency] = true;
                    _tabDisabled[(int)ProgressID.Provider] = false;

                    _tabActivePanel = (int)ProgressID.Provider;

                    Setup s = new()
                    {
                        Entry = "Home Currency",
                        Currency = _homeCurrency,
                        ProviderId = _currencyProviderId,
                    };

                    _setup.Add(s);
                }
                break; // => if '_requireCurrencyProvider' is TRUE then '_currencyProviderKey' has key for it

            case ProgressID.Provider:
                {
                    if (_providerKey.Length != GetKeyLength(_providerId))
                    {
                        await Dialog.ShowMessageBox("Missing key!", "Need to give that key, as this application uses it on your browser session to fetch stock valuations.", yesText: "Ok");
                        return;
                    }

                    _tabDisabled[(int)ProgressID.Provider] = true;
                    _tabDisabled[(int)ProgressID.Stock] = false;

                    foreach (Setup s in _setup)
                    {   // updates tables provider columns for markets to match selection
                        if (s.ProviderId != ExtProviderId.Unknown)
                            continue;

                        s.ProviderId = _providerId;
                    }

                    _tabActivePanel = (int)ProgressID.Stock;
                }
                break;

            case ProgressID.Stock:
                {
                    if (string.IsNullOrWhiteSpace(_pfName) == true)
                        _pfName = "My Portfolio";

                    if ( _addStockToMarket == MarketId.Unknown || string.IsNullOrWhiteSpace(_addStockSymbol) && string.IsNullOrWhiteSpace(_addStockName) )
                    {
                        await Dialog.ShowMessageBox("Select stock!", "Lets get one stock selected so have something to start with.", yesText: "Ok");
                        return;
                    }

                    _tabDisabled[(int)ProgressID.Stock] = true;
                    _tabDisabled[(int)ProgressID.Overview] = false;

                    _tabActivePanel = (int)ProgressID.Overview;

                    SetOverviewOfSetup();
                    _nextButton = "Setup";
                }
                break;

            case ProgressID.Overview:
                {
                    await RunWizardSetupAsync();

                    MudDialog.Close();
                }
                break;
        }
    }

    protected void SetOverviewOfSetup()
    {
        _overviewOfSetup = "Ready for launch, please start countdown for Portfoliostalker setup...";
    }

    protected bool _isSetupBusy = false;

    protected async Task RunWizardSetupAsync()
    {
        _overviewOfSetup = "To the moon...";
        _isSetupBusy = true;

        /* 1) Set Home Currency
         * 2) Activate all markets those user selected
         * 3) Set keys for currency and EOD providers
         * 4) Activate PFS to use those providers for selected markets etc
         * 5) Add portfolio & StockMeta & PF Tracking for stock
         */

        // 1) Set Home Currency

        Pfs.Config().HomeCurrency = _homeCurrency;

        // 2) Activate all markets those user selected

        foreach (MarketId marketId in GetMarketIds())
            Pfs.Config().SetMarketCfg(marketId, new MarketCfg(true, string.Empty, 60));

        // 3) Set keys for currency and EOD providers

        if (_requireCurrencyProvider)
            await Pfs.Cmd().CmdAsync($"cfgprov setkey {_currencyProviderId} {_currencyProviderKey}");

        await Pfs.Cmd().CmdAsync($"cfgprov setkey {_providerId} {_providerKey}");

        // 4) Activate PFS to use those providers for selected markets etc

        Pfs.Config().SetActiveRatesProvider(_currencyProviderId);

        List<ProvFetchCfg> eodProvCfg = new();

        foreach (MarketId marketId in GetMarketIds())
            eodProvCfg.Add(new ProvFetchCfg(marketId, string.Empty, [_providerId]));

        Pfs.Config().SetEodFetchCfg(eodProvCfg.ToArray());

        // 5) Add portfolio & StockMeta & PF Tracking for stock

        Pfs.Stalker().DoAction($"Add-Portfolio PfName=[{_pfName}]");

        Pfs.Stalker().AddNewStockMeta(_addStockToMarket, _addStockSymbol.ToUpper(), _addStockName, string.Empty);

        Pfs.Stalker().DoAction($"Follow-Portfolio PfName=[{_pfName}] SRef=[{_addStockToMarket}${_addStockSymbol.ToUpper()}]");


        // 6) Start fetching rates          Note! Could conflict if same provider for currency & EOD

        Pfs.Account().RefetchLatestRates();

        Pfs.Account().FetchExpiredStocks();
    }

    protected int GetKeyLength(ExtProviderId providerId)
    {
        switch (providerId)
        {
//            case ExtProviderId.Polygon: return 32;
            case ExtProviderId.Unibit: return 32;
//            case ExtProviderId.Tiingo: return 40;
//            case ExtProviderId.Marketstack: return 32;
//            case ExtProviderId.AlphaVantage: return 16;
            case ExtProviderId.CurrencyAPI: return 36;
            case ExtProviderId.TwelveData: return 32;
        }
        return 0;
    }
}
