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
public class StoreReportFilters : IDataOwner
{
    protected const string _componentName = "filters";
    protected readonly IPfsPlatform _platform;

    protected ReportFilters[] _stored;

    public StoreReportFilters(IPfsPlatform pfsPlatform)
    {
        _platform = pfsPlatform;

        LoadStorageContent();
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
    public void OnDataInit() { Init(); }
    public void OnDataSaveStorage() { BackupToStorage(); }

    public string CreateBackup()
    {
        return ExportXml();
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return ExportXml();
    }

    public Result RestoreBackup(string content)
    {
        try
        {
            _stored = ImportXml(content);

            return new OkResult();
        }
        catch (Exception ex)
        {   // Not handled as critical error, as yes params are lost but hopefully nothing else not...
            Log.Warning($"{_componentName} RestoreBackup failed to exception: [{ex.Message}]");
            return new OkResult();
        }
    }

    protected void LoadStorageContent()
    {
        try
        {
            string stored = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(stored))
            {
                Init();
                return;
            }

            _stored = ImportXml(stored);
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} LoadStorageContent failed to exception: [{ex.Message}]");
            Init();
            _platform.PermRemove(_componentName);
        }
    }

    protected void BackupToStorage()
    {
        _platform.PermWrite(_componentName, CreateBackup());
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

    protected ReportFilters[] ImportXml(string xml)
    {   // Expected to be called from constructor, so fields should be properly set to defaults

        XDocument xmlDoc = XDocument.Parse(xml);
        XElement allFiltersElem = xmlDoc.Element("PFS").Element("Filters");

        List<ReportFilters> ret = new();

        foreach ( XElement filterElem in allFiltersElem.Elements() )
            ret.Add(new ReportFilters(filterElem));

        return ret.OrderBy(p => p.Name).ToArray();
    }
}
