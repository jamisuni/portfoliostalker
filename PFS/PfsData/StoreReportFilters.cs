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

using System.Buffers;
using System.Xml.Linq;

using Serilog;

using Pfs.Types;

namespace Pfs.Data;

// Storage of user defined report filters
public class StoreReportFilters : IDataOwner // identical XML on backup & local storage
{
    protected const string _componentName = "filters";
    protected readonly IPfsPlatform _platform;

    protected ReportFilters[] _stored;

    public StoreReportFilters(IPfsPlatform pfsPlatform)
    {
        _platform = pfsPlatform;

        Init();
    }

    protected void Init()
    {
        _stored = Array.Empty<ReportFilters>();
    }

    public string[] List() // Full thing bases to Name's as reference
    {
        return _stored.Select(p => p.Name).ToArray();
    }

    public ReportFilters Get(string name)
    {
        return _stored[Array.FindIndex(_stored, item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))];
    }

    public void Store(ReportFilters filters)
    {
        int pos = Array.FindIndex(_stored, item => item.Name.Equals(filters.Name, StringComparison.OrdinalIgnoreCase));

        if ( pos < 0)                   // ADD
        {
            Array.Resize(ref _stored, _stored.Length + 1);
            _stored[_stored.Length - 1] = filters;
        }
        else if (filters.IsEmpty() )    // DELETE
        {
            var temp = _stored.ToList();
            temp.RemoveAt(pos);
            _stored = temp.ToArray();
        }
        else                            // EDIT
            _stored[pos] = filters;

        EventNewUnsavedContent?.Invoke(this, _componentName);
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnInitDefaults() { Init(); }

    public List<string> OnLoadStorage()
    {
        List<string> warnings = new();

        try
        {
            string stored = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(stored))
            {
                Init();
                return new();
            }

            warnings = ImportXml(stored);
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, OnLoadStorage failed w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        return warnings;
    }

    public void OnSaveStorage() { BackupToStorage(); }

    public string CreateBackup()
    {
        return ExportXml();
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return ExportXml();
    }

    public List<string> RestoreBackup(string content)
    {
        return ImportXml(content);
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, ExportXml());
    }

    protected string ExportXml()
    {
        XElement rootPFS = new XElement("PFS");
        XElement allFiltersElem = new XElement("Filters");
        rootPFS.Add(allFiltersElem);

        foreach (ReportFilters filter in _stored)
        {
            XElement filterElem = filter.GetStorageXml(); // Call is null or "Filter" XElement

            if (filterElem != null && filterElem.Value != null ) 
                allFiltersElem.Add(filterElem);
        }
        return rootPFS.ToString();
    }

    protected List<string> ImportXml(string xml)
    {   // Expected to be called from constructor, so fields should be properly set to defaults
        List<string> warnings = new();
        List<ReportFilters> filters = new();

        try
        { 
            XDocument xmlDoc = XDocument.Parse(xml);
            XElement allFiltersElem = xmlDoc.Element("PFS").Element("Filters");

            foreach ( XElement filterElem in allFiltersElem.Elements() )
                filters.Add(new ReportFilters(filterElem));
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, failed to load filters w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }

        _stored = filters.OrderBy(p => p.Name).ToArray();
        return warnings;
    }
}
