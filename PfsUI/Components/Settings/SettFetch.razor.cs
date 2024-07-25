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

partial class SettFetch // Shows list of all EOD fetch rules, with: Market, opt Symbols effected, and one/many providers to handle fetching
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    protected List<ViewRule> _view = new();

    public class ViewRule
    {
        public MarketId Market { get; set; }

        public string StocksHeader { get; set; }

        public List<string> Stocks { get; set; } = null;

        public string ProvidersHeader { get; set; }

        public ExtProviderId[] Providers {  get; set; } = null;

        public ProvFetchCfg Cfg { get; set; }
    }

    protected override void OnInitialized()
    {
        Reload();
    }

    protected void Reload()
    {
        _view = new();

        foreach (ProvFetchCfg cfg in Pfs.Config().GetEodFetchCfg()) //  ProvSetFetchCfg(MarketId market, string symbols, ExtProviderId[] providers);
        {
            ViewRule rule = new()
            {
                Market = cfg.market,
                Cfg = cfg,
            };

            if ( cfg.providers.Count() > 1 )
            {
                rule.ProvidersHeader = $"{cfg.providers.Count()} providers";
                rule.Providers = cfg.providers;
            }
            else
                rule.ProvidersHeader = cfg.providers[0].ToString();

            if (string.IsNullOrWhiteSpace(cfg.symbols) == false)
            {
                rule.Stocks = cfg.symbols.Split(',').ToList();
                rule.StocksHeader = $"{rule.Stocks.Count()} Stocks";
            }
            _view.Add(rule);
        }
    }

    protected async Task OnBtnAddNewRuleAsync()
    {
        var dialog = Dialog.Show<SettFetchDlg>("");
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            ProvFetchCfg cfg = result.Data as ProvFetchCfg;

            List<ProvFetchCfg> existing = _view.Select(r => r.Cfg).ToList();
            existing.Add(cfg);
            Pfs.Config().SetEodFetchCfg(existing.ToArray());

            Reload();
            StateHasChanged();
        }
    }

    protected async Task DoEditAsync(ProvFetchCfg cfg)
    {
        var parameters = new DialogParameters
        {
            { "Cfg", cfg },
        };

        var dialog = Dialog.Show<SettFetchDlg>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            List<ProvFetchCfg> remaining = _view.Select(r => r.Cfg).ToList();
            remaining.Remove(cfg);
            remaining.Add(result.Data as ProvFetchCfg);

            Pfs.Config().SetEodFetchCfg(remaining.ToArray());

            Reload();
            StateHasChanged();
        }
    }

    protected async Task DoDeleteAsync(ProvFetchCfg cfg)
    {
        if ( cfg.symbols.Count() > 0 )
        {
            bool? result = await Dialog.ShowMessageBox("Sure?", "Going to remove also symbols!", yesText: "Remove", cancelText: "Cancel");

            if (result.HasValue == false || result.Value == false)
                return;
        }

        List<ProvFetchCfg> remaining = _view.Select(r => r.Cfg).ToList();
        remaining.Remove(cfg);
        Pfs.Config().SetEodFetchCfg(remaining.ToArray());

        Reload();
        StateHasChanged();
    }

    private async Task DoTestFetchEodAsync(ProvFetchCfg cfg)
    {
        var parameters = new DialogParameters
        {
            { "Market", cfg.market },
            { "Symbol", string.Empty },
            { "AvailableProviders", cfg.providers }
        };

        if ( cfg.symbols.Count() == 1)
            parameters["Symbol"] = cfg.symbols[0];

        var dialog = Dialog.Show<DlgTestStockFetch>("", parameters);
        await dialog.Result;
    }

    protected async Task OnTestFetchBtnAsync()
    {
        var dialog = Dialog.Show<DlgTestStockFetch>("Test Fetch");
        var result = await dialog.Result;

        if (!result.Canceled)
        {
        }
    }
}
