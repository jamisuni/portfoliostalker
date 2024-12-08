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

using MudBlazor;

namespace PfsUI.Components;

public partial class DlgStartupWarnings
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    [Parameter] public string Warnings { get; set; }

    protected async Task DlgDumpAsync()
    {
        byte[] zip = Pfs.Account().ExportStorageDumpAsZip(Warnings);

        string fileName = "PfsV2StorageDump_" + DateTime.Today.ToString("yyyyMMdd") + ".zip";
        await BlazorDownloadFileService.DownloadFile(fileName, zip, "application/zip");
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
