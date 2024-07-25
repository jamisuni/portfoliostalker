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

partial class SettMarkets
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    // Decision! Not showing last/next closing times on UI, but look them on CmdTerminal

    protected List<ViewMarket> _view = new();

    protected bool _listActivesOnly = true;

    protected MarketId _editing = MarketId.Unknown;

    protected bool _editActive = false;
    protected int _editMinFetchMins = 1;
    protected string _editHolidays = string.Empty;

    public class ViewMarket
    {
        public char Active { get; set; } = '?';

        public MarketMeta Meta { get; set; }

        public string Closing { get; set; }

        public Color ClosingColor {  get; set; }
    }

    protected override void OnInitialized()
    {
        Reload();
    }

    protected void Reload()
    {
        DateTime utcNow = Pfs.Platform().GetCurrentUtcTime();

        _view = new();
        MarketStatus[] status = Pfs.Account().GetMarketStatus();

        foreach (MarketStatus ms in status)
        {
            if (ms.active)
            {
                ViewMarket m = new()
                {
                    Active = '+',
                    Meta = ms.market,
                };
                (m.Closing, m.ClosingColor) = Local_GetClosing(ms);
                _view.Add(m);
            }
            else if (_listActivesOnly == false)
            {
                ViewMarket m = new()
                {
                    Active = '-',
                    Meta = ms.market,
                };
                (m.Closing, m.ClosingColor) = Local_GetClosing(ms);
                _view.Add(m);
            }
        }
        return;

        (string, Color) Local_GetClosing(MarketStatus ms)
        {
            MarketCfg cfg = Pfs.Config().GetMarketCfg(ms.market.ID);

            TimeSpan fromClosing = utcNow - ms.lastClosingUtc;

            if ( fromClosing.TotalMinutes < 12 * 60 )
            {
                if (cfg != null && fromClosing.TotalMinutes < cfg.minFetchMins)
                    return (fromClosing.ToString(@"hh\:mm") + " AFTER", Color.Warning);
                else
                    return (fromClosing.ToString(@"hh\:mm") + " CLOSE", Color.Default);
            }

            TimeSpan toClosing = ms.nextClosingUtc - utcNow;

            if ( toClosing.TotalMinutes < 8 * 60 )
                return (toClosing.ToString(@"hh\:mm") + " OPEN", Color.Success);

            return (ms.lastDate.ToString("MMM-dd"), Color.Default);
        }
    }

    protected void OnChangedBetwAllAndActive(bool state)
    {
        _editing = MarketId.Unknown;
        _listActivesOnly = state;
        Reload();
        StateHasChanged();
    }

    private async Task OnRowClickedAsync(TableRowClickEventArgs<ViewMarket> data)
    {
        if ( _editing == data.Item.Meta.ID )
        {
            _editing = MarketId.Unknown;
            StateHasChanged();
            return;
        }
        _editing = data.Item.Meta.ID;

        MarketCfg cfg = Pfs.Config().GetMarketCfg(_editing);

        if ( cfg != null)
        {
            _editActive = cfg.Active;
            _editHolidays = cfg.Holidays;
            _editMinFetchMins = cfg.minFetchMins;
        }
        else
        {
            _editActive = false;
            _editHolidays = string.Empty;
            _editMinFetchMins = 1;
        }

        StateHasChanged();
    }

    protected void OnChangeEditActive(bool state)
    {
        _editActive = state;
    }

    private void EditCancel()
    {
        _editing = MarketId.Unknown;
        StateHasChanged();
        return;
    }

    private async Task EditSaveAsync()
    {
        MarketCfg cfg = new MarketCfg(_editActive, _editHolidays, _editMinFetchMins);

        if (Pfs.Config().SetMarketCfg(_editing, cfg) == false )
        {
            await Dialog.ShowMessageBox("Cant do!", "Invalid holiday string format?", yesText: "I'll check it");
            return;
        }

        Reload();
        _editing = MarketId.Unknown;
        StateHasChanged();
        return;
    }
}
