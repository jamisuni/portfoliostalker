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

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pfs.Types;

namespace PfsUI.Components;

// Allows to enter market+symbol combo and shows provides supporting market.. allowing user to test fetch latest closing for them
public partial class DlgTestStockFetch
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    [Parameter] public MarketId Market { get; set; } = MarketId.Unknown;
    [Parameter] public string Symbol { get; set; } = string.Empty;
    [Parameter] public ExtProviderId[] AvailableProviders { get; set; } = null;     // optional

    protected bool _lockSymbol = false;                         // Market/Symbol only locked after fetching is started
    protected bool _lockFetchBtn = false;                       // Button is locked on time of fetching
    List<MarketMeta> _markets = new();                          // List of markets those user has activated
    protected List<FetchProvider> _availableProviders = null;   // As soon market selected has list of providers here

    protected MarketId _selMarketTrigger
    {   // So Market is actual value holder, but we need to be able to have default on MudSelect & event from selection so this provides that
        get {  return Market; }
        set
        {
            Market = value;
            SetProviders();
        }
    }

    protected class FetchProvider(ExtProviderId ProvId)
    {
        public StateId State = StateId.selection;

        public bool Use { get; set; } = true;

        public ExtProviderId ProvId = ProvId;

        public FullEOD Result;

        public string ErrorMsg;

        public enum StateId
        {
            selection = 0,
            fetching,
            result,
            older,
            failed,
        }
    }

    protected override void OnInitialized()
    {
        if (Market.IsReal())
            SetProviders();
        else
            // cant bring providers in without setting also Market!
            AvailableProviders = null;

        _markets = Pfs.Account().GetActiveMarketsMeta().ToList();
    }

    public void SetProviders()
    {   // called when marketId is set properly... to show providers capable doing its fetching
        _availableProviders = new();

        if (AvailableProviders != null && AvailableProviders.Count() > 0 )
        {   // This case providers are enfroced by caller
            foreach (ExtProviderId provId in AvailableProviders)
                _availableProviders.Add(new FetchProvider(provId));
        }
        else
            foreach ( ExtProviderId provId in Pfs.Config().GetActiveEodProviders(Market))
                _availableProviders.Add(new FetchProvider(provId));
    }

    protected async Task OnTestStockFetchAsync()
    {
        Symbol = Symbol.ToUpper();

        List<ExtProviderId> useProviders = _availableProviders.Where(fp => fp.Use && fp.State == FetchProvider.StateId.selection).Select(fp => fp.ProvId).ToList();

        if (useProviders.Count == 0)
            return;

        _lockSymbol = true;
        _lockFetchBtn = true;

        _availableProviders.Where(fp => fp.Use && fp.State == FetchProvider.StateId.selection).ToList().ForEach(fp => fp.State = FetchProvider.StateId.fetching);
        MarketStatus marketStatus = Pfs.Account().GetMarketStatus().First(m => m.market.ID == Market);

        StateHasChanged();

        Dictionary<ExtProviderId, Result<FullEOD>> result = await Pfs.Eod().TestStockFetchingAsync(Market, Symbol, useProviders.ToArray());

        foreach ( KeyValuePair<ExtProviderId, Result<FullEOD>> kvp in result )
        {
            FetchProvider fp = _availableProviders.Single(p => p.ProvId == kvp.Key);

            if ( kvp.Value.Ok )
            {
                fp.Result = kvp.Value.Data;
                if (marketStatus.lastDate > fp.Result.Date)
                    fp.State = FetchProvider.StateId.older;
                else
                    fp.State = FetchProvider.StateId.result;
            }
            else
            {
                fp.State = FetchProvider.StateId.failed;
                fp.ErrorMsg = (kvp.Value as FailResult<FullEOD>).Message;
            }
        }
        if ( _availableProviders.Where(fp => fp.Use && fp.State == FetchProvider.StateId.selection).Count() > 0 )
            _lockFetchBtn = false;

        StateHasChanged();
    }

    protected void AcceptResult(FetchProvider fp)
    {
        if (Pfs.Stalker().GetStockMeta(Market, Symbol) == null)
            return;

        Pfs.Eod().AddEod(Market, Symbol, fp.Result);

        MudDialog.Close();
    }

    protected async Task AddFetchRuleAsync(FetchProvider fp)
    {
        Pfs.Config().SetEodFetchDedicatedProviderForSymbol(Market, Symbol, fp.ProvId);

        await LaunchDialog.ShowMessageBox("Fetch Rule Added!", $"{Market}${Symbol} on future is fetched wih {fp.ProvId}", yesText: "TY");
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
