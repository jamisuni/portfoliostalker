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
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using Serilog;

using Pfs.Helpers;
using Pfs.Types;

namespace Pfs.Shared;

public class StoreLatesRates : ILatestRates, ICmdHandler, IDataOwner
{
    protected const string _componentName = "rates";
    protected readonly string storekey = "rates.json";
    protected readonly IPfsPlatform _platform;

    protected class LatestRates
    {
        public CurrencyId HomeCurrency { get; set; } = CurrencyId.Unknown; // This is one-and-only-place to hold / store HomeCurrency
        public DateOnly Date {  get; set; }
        public CurrencyRate[] Rates { get; set; }           // And yes needs to store rates also as each backup maybe different from-where currency set
    }

    protected LatestRates _data;

    protected readonly static ImmutableArray<string> _cmdTemplates = [
        "list"
    ];

    public StoreLatesRates(IPfsPlatform pfsPlatform)
    {
        _platform = pfsPlatform;

        LoadStorageContent();
    }

    protected void Init()
    {
        _data = new()
        {
            Date = DateOnly.MinValue,
            Rates = null, 
            HomeCurrency = CurrencyId.Unknown,
        };
        SetRatesPer(null);
    }

    protected void SetRatesPer(CurrencyRate[] rates)
    {
        List<CurrencyRate> temp = new();

        foreach (CurrencyId currency in Enum.GetValues(typeof(CurrencyId)))
        {
            if (currency == CurrencyId.Unknown)
                continue;

            decimal? value = rates?.SingleOrDefault(c => c.currency == currency)?.rate;

            temp.Add(new(currency, value.HasValue ? value.Value : 0));
        }
        _data.Rates = temp.ToArray();
    }

    public void Store(DateOnly date, CurrencyRate[] rates)
    {
        SetRatesPer(rates);
        _data.Date = date; // must be after init
        EventNewUnsavedContent?.Invoke(this, _componentName);
        // Do not try to send event from there to UI, its useless, its very hard to use on UI
        // as its subdialog that would need it.. and after automatic updates its not need anyway!
    }

    public decimal GetLatest(CurrencyId currency)                                                   // ILatestRates
    {
        CurrencyRate cr = _data.Rates.FirstOrDefault(c => c.currency == currency);

        if (cr == null)
            return 0;

        return cr.rate;
    }

    public (DateOnly date, CurrencyRate[] rates) GetLatestInfo()
    {
        return (date: _data.Date, rates: _data.Rates);
    }

    public CurrencyId HomeCurrency { 
        get { return _data.HomeCurrency; } 
        set
        {
            if ( _data.HomeCurrency == CurrencyId.Unknown && value != CurrencyId.Unknown )
            {
                _data.HomeCurrency = value;
                EventNewUnsavedContent?.Invoke(this, _componentName);
            }
        }
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
            (CurrencyId homeCurrency, DateOnly date, CurrencyRate[] rates) = ImportXml(content);

            if ( homeCurrency == CurrencyId.Unknown )
                return new FailResult($"StoreLatesRates: Failed to get HomeCurrency");

            if (rates != null)
                Store(date, rates);
            else
                Init();

            _data.HomeCurrency = homeCurrency;
            return new OkResult();
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} RestoreBackup failed to exception: [{ex.Message}]");
            return new FailResult($"StoreLatesRates: Exception: {ex.Message}");
        }
    }

    protected void LoadStorageContent()
    {
        try
        {
            string stored = _platform.PermRead(storekey);

            if (string.IsNullOrWhiteSpace(stored) || stored == "{}")
            {
                Init();
                return;
            }

            _data = JsonSerializer.Deserialize<LatestRates>(stored);
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} LoadStorageContent failed to exception: [{ex.Message}]");
            Init();
            _platform.PermRemove(storekey);
        }
    }

    protected void BackupToStorage()
    {
        string dataJson = JsonSerializer.Serialize(_data);

        _platform.PermWrite(storekey, dataJson);
    }

    public string GetCmdPrefixes() { return _componentName; }                                        // ICmdHandler

    public async Task<Result<string>> CmdAsync(string cmd)                                          // ICmdHandler
    {
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        switch (parseResp.Data["cmd"])
        {
            case "list":
                {
                    int i = 0;
                    StringBuilder sb = new();
                    sb.AppendLine($"From: {_data.Date.ToYMD()})");

                    foreach (CurrencyRate r in _data.Rates)
                    {
                        sb.AppendLine($">{r.currency}: {r.rate}");
                        i++;
                    }

                    if (i == 0)
                        return new OkResult<string>("--empty--");
                    else
                        return new OkResult<string>(sb.ToString());
                }
        }
        return new FailResult<string>($"StoreLatesRates unknown command: {parseResp.Data["cmd"]}");
    }

    public async Task<Result<string>> HelpMeAsync(string cmd)                                       // ICmdHandler
    {
        var parseResp = CmdParser.Parse(cmd, _cmdTemplates);

        if (parseResp.Fail) // parser gives per templates a proper fail w help
            return new FailResult<string>((parseResp as FailResult<Dictionary<string, string>>).Message);

        return new OkResult<string>("Cmd is OK:" + string.Join(",", parseResp.Data.Select(x => x.Key + "=" + x.Value)));
    }

    protected string ExportXml()
    {
        XElement rootPFS = new XElement("PFS");

        // Keep this separate as so much more critical information
        XElement homeCurrencyElem = new XElement("HomeCurrency");
        homeCurrencyElem.SetValue(_data.HomeCurrency.ToString());
        rootPFS.Add(homeCurrencyElem);

        // Actual rates go here
        XElement allRatesElem = new XElement("Rates");
        allRatesElem.SetAttributeValue("Date", _data.Date.ToYMD());
        rootPFS.Add(allRatesElem);

        foreach (CurrencyRate cr in _data.Rates)
        {
            XElement crElem = new XElement(cr.currency.ToString());
            crElem.SetAttributeValue("Rate", cr.rate.ToString("0.000000"));
            allRatesElem.Add(crElem);
        }

        return rootPFS.ToString();
    }

    protected (CurrencyId homeCurrency, DateOnly date, CurrencyRate[] rates) ImportXml(string xml)
    {
        CurrencyId homeCurrency = CurrencyId.Unknown;
        XDocument xmlDoc = XDocument.Parse(xml);
        XElement rootPFS = xmlDoc.Descendants("PFS").First();
        XElement hcElem = rootPFS.Element("HomeCurrency");
        if (hcElem == null)
            return (CurrencyId.Unknown, DateOnly.MinValue, null);

        homeCurrency = (CurrencyId)Enum.Parse(typeof(CurrencyId), hcElem.Value);

        try
        {
            XElement allRatesElem = rootPFS.Element("Rates");

            DateOnly date = DateOnly.ParseExact((string)allRatesElem.Attribute("Date"), "yyyy-MM-dd", CultureInfo.InvariantCulture);

            List<CurrencyRate> rates = new();

            foreach (XElement crElem in allRatesElem.Descendants())
            {
                CurrencyId currencyId = (CurrencyId)Enum.Parse(typeof(CurrencyId), crElem.Name.ToString());
                rates.Add(new CurrencyRate(currencyId, (decimal)crElem.Attribute("Rate")));
            }
            return (homeCurrency, date, rates.ToArray());
        }
        catch ( Exception )
        {   // This is still accepted, as really HomeCurrency is only critical information
            return (homeCurrency, DateOnly.MinValue, null);
        }
    }
}
