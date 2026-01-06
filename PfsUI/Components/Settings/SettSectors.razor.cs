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

partial class SettSectors
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }

    protected ViewRow[] _viewRow = new ViewRow[SSector.MaxFields + 1];

    // protected List<ReportStockSectorUsage> _usageData = null;

    protected const int MAX = SSector.MaxNameLen;

    protected bool[] _sectorEdit = [false, false, false]; 

    public class ViewRow
    {
        public string Label { get; set; } = string.Empty;

        public string[] Edit { get; set; } = new string[SSector.MaxSectors];

        public int[] Usage { get; set; } = new int[SSector.MaxSectors];
    }

    protected override void OnInitialized()
    {
        Reload();
    }

    protected void Reload()
    {
        string[] sectorNames = Pfs.Stalker().GetSectorNames(); // always at least string.Empty's

        for (int i = 0; i < _viewRow.Length; i++)
            _viewRow[i] = new();

        for (int sectorId = 0; sectorId < SSector.MaxSectors; sectorId++)
        {
            _viewRow[0].Label = "Header";

            _viewRow[0].Edit[sectorId] = sectorNames[sectorId];
            _viewRow[0].Usage[sectorId] = 0; //  _usageData.Where(s => string.IsNullOrWhiteSpace(s.Sectors[sectorId]) == true).Count();

            string[] fields = Pfs.Stalker().GetSectorFieldNames(sectorId); // always at least string.Empty's

            for (int fieldId = 0; fieldId < SSector.MaxFields; fieldId++)
            {
                string[] sRefs = Pfs.Stalker().GetSectorFieldStocks(sectorId, fields[fieldId]);

                _viewRow[fieldId + 1].Edit[sectorId] = fields[fieldId];
                _viewRow[fieldId + 1].Usage[sectorId] = sRefs.Count();

                _viewRow[0].Usage[sectorId] += sRefs.Count();
            }
        }
    }

    protected async Task OnBtnSector0Async() { await OnHandleSectorBtnAsync(0); }
    protected async Task OnBtnSector1Async() { await OnHandleSectorBtnAsync(1); }
    protected async Task OnBtnSector2Async() { await OnHandleSectorBtnAsync(2); }

    protected async Task OnHandleSectorBtnAsync(int secID)
    {
        if (_sectorEdit[secID]) // Do SAVE
        {
            bool failed = false;

            // Need to compare edited contents to one from Stalker.. and figure out what was changed 

            string[] sectorNames = Pfs.Stalker().GetSectorNames();

            for (int sectorId = 0; sectorId < SSector.MaxSectors; sectorId++)
            {
                if (sectorNames[sectorId] != _viewRow[0].Edit[sectorId] )
                {   // Sector name itself has changed
                    if ( string.IsNullOrWhiteSpace(_viewRow[0].Edit[sectorId]) )
                    {   // DeleteAll-Sector SectorId
                        Result stalkerRes = Pfs.Stalker().DoAction($"DeleteAll-Sector SectorId=[{sectorId}]");

                        if (stalkerRes.Ok == false)
                            failed = true;
                        else // Successfull delete of whole sector & all fields so we done for this one...
                            continue;
                    }
                    else
                    {   // Set-Sector SectorId SectorName
                        Result stalkerRes = Pfs.Stalker().DoAction($"Set-Sector SectorId=[{sectorId}] SectorName=[{_viewRow[0].Edit[sectorId]}]");

                        if (stalkerRes.Ok == false)
                            failed = true;
                    }
                }

                if (string.IsNullOrWhiteSpace(_viewRow[0].Edit[sectorId]))
                    continue;

                string[] fields = Pfs.Stalker().GetSectorFieldNames(sectorId);

                for (int fieldId = 0; fieldId < SSector.MaxFields; fieldId++)
                {
                    if (fields[fieldId] != _viewRow[fieldId+1].Edit[sectorId])
                    {   // Field name has changed
                        if (string.IsNullOrWhiteSpace(_viewRow[fieldId + 1].Edit[sectorId]))
                        {   // Delete-Sector SectorId FieldId
                            Result stalkerRes = Pfs.Stalker().DoAction($"Delete-Sector SectorId=[{sectorId}] FieldId=[{fieldId}]");

                            if (stalkerRes.Ok == false)
                                failed = true;
                        }
                        else
                        {   // Edit-Sector SectorId FieldId FieldName
                            Result stalkerRes = Pfs.Stalker().DoAction($"Edit-Sector SectorId=[{sectorId}] FieldId=[{fieldId}] FieldName=[{_viewRow[fieldId + 1].Edit[sectorId]}]");

                            if (stalkerRes.Ok == false)
                                failed = true;
                        }
                    }
                }
            }

            if ( failed)
                await LaunchDialog.ShowMessageBox("Failed!", "Saving failed at least partially, carefull w special chars!", yesText: "Ok");

            _sectorEdit[secID] = false;
            Reload();
            StateHasChanged();
        }
        else // Allow EDIT
            _sectorEdit[secID] = true;
    }
}
