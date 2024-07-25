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

namespace PfsUI.Components;

public partial class SettingsDlg
{
    [Inject] PfsClientAccess PfsClientAccess { get; set; }
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
    [Inject] IDialogService Dialog { get; set; }

    protected PageHeader _childPageHeader;

    protected override void OnInitialized()
    {
    }


    protected async Task OnOpenDlgTerminalAsync()
    {
        var options = new DialogOptions { FullScreen = true, CloseButton = true, DisableBackdropClick = true };
        var parameters = new DialogParameters();

        var dialog = Dialog.Show<DlgTerminal>("Terminal", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
        }
    }
}
