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

    protected ReadOnlyCollection<SAlarm> _viewAlarms = null;
    protected string _errMsg = "";

    protected SAlarm _selectedAlarm = null;
    protected MarketMeta _marketMeta = null; // MarketCurrency

    protected override void OnParametersSet()
    {
        if ( Market == MarketId.CLOSED || Market == MarketId.Unknown )
        {
            _errMsg = "No alarm functionality available, as this stock is closed already!";
            return;
        }

        _marketMeta = PfsClientAccess.Account().GetMarketMeta(Market);

        UpdateAlarms();
    }

    protected void UpdateAlarms()
    {
        _viewAlarms = PfsClientAccess.Stalker().StockAlarmList(Market, Symbol);

        if (_viewAlarms != null && _viewAlarms.Count() > 0)
            _viewAlarms = _viewAlarms.OrderByDescending(a => a.AlarmType).ThenByDescending(a => a.Level).ToList().AsReadOnly();
    }

    private async Task OnRowClickedAsync(TableRowClickEventArgs<SAlarm> args)
    {
        await LaunchDlgAlarmEdit(args.Item);
    }

    public async Task AddNewAlarmAsync()
    {
        await LaunchDlgAlarmEdit();
    }

    private async Task OnEditAlarmAsync(SAlarm alarm)
    {
        await LaunchDlgAlarmEdit(alarm);
    }

    protected async Task LaunchDlgAlarmEdit(SAlarm alarm = null)
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
}
