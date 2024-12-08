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
public partial class DlgForceStockFetch
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    protected List<MarketId> _allActiveMarkets = null;
    protected List<ExtProviderId> _availableProviders = new();

    protected ExtProviderId _selectedProvider = ExtProviderId.Unknown;
    protected string _selMarkets = string.Empty;
    protected List<MarketId> _selMarketIds => _selMarkets.Split(',').ToList().Select(s => (MarketId) Enum.Parse(typeof(MarketId), s)).ToList();

    Dictionary<MarketId, List<string>> _expiredStocks = new();

    protected string SelMarketsTrigger
    {
        get {  return _selMarkets; }
        set
        {
            _availableProviders = new();
            _selectedProvider = ExtProviderId.Unknown;
            _selMarkets = value;

            if ( string.IsNullOrEmpty(value) )
                return;

            UpdateProvidersPerMarketSelection(_selMarketIds);
        }
    }

    protected override void OnInitialized()
    {
        // Get active markets for user, and limit off ones those dont have pending stocks
        _allActiveMarkets = Pfs.Account().GetActiveMarketsMeta().Select(m => m.ID).ToList();
        _expiredStocks = Pfs.Account().GetExpiredStocks();
        _allActiveMarkets = _allActiveMarkets.Where(m => _expiredStocks.ContainsKey(m)).ToList();

        if (_allActiveMarkets.Count == 0)
            return;
    }

    public void UpdateProvidersPerMarketSelection(List<MarketId> selMarketIds)
    {
        _availableProviders = new();

        foreach (MarketId marketId in selMarketIds)
        {
            List<ExtProviderId> supportingProviders = Pfs.Config().GetActiveEodProviders(marketId).ToList();

            if (_availableProviders.Count() == 0)
                _availableProviders = supportingProviders;
            else
                _availableProviders = _availableProviders.Intersect(supportingProviders).ToList();
        }
    }

    protected void OnForceStockFetch()
    {
        Dictionary<MarketId, List<string>> fetch = _expiredStocks.Where(e => _selMarketIds.Contains(e.Key)).ToDictionary(entry => entry.Key, entry => entry.Value);

        Pfs.Account().ForceFetchToProvider(_selectedProvider, fetch);

        MudDialog.Close();
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
