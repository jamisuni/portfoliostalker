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

using System.Collections.Immutable;
using System.Xml.Linq;

namespace Pfs.Types;

public enum ReportId
{
	Unknown,
    Overview,
    Invested,
    PfStocks,
    PfSales,
    Divident,
    ExpHoldings,
    ExpSales,
    ExpDividents,
    Weight,
}

public enum ReportOwningFilter
{
    Unknown,
    Holding,
    Trade,
    Tracking,
}

public interface IReportFilters
{
    bool AllowPF(string pfName);

	bool AllowSector(int sectorId, string field);

    bool AllowMarket(MarketId marketId);

    bool AllowOwning(ReportOwningFilter owning);
}

public enum FilterId
{
    PfName = 0,
    Sector0,
    Sector1,
    Sector2,
    Market,
    Owning,
};

public class ReportFilters : IReportFilters
{
	public const string DefaultTag = "--default--";
    public const string CurrentTag = "--current--";

    // used to separate 'Field's to create Content string
    const char _unitSeparator = ';'; // Wrn! ASCII 31 (0x1F) Unit Separator is illegal char for XML, example attribute values!
                                     // => so using ';' and making its usage illegal for SectorName & SectorFieldName under Validate

    protected static ReportFilters _default = new();

    public static ReportFilters Default { get { return _default; } }


#if false // !!!CODE!!! alternative way to create fixed dictionary
    protected static readonly ReadOnlyDictionary<string, string> _systemParams = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
    {
        ["PfStocks"] = "",
        ["PfStocks2"] = ""
    });
#endif

    protected static readonly ImmutableDictionary<ReportId, FilterId[]> Locked = new Dictionary<ReportId, FilterId[]> // !!!CODE!!! static readonly dictionary
    {   // some reports dont allow customization of specific filter's 
        { ReportId.PfStocks, [FilterId.PfName] },
        { ReportId.PfSales, [FilterId.PfName, FilterId.Owning] },
        { ReportId.Invested, [] },
        { ReportId.Overview, [] },
        { ReportId.Divident, [] },
        { ReportId.ExpHoldings, [] },
        { ReportId.ExpSales, [] },
        { ReportId.ExpDividents, [] },
    }.ToImmutableDictionary();

    public static FilterId[] GetLocked(ReportId report)
    {
        return Locked[report];
    }

    public static ReportFilters Create(string name)
    {
        return new()
        {
            Name = name,
        };
    }

    public string Name { get; internal set; } = string.Empty;

    //****************************************** SHARED GET/SET

    protected string[] _strFilters = new string[Enum.GetNames(typeof(FilterId)).Length];

    protected ReportFilters()
    {
    }

    public string[] Get(FilterId filter)
    {
        return _strFilters[(int)filter]?.Split(_unitSeparator);
    }

    public void Set(FilterId filter, string[] tags)
    {
        _strFilters[(int)filter] = string.Join(_unitSeparator, tags);
    }

    public void Set(FilterId filter, string tag = null)
    {
        _strFilters[(int)filter] = tag;
    }

    //****************************************** PORTFOLIO

    public bool AllowPF(string pfName)
	{
		if ( string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.PfName]))
            return true; // all PF's plz

		if (_strFilters[(int)FilterId.PfName].Split(_unitSeparator).Contains(pfName))
			return true;

		return false;
	}

    //****************************************** SECTORS

    public bool AllowSector(int sectorId, string field)
    {
        string filter = sectorId switch               // !!!CODE!!! switch variant for variable set
        {
            0 => _strFilters[(int)FilterId.Sector0],
            1 => _strFilters[(int)FilterId.Sector1],
            _ => _strFilters[(int)FilterId.Sector2],
        };

        if (string.IsNullOrEmpty(filter))
            return true;

        if (string.IsNullOrWhiteSpace(field))
            return false;

        return filter.Split(_unitSeparator).Contains(field);
    }

    //****************************************** MARKET

    public bool AllowMarket(MarketId marketId)
    {
        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Market]))
            return true;

        if (_strFilters[(int)FilterId.Market].Split(_unitSeparator).Contains(marketId.ToString()))
            return true;

        return false;
    }

    //****************************************** OWNING

    public bool AllowOwning(ReportOwningFilter owning)
    {
        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Owning]))
            return true;

        if (_strFilters[(int)FilterId.Owning].Split(_unitSeparator).Contains(owning.ToString()))
            return true;

        return false;
    }

    //****************************************** 

    public XElement GetStorageXml()
	{
        XElement filterElem = new XElement("Filter");

        if (string.IsNullOrWhiteSpace(Name))
            return null;

        filterElem.SetAttributeValue("Name", Name);

        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.PfName]) == false)
            filterElem.SetAttributeValue("PF", _strFilters[(int)FilterId.PfName]);

        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Sector0]) == false)
            filterElem.SetAttributeValue("S0", _strFilters[(int)FilterId.Sector0]);

        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Sector1]) == false)
            filterElem.SetAttributeValue("S1", _strFilters[(int)FilterId.Sector1]);

        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Sector2]) == false)
            filterElem.SetAttributeValue("S2", _strFilters[(int)FilterId.Sector2]);

        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Market]) == false)
            filterElem.SetAttributeValue("M", _strFilters[(int)FilterId.Market]);

        if (string.IsNullOrWhiteSpace(_strFilters[(int)FilterId.Owning]) == false)
            filterElem.SetAttributeValue("O", _strFilters[(int)FilterId.Owning]);

        return filterElem;
    }

    public ReportFilters(XElement xmlElem)
    {
        if (xmlElem.Attribute("Name") != null)
            Name = (string)xmlElem.Attribute("Name");

        if (xmlElem.Attribute("PF") != null)
            _strFilters[(int)FilterId.PfName] = (string)xmlElem.Attribute("PF");

        if (xmlElem.Attribute("S0") != null)
            _strFilters[(int)FilterId.Sector0] = (string)xmlElem.Attribute("S0");

        if (xmlElem.Attribute("S1") != null)
            _strFilters[(int)FilterId.Sector1] = (string)xmlElem.Attribute("S1");

        if (xmlElem.Attribute("S2") != null)
            _strFilters[(int)FilterId.Sector2] = (string)xmlElem.Attribute("S2");

        if (xmlElem.Attribute("M") != null)
            _strFilters[(int)FilterId.Market] = (string)xmlElem.Attribute("M");

        if (xmlElem.Attribute("O") != null)
            _strFilters[(int)FilterId.Owning] = (string)xmlElem.Attribute("O");
    }

    public bool IsEmpty()
    {
        foreach (FilterId f in Enum.GetValues(typeof(FilterId)))
            if (string.IsNullOrEmpty(_strFilters[(int)f]) == false)
                return false;

        return true;
    }

	public ReportFilters DeepCopy()
	{
		ReportFilters ret = new()
		{
			Name = new string(Name),
            _strFilters = _strFilters.Select(s => string.IsNullOrEmpty(s) ? null : new string(s)).ToArray()
        };
		return ret;
    }
}
