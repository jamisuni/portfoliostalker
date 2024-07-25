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

using Pfs.Types;
using System.Text;

namespace Pfs.Shared;

// These event's track things are shown user daily as potentially important, like: Triggered Alarms, Order Expires, etc
public class StoreNotes : IDataOwner
{
    const string _separator = "$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$";

    protected const string _componentName = "notes";
    protected readonly string storekey = "stocknotes_"; // ""stocknotes_NYSE_MSFT
    protected readonly IPfsPlatform _platform;          // Later! atm notes are lost if stock market/symbol is changed
    protected IPfsStatus _pfsStatus;

    public StoreNotes(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus)
    {
        _platform = pfsPlatform;
        _pfsStatus = pfsStatus;
    }

    protected void Init()
    {
    }

    public Note Get(string sRef)
    {
        if (_pfsStatus.AllowUseStorage == false)
            return null;

        string content = _platform.PermRead(StoreName(sRef));

        if (string.IsNullOrEmpty(content))
            return null;

        return new Note(content);
    }

    public void Store(string sRef, Note note)
    {
        if (_pfsStatus.AllowUseStorage == false)
            return;

        string content = note.GetStorageFormat();

        if ( string.IsNullOrWhiteSpace(content) == false)
            _platform.PermWrite(StoreName(sRef), note.GetStorageFormat());
        else
            _platform.PermRemove(StoreName(sRef));
    }

    protected string StoreName(string sRef)
    {
        var stock = StockMeta.ParseSRef(sRef);

        return $"{storekey}{stock.marketId}_{stock.symbol}";
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnDataInit() { Init(); }
    public void OnDataSaveStorage() { }

    public string CreateBackup() // All separate notes merged one 'file' w SRef followed w newline plus note content plus special break char
    {
        StringBuilder sb = new();

        foreach (string key in _platform.PermGetKeys().Where(k => k.StartsWith(storekey)))
        {
            string content = _platform.PermRead(key);

            string sRef = key.Replace(storekey, "");

            if (sRef.IndexOf('_') < 0)
                continue;

            sRef = sRef.Replace('_', '$');

            sb.AppendLine(_separator);
            sb.AppendLine(sRef);
            sb.AppendLine(content);
        }
        return sb.ToString();
    }
    
    public string CreatePartialBackup(List<string> symbols)
    {
        return string.Empty;
    }

    public Result RestoreBackup(string content)
    {
        Init();

        if (_pfsStatus.AllowUseStorage == false)
            return new OkResult();

        ClearStorageContent();

        string[] split = content.Split(_separator);

        foreach (string s in split)
        {
            if (string.IsNullOrWhiteSpace(s))
                continue;

            string sub = s.Substring(1); // has extra 

            int pos = sub.IndexOf(Environment.NewLine);

            if ( string.IsNullOrWhiteSpace(sub) || pos < 0) continue;

            string sRef = sub.Substring(0, pos);
            string note = sub.Substring(pos + 1);

            Store(sRef, new Note(note));
        }
        return new OkResult();
    }

    public void ClearStorageContent()
    {
        Init();

        if (_pfsStatus.AllowUseStorage == false)
            return;

        foreach (string key in _platform.PermGetKeys().Where(k => k.StartsWith(storekey)))
            _platform.PermRemove(key);
    }
}
