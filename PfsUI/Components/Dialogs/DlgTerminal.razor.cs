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

using BlazorDownloadFile;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pfs.Types;

namespace PfsUI.Components;

public partial class DlgTerminal
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    private string _cmdLine;
    private string _logText;
    private string _helpMsg;
    private List<string> _helpSel = null;
    private bool _helpSelMulti = false;
    private string _helpSelUser { get; set; } = "";

    protected override void OnInitialized()
    {
    }

    protected override void OnParametersSet()
    {
    }

    protected async Task OnCmdKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key != "Enter")
            return;

        Result<string> cmdResp = await Pfs.Cmd().CmdAsync(_cmdLine ?? "");

        string logUpdate = HandleTerminalResp(cmdResp);

        if (string.IsNullOrEmpty(logUpdate))
            return;

        string[] split = _cmdLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Count() > 1 && split[1].EndsWith("zip") )
        {   // for special cases with downloadable zip file, a cmd itself has special ending
            byte[] zip = Convert.FromBase64String(logUpdate);

            string fileName = $"{split.Last()}_" + DateTime.Today.ToString("yyyyMMdd") + ".zip";
            await BlazorDownloadFileService.DownloadFile(fileName, zip, "application/zip");

            _logText += Environment.NewLine;
            _logText += ">>>";
            _logText += _cmdLine;
            _logText += Environment.NewLine;
            _logText += "downloaded";
        }
        else
        {   // Normal case is that has string response
            _logText += Environment.NewLine;
            _logText += ">>>";
            _logText += _cmdLine;
            _logText += Environment.NewLine;
            _logText += logUpdate;
            
        }
        _cmdLine = string.Empty;
        StateHasChanged();
    }

    protected async Task OnHelpMeAsync()
    {
        Result<string> helpResp = await Pfs.Cmd().HelpMeAsync(_cmdLine?.ToLower() ?? "");

        string logUpdate = HandleTerminalResp(helpResp);

        if (string.IsNullOrEmpty(logUpdate) == false)
            // kind of "cmd looks ok, cant help"
            _helpMsg = logUpdate;

        StateHasChanged();
    }

    protected string HandleTerminalResp(Result<string> terminalResp)
    {
        HideHelpSelComponent();

        if (terminalResp.Fail)
        {
            _helpMsg = (terminalResp as FailResult<string>).Message;

            if (_helpMsg.Last() == ']')
            {   // this assumes that there is helper list [header,item1,item2] at end of msg
                _helpSel = _helpMsg.Substring(_helpMsg.IndexOf('[') + 1).Split(',').ToList();
                _helpMsg = _helpMsg.Substring(0, _helpMsg.IndexOf('['));

                if (_helpSel[0].First() == '#')
                    _helpSelMulti = true;

                _helpSel[_helpSel.Count - 1] = _helpSel.Last().Substring(0, _helpSel.Last().Length - 1);
            }
            return string.Empty;
        }
        return terminalResp.Data;
    }

    protected void OnHelpSelChanged(string selection)
    {
        _cmdLine += " " + selection;

        HideHelpSelComponent();
        StateHasChanged(); // !!!ERROR!!! Just doesnt wanna hide after selection, later...
    }

    protected void HideHelpSelComponent()
    {
        _helpSel = null;
        _helpMsg = string.Empty;
        _helpSelMulti = false;
    }
}
