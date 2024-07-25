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

public partial class DlgPortfolioEdit
{
    [Inject] PfsClientAccess PfsClientAccess { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    [Parameter] public string EditCurrPfName { get; set; } = string.Empty; 

    protected bool _fullscreen { get; set; } = false;
    protected string _editPfName = string.Empty;

    protected override void OnInitialized()
    {
        _editPfName = EditCurrPfName;
    }

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

    private async Task OnBtnEditAsync()
    {
        if (string.IsNullOrWhiteSpace(_editPfName) == true)
            return;

        // Edit-Portfolio PfCurrName PfNewName
        string cmd = string.Format("Edit-Portfolio PfCurrName=[{0}] PfNewName=[{1}]", EditCurrPfName, _editPfName);
        Result resp = PfsClientAccess.Stalker().DoAction(cmd);

        if (resp.Ok)
            MudDialog.Close();
        else
        {
            await Dialog.ShowMessageBox("Failed!", string.Format("Error: {0}", (resp as FailResult<string>).Message), yesText: "Ok");
        }
    }

    private async Task OnBtnAddAsync()
    {
        if (string.IsNullOrWhiteSpace(_editPfName) == true)
            return;

        string cmd = string.Format("Add-Portfolio PfName=[{0}]", _editPfName);
        Result resp = PfsClientAccess.Stalker().DoAction(cmd);

        if (resp.Ok)
            MudDialog.Close();
        else
        {
            await Dialog.ShowMessageBox("Failed!", string.Format("Error: {0}", (resp as FailResult<string>).Message), yesText: "Ok");
        }
    }
}
