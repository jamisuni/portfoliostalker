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

using System.Collections.ObjectModel;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

using BlazorDownloadFile;
using Pfs.Types;
using Serilog;
using System.Text; // https://github.com/arivera12/BlazorDownloadFile

namespace PfsUI.Components;

// Dialog: AccountImport, allows user to import notes, or full account backup.
public partial class DlgImport
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    public enum ImportType : int
    {
        Unknown = 0,
        AccountBackupZip,
        CompaniesTxt,
        StockNotesTxt,
        WaveAlarms,
    }

    protected static readonly ReadOnlyDictionary<ImportType, DlgImportCfg> _configs = new ReadOnlyDictionary<ImportType, DlgImportCfg>(new Dictionary<ImportType, DlgImportCfg>
    {
        [ImportType.AccountBackupZip] = new DlgImportCfg()
        {
            ContentTxt = false,
            ImportNote = "Pfs2Export_YYYYMMDD.zip. This is restoring previous full account backup status to account." +
                         "Please make sure you backup original situation first, as all gets wiped clean on progress.",
            ImportWarning = "Warning! This is going to remove all existing data, please be carefull!",
        },
        [ImportType.CompaniesTxt] = new DlgImportCfg()
        {
            ContentTxt = true,
            ImportNote = "Allows to bring company meta as txt file w lines: MarketId,Symbol,CompanyName",
            ImportWarning = null,
        },
        [ImportType.StockNotesTxt] = new DlgImportCfg()
        {
            ContentTxt = true,
            ImportNote = "Used to import manually edited stock notes (highlights). Start of start line needs to be [#NYSE$TSN#>... " +
                         "and text area on txt file needs to end #] as start of line. That > there marks header that shown reports.",
            ImportWarning = "Warning! All previous Stock Note descriptions are going to be replaced with ones from this txt file",
        },
        [ImportType.WaveAlarms] = new DlgImportCfg()
        {
            ContentTxt = true,
            ImportNote = "Find from file ~$NYSE$AFL~33.7~BUY 30pcs~ and ~$ABCL~SELL~6.02~Sell 260~" + Environment.NewLine +
                         "type alarms, and replaces existing alarms of stock with these.",
            ImportWarning = "Warning! Removes ALL previous basic buy/sell alarms from effected stocks!",
        }
    });

    protected ImportType _importType { get; set; } = ImportType.Unknown;

    IBrowserFile _selectedFile = null; // This is Microsoft provided, no nuget's required

    List<ImportType> _supportedImports = null;

    protected bool _showBusySignal = false;
    protected override void OnInitialized()
    {
        _supportedImports = [
            ImportType.AccountBackupZip, 
            ImportType.CompaniesTxt,
            ImportType.WaveAlarms,
            ImportType.StockNotesTxt];
    }

    protected void OnImportTypeChanged(ImportType type)
    {
        _importType = type;
        _selectedFile = null;
        StateHasChanged();
    }

    private void OnInputFileChange(InputFileChangeEventArgs e)
    {
        // Note: Picky, hides if different format: Use: <InputFile OnChange="@OnInputFileChange" accept=".txt"></InputFile>

        // http://www.binaryintellect.net/articles/06473cc7-a391-409e-948d-3752ba3b4a6c.aspx
        _selectedFile = e.File;
        this.StateHasChanged();
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    protected async Task DlgOkAsync()
    {
        bool ret = true;
        string failedMsg = "Something is wrong looks like? yes, something wrong...";

        if (_selectedFile == null)
        {
            bool? result = await Dialog.ShowMessageBox("Failed!", "Hmm, I thnk I need file for this? Could you select one for me plz?", yesText: "Ok");
            return;
        }

        if (_importType == ImportType.AccountBackupZip)
        {
            try
            {
#if false                                                                           // !!!LATER!!! This is backup of old before runnig it over w given one..
                byte[] zip = PfsClientAccess.Account().ExportAccountAsZip();
                string fileName = "PfsExport_" + DateTime.Today.ToString("yyyyMMdd") + ".zip";
                await BlazorDownloadFileService.DownloadFile(fileName, zip, "application/zip");
#endif
            }
            catch (Exception)
            {
                // !!!LATER!!! Should really make this work always, but some messed up cases we need import even export doesnt work...
            }
        }

        _showBusySignal = true;

        MemoryStream ms = new MemoryStream();
        Stream stream = _selectedFile.OpenReadStream(2000000); // Default is just 512K, needs bit more...
        await stream.CopyToAsync(ms);

        if (_configs[_importType].ContentTxt == false)
        {
            // Note! Atm only bin/zip format supported is out very own PfsExport.. so has dedicated function 

            // This cleans all user data locally, imports new tuff per file, and backups locally.. so all replaced!
            Result importResult = Pfs.Account().ImportAccountFromZip(ms.ToArray());

            if (importResult.Ok == false)
            {

                ret = false;
            }

        }
        else if (_importType == ImportType.WaveAlarms)
        {
            string content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            WaveAlarmImport(content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        }
        else if (_importType == ImportType.StockNotesTxt)
        {
            string content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            StockNotesImport(content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        }
        else if (_importType == ImportType.CompaniesTxt)
        {
            string content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            string line = "";

            try
            {
                using (StringReader reader = new StringReader(content))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length < 3)
                            continue;

                        string ISIN = string.Empty;

                        if (parts.Length == 4)
                            ISIN = parts[3];

                        if (Pfs.Stalker().AddNewStockMeta(Enum.Parse<MarketId>(parts[0]), parts[1].ToUpper(), parts[2], ISIN) == null)
                        {
                            failedMsg = $"Failed to add line: [{line}]";
                            ret = false;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failedMsg = $"Failed to line: [{line}] as ex [{ex.Message}]";
                ret = false;
            }
        }
        else
        {
#if false
            string result = System.Text.Encoding.UTF8.GetString(ms.ToArray());

            ret = await PfsClientAccess.Account().ImportTextContentAsync(_importType, result);
#endif
        }

        _showBusySignal = false;

        if (ret == true)
        {
            if (_importType == ImportType.AccountBackupZip)
            {
                // forceLoad as all data is replaced, so easier just store backup and do normal startup loading 
                Pfs.Account().SaveData();
                NavigationManager.NavigateTo(NavigationManager.BaseUri, true);
            }
            else
                MudDialog.Close();
        }
        else
        {
            bool? result = await Dialog.ShowMessageBox("Failed!", failedMsg, yesText: "Ok");
        }
    }

    protected struct DlgImportCfg
    {
        public string ImportNote { get; set; }
        public string ImportWarning { get; set; }
        public bool ContentTxt { get; set; }
    }

    protected void StockNotesImport(string[] allInputLines)
    {
        foreach ( KeyValuePair<string, Note> kvp in Local_Parse(allInputLines))
            Pfs.Account().StoreNote(kvp.Key, kvp.Value);
        return;

        Dictionary<string, Note> Local_Parse(string[] allInputLines)
        {
            Dictionary<string, Note> ret = new();

            for (int linePos = 0; linePos < allInputLines.Length; linePos++)
            {
                if (allInputLines[linePos].StartsWith("[#") == false)
                    continue;

                (string sRef, Note note, string errMsg) = Note.ParseExportFormat(ref allInputLines, linePos);

                if ( string.IsNullOrEmpty(errMsg) == false )
                {
                    Log.Warning($"StockNotesImport() failed to parse [{errMsg}]");
                    continue;
                }

                (MarketId marketId, string symbol) = StockMeta.ParseSRef(sRef);

                if (Pfs.Stalker().GetStockMeta(marketId, symbol) == null)
                {
                    Log.Warning($"StockNotesImport() has note for unknown stock {sRef}");
                    continue;
                }

                ret.Add(sRef, note);
            }
            return ret;
        }
    }

    protected record WAlarm(SAlarmType type, decimal lvl, string note);

    protected void WaveAlarmImport(string[] allInputLines)
    {
        Dictionary<string, List<WAlarm>> alarms = Local_ParseAlarmInput(allInputLines);

        foreach (KeyValuePair<string, List<WAlarm>> kvp in alarms)
        {
            (MarketId marketId, string symbol) = StockMeta.ParseSRef(kvp.Key);

            List<SAlarm> existings = Pfs.Stalker().StockAlarmList(marketId, symbol).ToList();

            foreach (SAlarm ex in existings)
            {   // Removing all existing BUY/SELL alarms
                if (ex.AlarmType != SAlarmType.Over && ex.AlarmType != SAlarmType.Under)
                    continue;

                Pfs.Stalker().DoAction($"Delete-Alarm SRef=[{kvp.Key}] Level=[{ex.Level}]");
            }

            foreach (WAlarm add in kvp.Value)
            {
                Result stalkerResult = Pfs.Stalker().DoAction($"Add-Alarm Type=[{add.type}] SRef=[{kvp.Key}] Level=[{add.lvl}] Prms=[] Note=[{add.note}]");

                if (stalkerResult.Fail)
                {
                    Log.Warning($"ImportWaveAlarms() failed to add {kvp.Key} w level={add.lvl}");
                }
            }
        }
        return;

        Dictionary<string, List<WAlarm>> Local_ParseAlarmInput(string[] allInputLines)
        {
            Dictionary<string, List<WAlarm>> ret = new();

            // cmd = $"Add-Alarm  Type=[{_editType}] SRef=[{Market}${Symbol}] Level=[{_editLevel}] Prms=[] Note=[{_editNote}]";

#if false   // !!!TODO!!! On future just simple EMPTY/BUY/SELL wo date... but as long V1 on use keep also old syntax 
        
        ~$NYSE$AFL~33.7~BUY 30pcs~
        ~$AFL~BUY~33.7~BUY 30pcs~
        ~$ABCL~SELL~6.02~Sell 260~

        ~$ABCL~AAOP~6.02~Sell 260~SET 2023-07-19~
        ~$ABCL~3.72~BUY 1.5KE~SET 2023-12-22~

#endif

            foreach (string inputLine in allInputLines)
            {
                string line = inputLine.TrimStart().TrimEnd();

                if (line.Contains("~$") == false)
                {   
                    if ( line.Count(c => c == '~') >= 4 )
                    {   // easy to forgot it has to start ~$ also when giving market, so lets give warning from potentials
                        Log.Warning($"ImportWaveAlarms() was this supposed to have alarm: {line}");
                    }
                    continue;
                }

                line = line.Substring(line.IndexOf("~$") + 2);

                int lastPos = line.LastIndexOf('~');

                if (lastPos <= 0)
                    continue;

                line = line.Substring(0, lastPos);

                string[] split = line.Split('~');
                int splitCount = split.Count();

                if (split.Last().StartsWith("SET "))
                    splitCount--;

                if (splitCount < 3 || splitCount > 4) // Later! Just allow 3 pieces
                    continue;

                MarketId marketId = MarketId.Unknown;
                string symbol;
                SAlarmType type;
                string note;
                decimal lvl;

                if (split[0].IndexOf('$') > 0)
                    (marketId, symbol) = StockMeta.ParseSRef(split[0]);
                else
                    symbol = split[0];

                if (Validate.Str(ValidateId.Symbol, split[0]).Fail)
                    continue;

                StockMeta sm;

                if (marketId != MarketId.Unknown)
                    sm = Pfs.Stalker().GetStockMeta(marketId, symbol);
                else
                    sm = Pfs.Stalker().FindStock(symbol);

                if (sm == null)
                { Log.Warning($"ImportWaveAlarms() could not find/identify {marketId}${symbol}, ignored {line}"); continue; }

                symbol = sm.symbol;
                marketId = sm.marketId;
                int pos = 2;
                switch (split[1].ToUpper())
                {
                    case "AAOP":
                    case "SELL": type = SAlarmType.Over; break;
                    case "BUY": type = SAlarmType.Under; break;
                    default: type = SAlarmType.Under; pos = 1; break;
                }

                if (decimal.TryParse(split[pos], out lvl) == false)
                { Log.Warning($"ImportWaveAlarms() expecting decimal {split[pos]}, ignored {line}"); continue; }

                note = split[pos + 1];

                if (ret.ContainsKey($"{marketId}${symbol}") == false)
                    ret.Add($"{marketId}${symbol}", new());

                ret[$"{marketId}${symbol}"].Add(new WAlarm(type, lvl, note));
            }
            return ret;
        }
    }
}