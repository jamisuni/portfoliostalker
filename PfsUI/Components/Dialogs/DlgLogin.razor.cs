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
using System.ComponentModel.DataAnnotations;

using MudBlazor;
using Pfs.Types;

namespace PfsUI.Components;

public partial class DlgLogin
{
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }
    [Inject] private IDialogService Dialog { get; set; }
    [Inject] PfsUiState PfsUiState { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    protected bool _remember = false;
    protected DlgLoginFormData _userinfo = null;
    protected string _defUsername = "";
    protected bool _showBusySignal = false;

    protected override void OnInitialized()
    {
        //_defUsername = PfsClientAccess.Account().Property("DEFUSERNAME");

        if (string.IsNullOrEmpty(_defUsername) == false)
            // Keep remember me setting on
            _remember = true;

        _userinfo = new()
        {
            Username = _defUsername,
        };
    }

    protected void OnSwapRegister()
    {
        MudDialog.Close();
    }

    protected void OnUsernameChanged(string username)
    {
        if (string.IsNullOrEmpty(_defUsername) == false)
        {
            // RememberMe is finished if username is edited
            _defUsername = string.Empty;
            _remember = false;
            StateHasChanged();
        }
    }

    protected async Task DlgLaunchDemoAsync(int demoRef)
    {
        if (Pfs.Account().AccountType != AccountTypeId.Offline)
            return;

        NavigationManager.NavigateTo(NavigationManager.BaseUri + $"?demo={demoRef+1}", true);
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    private async Task DlgOkAsync()
    {
        // Little bit of verifications

        if (string.IsNullOrWhiteSpace(_userinfo.Username) == true)
        {
            // Must have username
            await Dialog.ShowMessageBox("Failed!", "Give username", yesText: "Ok");
            return;
        }

        if (string.IsNullOrEmpty(_defUsername) == true && string.IsNullOrWhiteSpace(_userinfo.Password) == true)
        {
            if (_userinfo.Username.ToUpper().Contains("DEMO") == false)
            {
                // Must have password if not previous 'Remember Me' active
                await Dialog.ShowMessageBox("Failed!", "Give password", yesText: "Ok");
                return;
            }
        }

        _showBusySignal = true;

        // Thats minimal checking but ok, lets go then
        string errorMsg = ""; //  await Pfs.Account().UserLoginAsync(_userinfo.Username, _userinfo.Password, _remember);

        _showBusySignal = false;

        if (string.IsNullOrEmpty(errorMsg) == true)
        {
            // close dialog and let caller know 'OK'
            MudDialog.Close();
        }
        else
        {
            await Dialog.ShowMessageBox("Login Failed!", errorMsg, yesText: "Ok");
            StateHasChanged();
        }
    }

}

public class DlgLoginFormData
{
    [Required]
    [StringLength(32, MinimumLength = 5)]
    public string Username { get; set; }

    [Required]
    [StringLength(32, MinimumLength = 8)]
    public string Password { get; set; }
}
