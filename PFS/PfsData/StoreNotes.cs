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

using System.Text;

using Pfs.Types;

namespace Pfs.Data;

// These event's track things are shown user daily as potentially important, like: Triggered Alarms, Order Expires, etc
public class StoreNotes : IDataOwner, IStockNotes
{
    const string _separator = "$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$";

    protected const string _componentName = "notes";
    protected const string prefixstorekey = "stocknotes_"; // ""stocknotes_NYSE_MSFT
    protected readonly IPfsPlatform _platform;          // Later! atm notes are lost if stock market/symbol is changed
    protected IPfsStatus _pfsStatus;

    protected Dictionary<string, string> _cacheHeaders = new();

    /* !!!DOCUMENT!!! Notes
     * - Each stock can have its own user specific notes, those can be seen/edited under StockMgmt
     * - These are really preferred to be max 10 lines or so, longer notes should be kept elsewhere
     * - Stocks note itself is stored as own file stocknotes_NYSE_MSFT, and not hold on memory
     * - Demo doesnt support notes
     * - If first line of note starts with '>' then  its assume as header:
     *      - Header's is just single line (to first eod/endoffile), kind of personal highlights for stock
     *      - Limiting is as 120 characters maximum
     *      - Its cached for fast fetch as reports can/should have it available
     *      - User can add it simply on note editing by just adding line
     * 
     * Later!
     * - Secondary book keeping, by also storing cached headers, and using it as index to double know what notes has currently
     * - Going to create own custom importing, someday.. that allows to bring highlights from longer notes to PFS notes
     * - Going to have "public" part on custom importing, for.. later...
     */

    public StoreNotes(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus)
    {
        _platform = pfsPlatform;
        _pfsStatus = pfsStatus;

        Init();
    }

    protected void Init()
    {
        _cacheHeaders = new();

        foreach (string skey in _platform.PermGetKeys().Where(k => k.StartsWith(prefixstorekey)))
        {
            string sRef = StoreName2SRef(skey);
            Note note = Get(sRef);
            if (note == null)
                continue;

            string hdr = note.GetHeader();

            if (hdr == null)
                continue;

            AddHeader(sRef, hdr);
        }
    }

    public string GetHeader(string sRef)
    {
        if (_cacheHeaders.ContainsKey(sRef))
            return _cacheHeaders[sRef];

        return string.Empty;
    }

    protected void AddHeader(string sRef, string hdr)
    {
        if ( string.IsNullOrWhiteSpace(hdr) )
        {
            if (_cacheHeaders.ContainsKey(sRef))
                _cacheHeaders.Remove(sRef);
        }
        else
        {
            if (_cacheHeaders.ContainsKey(sRef))
                _cacheHeaders[sRef] = hdr;
            else
                _cacheHeaders.Add(sRef, hdr);
        }
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

        if (string.IsNullOrWhiteSpace(content) == false)
        {
            _platform.PermWrite(StoreName(sRef), content);

            AddHeader(sRef, note.GetHeader());
        }
        else
            _platform.PermRemove(StoreName(sRef));

        EventNewUnsavedContent?.Invoke(this, _componentName);
    }

    protected static string StoreName(string sRef)
    {
        var stock = StockMeta.ParseSRef(sRef);

        return $"{prefixstorekey}{stock.marketId}_{stock.symbol}";
    }

    protected static string StoreName2SRef(string skey) 
    {
        return skey.Replace(prefixstorekey, "").Replace('_', '$');
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnDataInit() { Init(); }
    public void OnDataSaveStorage() { }

    public string CreateBackup() // All separate notes merged one 'file' w SRef followed w newline plus note content plus special break char
    {
        StringBuilder sb = new();

        foreach (string skey in _platform.PermGetKeys().Where(k => k.StartsWith(prefixstorekey)))
        {
            string content = _platform.PermRead(skey);

            string sRef = StoreName2SRef(skey);

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

        foreach (string skey in _platform.PermGetKeys().Where(k => k.StartsWith(prefixstorekey)))
            _platform.PermRemove(skey);
    }
}
