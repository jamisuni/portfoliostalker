﻿/*
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

public partial class SettProviders
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService Dialog { get; set; }

    protected static readonly ReadOnlyDictionary<ExtProviderId, DlgProviderCfg> _description = new ReadOnlyDictionary<ExtProviderId, DlgProviderCfg>(new Dictionary<ExtProviderId, DlgProviderCfg>
    {
        [ExtProviderId.AlphaVantage] = new DlgProviderCfg()
        {
            Name = "AlphaVantage (avoid: 5 per min, 25 max day)",
            Addr = "https://www.alphavantage.co/",
            Desc = "Support US/TSX" + Environment.NewLine
                 + "Free: => Slow, as speed capped to 5 stocks per minute fetching. Dropped 500->25 per day limit. Sad as very professional looking!" + Environment.NewLine
                 + "Premium: => 50$/month for 75 API request per minute would solve speed/limit issues. Not bad! Is it for business use / profit applications?",
        },

        [ExtProviderId.Unibit] = new DlgProviderCfg()
        {
            Name = "UniBit",
            Addr = "https://unibit.ai/solution",
            Desc = "Recommended as supports all markets. Data available in 3+ hours after market closing." + Environment.NewLine
                 + "50,000 monthly credits of free account is 2170 max credits per. " + Environment.NewLine
                 + "Web Appl: => Nice as very fast fetching of daily closing data. " + Environment.NewLine
                 //                     + "Priv Srv: => Good option for Non-US markets, but keep eye stock amounts under it." + Environment.NewLine
                 + "Premium: => 200$/month as cheapest is hefty price for personal / home use. Atm cant see value worth of testing it out." + Environment.NewLine
                 + "Note! Did see often days when End-Of-Day values for bunch of stocks on main markets where missing half day after market closing.",
            // => One day missed half stocks from 3 main markets for all day, next day missed 15 stocks from TSX still 8 hours after closing
            //    so all ok dang fast for use but has to live w issues is seams to have often to keep up w data
            Test = TestSupport.StockMSFT,
        },

        [ExtProviderId.CurrencyAPI] = new DlgProviderCfg()
        {
            Name = "CurrencyAPI",
            Addr = "https://currencyapi.com/",
            Desc = "Currency rate provider. No stock market data. Free account is limited to 300 requests per month.",
            Test = TestSupport.None,
        },

        [ExtProviderId.TwelveData] = new DlgProviderCfg()
        {
            Name = "TwelveData",
            Addr = "https://twelvedata.com/",
            Desc = "Looks promising, but under testing still... up to 800 stocks per day, but max 8 per minute.",
            Test = TestSupport.StockMSFT,
        },

        [ExtProviderId.Polygon] = new DlgProviderCfg()
        {
            Name = "Polygon (free slow, USA only, but OK)",
            Addr = "https://polygon.io/",
            Desc = "All good option for US markets, sadly no other markets. Able to provide currency rates! Data availability is ?? hours." + Environment.NewLine
                 + "Slow, as speed capped to 5 stocks per minute fetching, no credit limits.",
            Test = TestSupport.StockMSFT,
        },
        [ExtProviderId.Marketstack] = new DlgProviderCfg()
        {
            Name = "Marketstack (requires 9U$ subscription)",
            Addr = "https://marketstack.com/?utm_source=FirstPromoter&utm_medium=Affiliate&fpr=pfs",
            Desc = "Supports all markets. Requires 9$/month account minimum! They do NOT provide functional free account as no HTTPS." + Environment.NewLine
                 + "9$/month gives 10,000 Req/mo. Thats enough for personal use for few hundred stocks with normal using."
        },
    });


    protected ExtProviderId _selectedProvider = ExtProviderId.Unknown;

    protected string _providerDesc = string.Empty; // Has to copy here as cant do bind to dictionary field wo errors
    protected TestSupport _providerTestSupport = TestSupport.None;

    protected Dictionary<ExtProviderId, string> _providerKeys = null;

    protected enum TestSupport
    {
        None = 0,
        StockMSFT,
    }

    protected override void OnInitialized()
    {
        _providerKeys = Pfs.Config().GetProvPrivKeys();
    }

    protected void OnProviderChanged(object provider)
    {
        _selectedProvider = (ExtProviderId)Enum.Parse(typeof(ExtProviderId), provider.ToString());
        _providerDesc = _description[_selectedProvider].Desc;
        _providerTestSupport = _description[_selectedProvider].Test;
    }

    protected async Task OnKeySaveAsync()
    {
        string updKey = _providerKeys[_selectedProvider];

        if (string.IsNullOrWhiteSpace(updKey))
            Pfs.Config().SetProvPrivKey(_selectedProvider, null);
        else
            Pfs.Config().SetProvPrivKey(_selectedProvider, updKey);

        _selectedProvider = ExtProviderId.Unknown;
        StateHasChanged();
    }

    private async Task OnBtnManualTestAsync()
    {
        if (_providerKeys[_selectedProvider] != Pfs.Config().GetProvPrivKeys()[_selectedProvider])
        {
            await Dialog.ShowMessageBox("Plz save!", "U need to save edited key, before clicking here", yesText: "Ok");
            return;
        }

        if (_providerTestSupport == TestSupport.StockMSFT)
        {
            Dictionary<ExtProviderId, Result<ClosingEOD>> fetchResult = await Pfs.Account().TestStockFetchingAsync(MarketId.NASDAQ, "MSFT", new ExtProviderId[1] { _selectedProvider });

            if (fetchResult == null || fetchResult.ContainsKey(_selectedProvider) == false)
                return;

            Result<ClosingEOD> res = fetchResult[_selectedProvider];

            if (res.Ok)
                await Dialog.ShowMessageBox("Ok!", $"For NASDAQ$MSFT got {res.Data.Close.ToString("0.00")}", yesText: "Ok");
            else
                await Dialog.ShowMessageBox("Failed!", $"Failed to error: {(res as FailResult<ClosingEOD>).Message}", yesText: "Ok");
        }
    }

    protected struct DlgProviderCfg
    {
        public string Name;
        public string Addr;
        public string Desc;
        public TestSupport Test;
    };
}
