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
using System.Collections.ObjectModel;

using MudBlazor;

using Pfs.Types;

namespace PfsUI.Components;

// Viewing/Editing component for Stock Alarm's to be used from multiple places
public partial class StockMgmtAlarms
{
    private EventHandler evChanged; // !!!CODE!!! Create event handler that ignores duplicate registerations
    public event EventHandler EvChanged
    {
        add
        {
            if (evChanged == null || !evChanged.GetInvocationList().Contains(value))
            {
                evChanged += value;
            }
        }
        remove
        {
            evChanged -= value;
        }
    }

    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess PfsClientAccess { get; set; }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    protected ReadOnlyCollection<ViewData> _viewAlarms = null;
    protected string _errMsg = "";

    protected ViewData _selectedAlarm = null;
    protected MarketMeta _marketMeta = null; // MarketCurrency
    protected FullEOD _fullEod = null;

    protected override void OnParametersSet()
    {
        if ( Market == MarketId.CLOSED || Market == MarketId.Unknown )
        {
            _errMsg = "No alarm functionality available, as this stock is closed already!";
            return;
        }

        _fullEod = PfsClientAccess.Eod().GetLatestSavedEod(Market, Symbol);

        _marketMeta = PfsClientAccess.Account().GetMarketMeta(Market);

        UpdateAlarms();
    }

    protected void UpdateAlarms()
    {
        var alarms = PfsClientAccess.Stalker().StockAlarmList(Market, Symbol);

        if (alarms == null || alarms.Count == 0)
        {
            _viewAlarms = new List<ViewData>().AsReadOnly();
            return;
        }

        var viewList = alarms
            .Select(s => new ViewData
            {
                a = s,
                AlarmDistance = GetAlarmDistance(s)
            })
            .ToList();

        // Order by underlying alarm's AlarmType then Level (both descending).
        _viewAlarms = viewList
            .OrderByDescending(v => v.a.AlarmType)
            .ThenByDescending(v => v.a.Level)
            .ToList()
            .AsReadOnly();
        return;

        decimal? GetAlarmDistance(SAlarm alarm)
        {
            switch ( alarm.AlarmType )
            {
                case SAlarmType.Under: return alarm.GetAlarmDistance(_fullEod.GetSafeLow());
                case SAlarmType.Over: return alarm.GetAlarmDistance(_fullEod.GetSafeHigh());
            }
            return null;
        }
    }

    private async Task OnRowClickedAsync(TableRowClickEventArgs<ViewData> args)
    {
        await LaunchDlgAlarmEdit(args.Item);
    }

    public async Task AddNewAlarmAsync()
    {
        await LaunchDlgAlarmEdit();
    }

    private async Task OnEditAlarmAsync(ViewData alarm)
    {
        await LaunchDlgAlarmEdit(alarm);
    }

    protected async Task LaunchDlgAlarmEdit(ViewData alarm = null)
    {
        var parameters = new DialogParameters
        {
            { "Market", Market },
            { "Symbol", Symbol },
            { "Alarm", alarm }
        };

        var dialog = await LaunchDialog.ShowAsync<DlgAlarmEdit>("Alarms", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            UpdateAlarms();
            StateHasChanged();

            evChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected class ViewData
    {
        public SAlarm a;

        public decimal? AlarmDistance { get; set; } = null;
    }
}
