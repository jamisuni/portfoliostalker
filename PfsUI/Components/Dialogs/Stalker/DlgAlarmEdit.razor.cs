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

public partial class DlgAlarmEdit
{
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
    [Inject] private IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }
    [Parameter] public SAlarm Alarm { get; set; } = null;       // If given then edit

    protected string _title;                                    // Add/Edit Alarm

    /* AlarmType, on Add can be selected, on Edit stays same
     * => Selecting it may change fields available -> so type gets locked when selected on Add
     * 
     */

    protected decimal _editLevel = 0;
    protected string _editNote = string.Empty;
    protected SAlarmType _editType = SAlarmType.Unknown;

    public SAlarmType EditType              
    { 
        get => _editType; 
        set
        {
            if(value == _editType)
                return;
            _editType = value;

            AlarmTypeSelectionChanged();
        }
    }

    protected override void OnInitialized()
    {
        if (Alarm != null)
        {
            _title = "Edit Alarm";

            _editType = Alarm.AlarmType;
            _editLevel = Alarm.Level;
            _editNote = Alarm.Note;
        }
        else
            _title = "Add Alarm";

        AlarmTypeSelectionChanged();
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    public void AlarmTypeSelectionChanged()
    {
        switch ( _editType )
        {
            case SAlarmType.Over:
            case SAlarmType.Under:
                break;

            // This is here waiting those cases where alarm has more fields than default ones...
        }
    }

    protected async Task DlgSaveAsync()
    {
        string cmd;

        if (Alarm != null)
            // Edit-Alarm Type SRef OldLevel NewLevel Prms Note
            cmd = $"Edit-Alarm Type=[{_editType}] SRef=[{Market}${Symbol}] OldLevel=[{Alarm.Level}] NewLevel=[{_editLevel}] Prms=[] Note=[{_editNote}]";
        else
            // Add-Alarm Type SRef Level Prms Note
            cmd = $"Add-Alarm Type=[{_editType}] SRef=[{Market}${Symbol}] Level=[{_editLevel}] Prms=[] Note=[{_editNote}]";

        Result stalkerResult = Pfs.Stalker().DoAction(cmd);

        if (stalkerResult.Ok)
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Operation failed!", (stalkerResult as FailResult).Message, yesText: "Ok");
    }

    protected async Task DlgDeleteAsync()
    {
        // Delete-Alarm SRef Value
        string cmd = $"Delete-Alarm SRef=[{Market}${Symbol}] Level=[{Alarm.Level}]";

        Result stalkerResult = Pfs.Stalker().DoAction(cmd);

        if (stalkerResult.Ok)
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Operation failed!", (stalkerResult as FailResult).Message, yesText: "Ok");
    }
}
