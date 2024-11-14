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

using System.IO.Compression;
using System.Text;

using Serilog;

using Pfs.Types;

namespace Pfs.Client;

// Separate component to be owner of all 'IDataOwner' as easy to get DI loops 
public class ClientData
{
    protected IPfsStatus _pfsStatus;

    protected bool _unsavedDataStatus = false;
    protected DataOwner[] _dataOwners;

    public ClientData(IEnumerable<IDataOwner> dataOwners, IPfsStatus pfsStatus)
    {
        _pfsStatus = pfsStatus;

        List<DataOwner> tempOwners = new();
        foreach (IDataOwner iDo in dataOwners)
        {
            tempOwners.Add(new DataOwner()
            {
                Name = iDo.GetComponentName(),
                Ref = iDo,
                UnsavedData = false,
            });
            iDo.EventNewUnsavedContent += OnEventNewUnsavedContent;
        }
        _dataOwners = tempOwners.ToArray();
    }

    public void OnEventNewUnsavedContent(object sender, string cName) // 2024-Apr: Keep this like this!
    {   
        // Called by any 'IDataOwner' component when something is pending to be saved
        DataOwner cData = _dataOwners.Single(o => o.Name == cName);
        cData.UnsavedData = true;

        if (_unsavedDataStatus == false)
        {
            _unsavedDataStatus = true;
            _pfsStatus.SendPfsClientEvent(PfsClientEventId.StatusUnsavedData, true);
        }
    }

    public void DoSaveData()
    {
        foreach (DataOwner dataOwner in _dataOwners)
            dataOwner.Ref.OnDataSaveStorage();

        _dataOwners = _dataOwners.Select(o => { o.UnsavedData = false; return o; }).ToArray(); // Later! Atm this 'UnsavedData' is not even used from here!

        _unsavedDataStatus = false;
        _pfsStatus.SendPfsClientEvent(PfsClientEventId.StatusUnsavedData, false);
    }

    public void DoInitDataOwners()
    {
        foreach (DataOwner dataOwner in _dataOwners)
            dataOwner.Ref.OnDataInit();

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.StatusUnsavedData, false);
    }

    public byte[] ExportAccountBackupAsZip()
    {
        var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (DataOwner dataOwner in _dataOwners)
            {
                string backupContent = dataOwner.Ref.CreateBackup();

                if (string.IsNullOrWhiteSpace(backupContent))
                    continue;

                var notesEntry = zip.CreateEntry($"{dataOwner.Name}.txt");
                using (var writer = new StreamWriter(notesEntry.Open(), Encoding.ASCII))
                {
                    writer.WriteLine(backupContent);
                }
            }
            zip.Dispose();
        }
        ms.Seek(0, SeekOrigin.Begin);
        return ms.ToArray();
    }

    public byte[] ExportPartialBackupAsZip(List<string> symbols)
    {
        var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (DataOwner dataOwner in _dataOwners)
            {
                string backupContent = dataOwner.Ref.CreatePartialBackup(symbols);

                if (string.IsNullOrWhiteSpace(backupContent))
                    continue;

                var notesEntry = zip.CreateEntry($"{dataOwner.Name}.txt");
                using (var writer = new StreamWriter(notesEntry.Open(), Encoding.ASCII))
                {
                    writer.WriteLine(backupContent);
                }
            }
            zip.Dispose();
        }
        ms.Seek(0, SeekOrigin.Begin);
        return ms.ToArray();
    }

    public List<string> ImportFromBackupZip(byte[] zip)
    {
        List<string> warnings = new();

        try
        {
            using (var zippedStream = new MemoryStream(zip))
            {
                using (var archive = new ZipArchive(zippedStream))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string name = entry.Name.Replace(".txt", "");

                        using (var unzippedEntryStream = entry.Open())
                        {
                            using (var ms = new MemoryStream())
                            {
                                unzippedEntryStream.CopyTo(ms);
                                var unzippedArray = ms.ToArray();
                                string content = Encoding.ASCII.GetString(unzippedArray);

                                IDataOwner iRefDO = _dataOwners.FirstOrDefault(d => d.Name == name).Ref;

                                List<string> wrns = iRefDO.RestoreBackup(content);

                                warnings.AddRange(wrns);
                            }
                        }
                    }

                    // Later! if conflicts w dependencies then do second loop for ones those depend previous ones..

                }
            }

            _unsavedDataStatus = true;
            _pfsStatus.SendPfsClientEvent(PfsClientEventId.StatusUnsavedData, true);

            return warnings;
        }
        catch (Exception ex)
        {
            Log.Warning($"ImportFromBackupZip failed to exception: [{ex.Message}]");

            DoInitDataOwners();

            warnings.Add($"Close and reopen application plz! ImportFromBackupZip failed to: {ex.Message}");
            return warnings;
        }
    }

    protected struct DataOwner
    {
        public string Name { get; set; }

        public bool UnsavedData { set; get; }

        public IDataOwner Ref { get; set; }
    }
}
