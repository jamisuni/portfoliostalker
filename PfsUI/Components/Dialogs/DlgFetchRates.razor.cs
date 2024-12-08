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

public partial class DlgFetchRates
{
    [Parameter] public List<Fetch> Missing { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    public class Fetch
    {
        public DateOnly Date;
        public CurrencyId Currency;
        public decimal? Rate = 0;
    }

    protected async Task OnBtnSearchOnlineAsync(Fetch f)
    {
        decimal? ret = await Pfs.Account().GetHistoryRateAsync(f.Currency, f.Date);

        if (ret.HasValue)
            f.Rate = ret.Value.Round5();
    }

    protected void OnAccept()
    {
        MudDialog.Close(DialogResult.Ok(Missing));
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
