﻿/*
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
     */

    protected bool _editExisting = false;

    protected SAlarmType _editType = SAlarmType.Unknown;

    protected decimal _lvlValue = 0;
    protected bool _lvlDisabled = false;

    protected string _noteValue = string.Empty;

    protected decimal _prm1Value = 0;
    protected string _prm1Label = string.Empty;
    protected bool _prm1Disabled = true;

    protected decimal _prm2Value = 0;
    protected string _prm2Label = string.Empty;
    protected bool _prm2Disabled = true;

    protected record AlarmItem(SAlarmType Type, string Desc);

    protected AlarmItem[] _alarmTypeSel = [         // !!!CODE!!! Init [] table with records
        new AlarmItem(SAlarmType.Over,  "Sell"),
        new AlarmItem(SAlarmType.Under, "Buy"),
//        new AlarmItem(SAlarmType.TrailingSellP, "Sell 'max'"),        still Beta
        ]; // Later add "Cut", "Profit" for tracking type alarms

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
            _editExisting = true;
            _title = "Edit Alarm";

            _editType = Alarm.AlarmType;
            _lvlValue = Alarm.Level;
            _noteValue = Alarm.Note;
        }
        else
            _title = "Add Alarm";

        if (Pfs.Account().GetAppCfg(AppCfgId.UseBetaFeatures) > 0)
            _alarmTypeSel = [.. _alarmTypeSel, new AlarmItem(SAlarmType.TrailingSellP, "Sell 'max'")];

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
                _prm1Label = string.Empty;
                break;

            case SAlarmType.TrailingSellP:
                // On creation/editing can tune trigger procentage how much drop is allowed
                _prm1Label = "Drop % from high to alarm";
                _prm1Disabled = false;

                if (_editExisting)
                {   // To allow debugging etc on editing show high points value
                    _lvlDisabled = true;

                    _prm2Label = "High";
                    _prm2Disabled = true;

                    _prm1Value = (Alarm as SAlarmTrailingSellP).DropP;
                    _prm2Value = (Alarm as SAlarmTrailingSellP).High;
                }
                else
                {   // On creating new one, its editable but normally should use latest closing as level
                    FullEOD eod = Pfs.Account().GetLatestSavedEod(Market, Symbol);
                    _lvlValue = eod?.Close ?? 0;

                    _prm1Value = Pfs.Account().GetAppCfg(AppCfgId.DefTrailingSellP);
                }
                break;
        }
    }

    protected async Task DlgSaveAsync()
    {
        string cmd;
        string prms = string.Empty;

        switch (_editType )
        {
            case SAlarmType.TrailingSellP: // limited edit for this type, just note & dropP allowed to change
                prms = SAlarmTrailingSellP.CreatePrms(_prm1Value, _lvlValue);
                break;
        }

        if (Alarm != null)
            // Edit-Alarm Type SRef OldLevel NewLevel Prms Note
            cmd = $"Edit-Alarm Type=[{_editType}] SRef=[{Market}${Symbol}] OldLevel=[{Alarm.Level}] NewLevel=[{_lvlValue}] Prms=[{prms}] Note=[{_noteValue}]";
        else
            // Add-Alarm Type SRef Level Prms Note
            cmd = $"Add-Alarm Type=[{_editType}] SRef=[{Market}${Symbol}] Level=[{_lvlValue}] Prms=[{prms}] Note=[{_noteValue}]";

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
