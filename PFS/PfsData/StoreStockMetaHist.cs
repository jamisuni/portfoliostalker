﻿/*
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

using System.Xml.Linq;

using Serilog;

using Pfs.Types;

namespace Pfs.Data;

// Helper class for StoreStockMeta to handle History part of it. Mainly as wants own storage file for this
public class StoreStockMetaHist : IDataOwner // identical XML on backup & local storage
{
    protected const string _componentName = "stockhist";
    protected readonly IPfsPlatform _platform;

    protected StockMetaHist[] _stockHist; // Tracks major changes, like symbol updates or stock closings

    public StoreStockMetaHist(IPfsPlatform pfsPlatform)
    {
        _platform = pfsPlatform;

        Init();
    }

    public void Init()
    {
        _stockHist = Array.Empty<StockMetaHist>();
    }

    public void AppendNewStock(MarketId marketId, string symbol, string companyName, DateOnly date)
    {
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.AddNew, $"{marketId}${symbol}", $"{marketId}${symbol}", date, $"Added new stock, name=[{companyName}]")).ToArray();
    }

    public void AppendUpdateName(MarketId marketId, string symbol, DateOnly date, string companyName)
    {
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.UpdName, $"{marketId}${symbol}", $"{marketId}${symbol}", date, $"Updated to name=[{companyName}]")).ToArray();
    }

    public void AppendUpdateISIN(MarketId marketId, string symbol, DateOnly date, string ISIN)
    {
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.UpdISIN, $"{marketId}${symbol}", $"{marketId}${symbol}", date, $"Updated to ISIN=[{ISIN}]")).ToArray();
    }

    public void AppendUpdateSRef(MarketId updMarketId, string updSymbol, string updCompanyName, MarketId oldMarketId, string oldSymbol, DateOnly date, string comment)
    {
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.UpdSRef, $"{updMarketId}${updSymbol}", $"{oldMarketId}${oldSymbol}", date, $"Updated SREF: {comment}")).ToArray();
    }

    public void AppendSplitStock(MarketId marketId, string symbol, DateOnly date, string comment)
    {
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.Split, $"{marketId}${symbol}", $"{marketId}${symbol}", date, $"Split=[{comment}]")).ToArray();
    }

    public void AppendCloseStock(MarketId oldMarketId, string closeSymbol, DateOnly date, string comment)
    {
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.Close, $"{MarketId.CLOSED}${closeSymbol}", $"{oldMarketId}${closeSymbol}", date, $"Closed: {comment}")).ToArray();
    }

    public void AppendMapping(string fromSymbol, MarketId toMarketId, string toSymbol, string comment)
    {   // This is user mapping, pointing symbol to another existing stock. Using CLOSED as market for mapped fromSymbol as doesnt matter what market it is
        // => Note! On tracking view can create mapping from old/diff symbol to existing SREF and that way automatically handled it as same stock later
        // Only one mapping per symbol, so first remove all old ones
        _stockHist =_stockHist.Where(h => h.Type != StockMetaHistType.UserMap || h.OldSRef != $"{MarketId.CLOSED}${fromSymbol}").ToArray();
        _stockHist = _stockHist.Append(new StockMetaHist(StockMetaHistType.UserMap, $"{toMarketId}${toSymbol}", $"{MarketId.CLOSED}${fromSymbol}", _platform.GetCurrentLocalDate(), comment)).ToArray();
    }

    public string GetMapping(string symbol)
    {
        return _stockHist.FirstOrDefault(h => h.OldSRef == $"{MarketId.CLOSED}${symbol}")?.UpdSRef;
    }

    // Allows to pull full history of symbol changes etc for given SRef -> StockMgmt.History 
    public List<StockMetaHist> GetHistory(string sRef)
    {
        List<StockMetaHist> ret = new(); 
        List<string> relatedSRefs = new() { sRef };

        // Keep spinning all new found dependables until all possible history pulled
        while ( string.IsNullOrEmpty(sRef = LocalAdd(sRef)) == false)
        {
            relatedSRefs.Add(sRef);
        }
        // Shows actual adding day of stock as first, rest are date order (as w import add day may be after some records)
        return ret.Distinct().OrderBy(h => h.Type == StockMetaHistType.AddNew).ThenBy(d => d.Date).ToList();

        // Takes sRef to be checked, and returns new sRef if has unknown found
        string LocalAdd(string sRef)
        {
            List<StockMetaHist> temp = new();
            temp.AddRange(_stockHist.Where(h => h.UpdSRef == sRef));
            temp.AddRange(_stockHist.Where(h => h.OldSRef == sRef));
            ret.AddRange(temp);

            sRef = temp.FirstOrDefault(h => relatedSRefs.Contains(h.UpdSRef) == false)?.UpdSRef;
            if (string.IsNullOrEmpty(sRef) == false)
                return sRef;

            sRef = temp.FirstOrDefault(h => relatedSRefs.Contains(h.OldSRef) == false)?.OldSRef;
            if (string.IsNullOrEmpty(sRef) == false)
                return sRef;

            return null;
        }
    }

    public void DestroyStock(string sRef)
    {
        List<StockMetaHist> hist = GetHistory(sRef);
        List<string> relatedSRefs = new() { sRef };
        relatedSRefs.AddRange(hist.Select(h => h.OldSRef));
        relatedSRefs.AddRange(hist.Select(h => h.UpdSRef));
        relatedSRefs = relatedSRefs.Distinct().ToList();
        List<StockMetaHist> currHist = _stockHist.ToList();
        currHist.RemoveAll(h => relatedSRefs.Contains(h.OldSRef) || relatedSRefs.Contains(h.UpdSRef));
        _stockHist = currHist.ToArray();
    }

#pragma warning disable 0067
    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
#pragma warning restore 0067

    public string GetComponentName() { return _componentName; }
    public void OnInitDefaults() { Init(); }

    public List<string> OnLoadStorage()
    {
        List<string> warnings = new();

        try
        {
            Init();

            string content = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(content))
                return new();

            warnings = ImportXml(content);
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, OnLoadStorage failed w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        return warnings;
    }

    public void OnSaveStorage() {
        _platform.PermWrite(_componentName, ExportXml());
    }

    public string CreateBackup()
    {
        return ExportXml();
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return ExportXml(symbols);
    }

    public List<string> RestoreBackup(string content)
    {
        return ImportXml(content);
    }

    protected string ExportXml(List<string> symbols = null)
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement stockHistElem = new XElement("StockMetaHist");
        rootPFS.Add(stockHistElem);

        foreach (StockMetaHist sh in _stockHist)
        {
            if (symbols == null ||
                symbols.Contains(StockMeta.ParseSRef(sh.OldSRef).symbol) ||
                symbols.Contains(StockMeta.ParseSRef(sh.UpdSRef).symbol) )
            {
                XElement shElem = new XElement("D" + sh.Date.ToYMD());
                shElem.SetAttributeValue("Type", sh.Type.ToString());
                shElem.SetAttributeValue("Upd", sh.UpdSRef);
                shElem.SetAttributeValue("Old", sh.OldSRef);
                shElem.SetAttributeValue("Note", sh.Note);
                stockHistElem.Add(shElem);
            }
        }
        return rootPFS.ToString();
    }

    protected List<string> ImportXml(string xml)     // string newSRef, string oldSRef, DateOnly date, string comment
    {
        List<string> warnings = new();
        List<StockMetaHist> hist = new();

        try
        {
            XDocument xmlDoc = XDocument.Parse(xml);
            XElement rootPFS = xmlDoc.Descendants("PFS").First();
            XElement stockHistElem = rootPFS.Element("StockMetaHist");

            foreach (XElement shElem in stockHistElem.Descendants())
            {
                DateOnly date = DateOnlyExtensions.ParseYMD(shElem.Name.ToString().Substring(1));
                StockMetaHistType type = Enum.Parse<StockMetaHistType>((string)shElem.Attribute("Type"));
                string upd = (string)shElem.Attribute("Upd");
                string old = (string)shElem.Attribute("Old");
                string note = (string)shElem.Attribute("Note");
                hist.Add(new StockMetaHist(type, upd, old, date, note));
            }
        }
        catch (Exception ex)
        {
            string wrnmsg = $"{_componentName}, failed to load history info w exception [{ex.Message}]";
            warnings.Add(wrnmsg);
            Log.Warning(wrnmsg);
        }
        _stockHist = hist.ToArray();
        return warnings;
    }
}
