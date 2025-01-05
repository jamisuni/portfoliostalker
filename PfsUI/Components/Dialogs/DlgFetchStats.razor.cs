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
using System.Timers;

namespace PfsUI.Components;

public partial class DlgFetchStats
{
    [Parameter] public int PendingAmount { get; set; }  // Given by caller to indicate amount of non-fetched as those pending 'MinFetchMins'
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService LaunchDialog { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    protected int _maxProgress { get; set; } = 1;       // How many symbols is under fetching
    protected int _totalProgress { get; set; } = 0;     // How many fetched so far from max
    protected int _failedProgress { get; set; } = 0;

    protected FetchProgress _progress;

    protected override async Task OnInitializedAsync()
    {
        await Task.CompletedTask;

        OnUpdate(this, null);

        var timer = new System.Timers.Timer(1000);
        timer.Elapsed += OnUpdate;
        timer.Start();

        return;
    }

    protected void OnUpdate(object sender, ElapsedEventArgs e)
    {
        _progress = Pfs.Eod().GetFetchProgress();
        _maxProgress = _progress.Requested;
        _failedProgress = _progress.Failed + _progress.Ignored;
        _totalProgress = _progress.Succeeded + _failedProgress;

        StateHasChanged();
    }

    protected async Task OnShowFailuresAsync()
    {
        Result<string> failuresRes = await Pfs.Cmd().CmdAsync($"fetcheod failed");

        if (failuresRes.Ok == false || string.IsNullOrWhiteSpace(failuresRes.Data))
            return;

        var parameters = new DialogParameters
        {
            { "Title",  "Latest failures" },
            { "Text",  failuresRes.Data }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };

        await LaunchDialog.ShowAsync<DlgSimpleTextViewer>("", parameters, maxWidth);
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
