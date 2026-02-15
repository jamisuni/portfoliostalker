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
using System.Collections.ObjectModel;

namespace Pfs.Data.Stalker;

// Raw presentation of specific accounts core data
public class StalkerData
{
    // One account can have multiple portfolios, those each have their own Holdings
    protected List<SPortfolio> _portfolios = new();

    // This list contains all alarm & meta etc information of stocks user is following/tracking
    protected List<SStock> _stocks = new();

    // User sectors definations, giving them name and fields
    protected SSector[] _sectors = new SSector[SSector.MaxSectors];

    public void Init()
    {
        _portfolios = new();
        _stocks = new();
        _sectors = new SSector[SSector.MaxSectors];
    }

    public static void DeepCopy(StalkerData from, StalkerData to)
    {
        to._portfolios = new();
        to._stocks = new();
        to._sectors = new SSector[SSector.MaxSectors];

        foreach (SPortfolio pf in from._portfolios)
            to._portfolios.Add(pf.DeepCopy());

        foreach (SStock s in from._stocks)
            to._stocks.Add(s.DeepCopy());

        for (int i = 0; i < SSector.MaxSectors; i++)
            if (from._sectors[i] != null)
                to._sectors[i] = from._sectors[i].DeepCopy();
    }

    public SPortfolio PortfolioRef(string pfName)
    {
        return _portfolios.SingleOrDefault(p => p.Name == pfName);
    }

    public ref readonly List<SPortfolio> Portfolios()
    {
        return ref _portfolios;
    }

    public ReadOnlyCollection<string> PortfolioSRefs(string pfName)
    {
        SPortfolio pf = _portfolios.SingleOrDefault(p => p.Name == pfName);

        if (pf == null)
            // Should always return at least empty collection, never null, even this major failure
            return new List<string>().AsReadOnly();

        return pf.SRefs.OrderBy(h => h).ToList().AsReadOnly();
    }

    public ReadOnlyCollection<SHolding> PortfolioHoldings(string pfName, string sRef = null)
    {
        SPortfolio pf = _portfolios.SingleOrDefault(p => p.Name == pfName);

        if (pf == null)
            // Should always return at least empty collection, never null, even this major failure
            return new List<SHolding>().AsReadOnly();

        if (string.IsNullOrEmpty(sRef))
            return pf.StockHoldings.OrderBy(h => h.PurhaceDate).ToList().AsReadOnly();
        else
            return pf.StockHoldings.Where(h => h.SRef == sRef).OrderBy(h => h.PurhaceDate).ToList().AsReadOnly();
    }

    public ReadOnlyCollection<SHolding> PortfolioTrades(string pfName, string sRef = null)
    {
        SPortfolio pf = _portfolios.SingleOrDefault(p => p.Name == pfName);

        if (pf == null)
            // Should always return at least empty collection, never null, even this major failure
            return new List<SHolding>().AsReadOnly();

        if (string.IsNullOrEmpty(sRef))
            return pf.StockTrades.AsReadOnly();
        else
            return pf.StockTrades.Where(o => o.SRef == sRef).ToList().AsReadOnly();
    }

    public ReadOnlyCollection<SOrder> PortfolioOrders(string pfName, string sRef = null)
    {
        SPortfolio pf = _portfolios.SingleOrDefault(p => p.Name == pfName);

        if (pf == null)
            // Should always return at least empty collection, never null, even this major failure
            return new List<SOrder>().AsReadOnly();

        if ( string.IsNullOrEmpty(sRef) )
            return pf.StockOrders.AsReadOnly();
        else
            return pf.StockOrders.Where(o => o.SRef == sRef).ToList().AsReadOnly();
    }

    public bool IsPurhaceId(string purhaceId)
    {   // Used to check duplicates, so actually its still duplicate even is sold already
        if (_portfolios.Where(p => p.StockHoldings.Any(h => h.PurhaceId == purhaceId)).SingleOrDefault() != null ||
            _portfolios.Where(p => p.StockTrades.Any(h => h.PurhaceId == purhaceId)).SingleOrDefault() != null)
            return true;
        else
            return false;
    }

    public bool IsTradeId(string tradeId)
    {
        SPortfolio pf = _portfolios.Where(p => p.StockTrades.Any(t => t.Sold.TradeId == tradeId)).SingleOrDefault();

        if (pf == null)
            return false;

        return true;
    }

    public SStock StockRef(string sRef)
    {
        return _stocks.SingleOrDefault(s => s.SRef == sRef);
    }

    public ReadOnlyCollection<SAlarm> StockAlarms(string sRef)
    {
        SStock stock = StockRef(sRef);

        if (stock == null)
            return new List<SAlarm>().AsReadOnly();

        return stock.Alarms.AsReadOnly();
    }

    public ref readonly List<SStock> Stocks()
    {
        return ref _stocks;
    }

    public (string sectorName, string[] fieldNames) GetSector(int i)
    {
        if (_sectors[i] == null)
            return (string.Empty, []);

        return (_sectors[i].Name, _sectors[i].FieldNames);
    }

    public string[] GetStockSectors(string sRef)
    {
        string[] ret = new string[SSector.MaxSectors];

        SStock stock = StockRef(sRef);

        if (stock == null)
            return ret;

        for (int s = 0; s < SSector.MaxSectors; s++)
            if (stock.Sectors[s] >= 0 && _sectors[s] != null )
                ret[s] = _sectors[s].FieldNames[stock.Sectors[s]];

        return ret;
    }
}
