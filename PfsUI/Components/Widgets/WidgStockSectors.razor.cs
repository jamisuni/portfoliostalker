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

using Pfs.Types;

namespace PfsUI.Components;

// Simple one-liner component by default showing current sector fields set to stock by user, and allowing edit them
public partial class WidgStockSectors
{
    [Inject] PfsClientAccess Pfs { get; set; }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    protected bool _edit;
    protected const string Unselected = "-unselected-";

    protected string[] _sectorNames = null;
    protected string[] _stockFields = null;
    protected string[][] _sectorFields = null;
    protected string[] _stockSelections = null;

    protected override void OnParametersSet()
    {
        Init();
    }

    protected void Init()
    {
        _edit = false;
        _sectorNames = Pfs.Stalker().GetSectorNames();
        _stockFields = Pfs.Stalker().GetStockSectorFields($"{Market}${Symbol}");
    }

    protected void OnStartEditing()
    {
        _edit = true;

        _sectorFields = new string[SSector.MaxSectors][];
        _stockSelections = Pfs.Stalker().GetStockSectorFields($"{Market}${Symbol}");

        for (int s = 0; s < SSector.MaxSectors; s++)
        {
            if (string.IsNullOrWhiteSpace(_sectorNames[s]) == false)
                _sectorFields[s] = Pfs.Stalker().GetSectorFieldNames(s);
        }

        StateHasChanged();
    }

    protected void OnSave()
    {
        string cmd;

        for (int s = 0; s < SSector.MaxSectors; s++)
        {
            if (string.IsNullOrWhiteSpace(_sectorNames[s]))
                continue;

            if (_stockSelections[s] == null || _stockSelections[s] == _stockFields[s])
                continue;

            if (_stockSelections[s] == Unselected )
                // Unfollow-Sector SRef SectorId
                cmd = $"Unfollow-Sector SRef=[{Market}${Symbol}] SectorId=[{s}]";
            else
            {
                // Follow-Sector SRef SectorId FieldId
                cmd = $"Follow-Sector SRef=[{Market}${Symbol}] SectorId=[{s}] FieldId=[{Array.IndexOf(_sectorFields[s], _stockSelections[s])}]";
            }
        
            Result stalkerResp = Pfs.Stalker().DoAction(cmd);

            if ( stalkerResp.Ok == false)
            {
                // ? Ignore ?
            }
        }

        Init();
        StateHasChanged();
    }
}
