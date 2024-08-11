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
using Microsoft.AspNetCore.Components;

using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;
using Pfs.Data.Stalker;
using Pfs.Types;
using Pfs.ExtTransactions;
using BlazorDownloadFile;

namespace PfsUI.Components;

// Complicated piece of multi-step UI, allowing user to import external CSV etc records from Bank/Broker to PFS 
public partial class ImportTransactions
{
    [Inject] IDialogService Dialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    protected enum ImportFormats
    {
        Unknown,
        Nordnet,
    };

    protected Progress _progress = Progress.PreConversion;
    protected MudExpansionPanels _expPanels;

    protected List<string> _portfolios; // 4/5 has portfolio selection of single pf where holdings are to be added
    protected string _selPortfolio;

    protected enum Progress
    {
        PreConversion,          // 1/5 User to select provider
                                // 2/5 Import raw data from csv file
        ViewAll,                // 3/5 Conversion w broker provider code, and processing information to present parsing status
                                //     plus add companies to account those are not yet followed but has now records on importing
        ViewTestRun,            // 4/5 Per try run w stalker updates ones going forward either to OK, or duplicates etc
        ViewResultLog,          // 5/5 Textual log presenting what happened on conversion, includes even some rejected actions
    }

    protected string _origCsvHeaderLine = string.Empty;

    protected override void OnInitialized()
    {
        _portfolios = Pfs.Stalker().GetPortfolios().Select(p => p.Name).ToList();
    }

    protected string Convert2RawString(byte[] rawByteData)
    {
        switch (_selFormat)
        {
            case ImportFormats.Nordnet:
                return BtNordnet.Convert2RawString(rawByteData);
        }
        return string.Empty;
    }

    protected BtNordnet _nordnet = new();

    protected Result<List<BtAction>> Convert2BtActions(byte[] rawByteData)
    {
        switch (_selFormat)
        {
            case ImportFormats.Nordnet:
                return _nordnet.InitAndConvert(rawByteData, Pfs.Config().HomeCurrency);
        }
        return null;
    }

    protected string Convert2Debug(string line, Transaction ta)
    {
        switch (_selFormat)
        {
            case ImportFormats.Nordnet:
                return _nordnet.Convert2Debug(line, ta);
        }
        return null;
    }

    protected void UpdateMissingRate(BtAction bta, decimal currencyRate)
    {
        if (bta.Status != BtAction.TAStatus.MisRate)
            return;

        switch (_selFormat)
        {
            case ImportFormats.Nordnet:
                if (_nordnet.AddMissingRate(bta, currencyRate) )
                    bta.Status = BtAction.TAStatus.Acceptable;
                break;

            default:
                return;
        }
    }

    // Different statistics and orig contents captured for final report
    protected int _repTotalTAs = 0;
    protected int _repIgnoredTAs = 0;
    protected int[] _repAdded = new int[Enum.GetValues(typeof(TaType)).Cast<int>().Max()+1];
    protected List<string> _repFailedConv = new();
    protected List<string> _repRejected = new();
    protected List<string> _repManual = new();
    protected int _repFailedDupl = 0;
    protected List<string> _repFailedUnit = new();
    protected List<string> _repFailedTest = new();

    /* Transaction Import Flow: (!Split small functions!)
     * 
     * PreConversion: 
     * 
     * 1) Select Import Format
     * 2) Import '_rawByteData' from file
     * 
     * ViewAll-Init: (CSV -> BTA's, and list of Companies)
     * 3) Parse CSV file to create bta's per Nordnet etc CSV files, line-by-line
     * 4) Validate Transactions to have proper ranged information for stalker use
     * 5) From Broker Transactions find all different broker provided companies, for company list
     * 6) Check against user's existing companies to see if can automatically map bta's to them
     * 
     * ViewAll-Repeat:
     * 7) Show all Broker Transactions, grouped by MapCompRef (==broker company, NOT by StockMeta)
     * 
     * ViewAll-AddCompanies
     * 8) Allow launch dialog to automatically/manually assign records to companies, and add new tracked stocks for records
     * 9) Update bta's with matched/added StockMeta's 
     */

    // 1) Select Import Format
    ImportFormats _selFormat = ImportFormats.Unknown;

    IBrowserFile _selectedFile = null;  // This is Microsoft provided, no nuget's required
    byte[] _rawByteData = null;         // <= stays same on whole time w original content unmodified

    // 2) Import '_rawByteData' from file
    private async Task OnInputFileChangeAsync(InputFileChangeEventArgs e) 
    {
        _selectedFile = e.File;

        MemoryStream ms = new MemoryStream();
        Stream stream = e.File.OpenReadStream(maxAllowedSize: 1024 * 1024);
        await stream.CopyToAsync(ms);

        _rawByteData = ms.ToArray();

        _viewRawTextual = Convert2RawString(_rawByteData);
        _origCsvHeaderLine = _viewRawTextual.Substring(0, _viewRawTextual.IndexOf(Environment.NewLine));

        StateHasChanged();
    }

    protected string _viewRawTextual = null; // Initially used to show raw CSV 2/5, and on last step 5/5 a report log
    List<BtAction> _brokerTransactions = null;  // this is list of actions under processing, created by Nordnet conversion etc

    protected Dictionary<string, TaMapCompany> _targetCompanies = null; // holds brokers company info, and mapping to StockMeta if matched

    protected class TaMapCompany : MapCompany   // Expanding map company to allow some own state info for company
    {                                           // this is place for states as _viewByCompany gets recreated all the time
        public bool Flagged { get; set; } = false;

        public TestState Test = TestState.UnTested;
        public enum TestState { UnTested = 0, TestOk = 1, Failed = 2 }

        public List<string> AcceptableActionSet = null;
    }

    private IEnumerable<string> _typeFilterSelection { get; set; } = new HashSet<string>();
    protected List<string> _typeFilterAll = new(); // Buy, Sell, BrokerTypes those shown on filter menu, step 3/5

    protected string SelTypes(List<string> types)
    {   // Always just 'Type Filter' as text
        ViewAllPerCompany();
        StateHasChanged();
        return "Type Filter";
    }

    protected TaMapCompany CreateMapCompany(Transaction ta)
    {
        return new TaMapCompany()
        {   // Each company from broker just ones
            ExtMarketId = ta.Market,
            ExtSymbol = ta.Symbol,
            ExtCompanyName = ta.CompanyName,
            ExtISIN = ta.ISIN,
            ExtMarketCurrencyId = ta.Currency,
            // and sm=null mark's that its not mapped yet users companies
            StockMeta = null,
            // 3/5 has few extra features to support handling of large csvs
            Flagged = false,
            Test = TaMapCompany.TestState.UnTested,
        };
    }

    protected void OnBtnExpandAll()
    {
        _expPanels.ExpandAll();
    }

    protected void OnBtnCollapseAll()
    {
        _expPanels.CollapseAll();
    }

    protected async Task OnBtnExportFlaggedAsync()
    {
        List<string> flaggedMapCompRefs = GetFlaggedMapCompRefs();

        if (flaggedMapCompRefs.Count <= 0)
            return;

        StringBuilder sb = new();
        sb.Append(_origCsvHeaderLine);

        // Then loop all TA's and export those w flagged companies
        foreach (KeyValuePair<string, List<ViewBtAction>> kvp in _viewByCompany)
        {
            foreach ( ViewBtAction vBta in kvp.Value )
            {
                if (flaggedMapCompRefs.Contains(vBta.bta.MapCompRef) )
                    sb.AppendLine(vBta.bta.Orig);
            }
        }

        string fileName = "Pfs2Flagged_" + DateTime.Today.ToString("yyyyMMdd") + ".txt";
        await BlazorDownloadFileService.DownloadFileFromText(fileName, sb.ToString(), Encoding.Unicode, "text/csv");
    }

    protected void OnBtnDeleteFlagged()
    {
        List<string> flaggedMapCompRefs = GetFlaggedMapCompRefs();
        _brokerTransactions.RemoveAll(a => flaggedMapCompRefs.Contains(a.MapCompRef));

        ViewAllPerCompany();
        StateHasChanged();
    }

    protected void OnBtnFlagUnmapped()
    {
        _targetCompanies.Values.Where(c => c.StockMeta == null).ToList().ForEach(s => s.Flagged = true);
    }

    protected List<string> GetFlaggedMapCompRefs()
    {
        List<string> flaggedMapCompRefs = new();
        foreach (KeyValuePair<string, TaMapCompany> kvp in _targetCompanies)
        {
            if (kvp.Value.Flagged)
                flaggedMapCompRefs.Add(kvp.Key);
        }
        return flaggedMapCompRefs;
    }

    private async Task OnBtnConvertFromRawAsync() // From: PreConversion (view 'raw' data) ==> ViewAll (including transactions with errors)
    {
        if (_selFormat == ImportFormats.Unknown || _rawByteData == null )
            return;

        // => _rawByteData

        if (Local_ConvertRawData2BrokerTransactions() == false )
        {
            await Dialog.ShowMessageBox("Failed!", "Conversion of content failed with selected broker/content format", yesText: "Ok");
            return;
        }

        // => BtActions

        Local_ValidateTransactions();

        // => _etRecords with 'ProcessingStatus' set

        Local_ListAllTargetCompanies();

        // => _targetCompanies with 'all companies' those have any record effecting them

        Local_MapCompany2UserStockMetas();

        // => Add StockMeta to those _targetCompanies that can be found from User's stocks by Symbol

        _progress = Progress.ViewAll;
        ViewAllPerCompany(); // Finishes initial conversion, and shows records on minimal processed format to user
        return;

        // 3) Parse CSV file to create bta's per Nordnet etc CSV files, line-by-line
        bool Local_ConvertRawData2BrokerTransactions()
        {
            try
            {
                Result<List<BtAction>> convResult = Convert2BtActions(_rawByteData);

                if ( convResult.Fail )
                    throw new Exception((convResult as FailResult<List<BtAction>>).Message);

                foreach (TaType type in Enum.GetValues(typeof(TaType)))
                    if ( type != TaType.Unknown)
                        _typeFilterAll.Add(type.ToString());

                _brokerTransactions = new();

                foreach (BtAction bta in convResult.Data)
                {
                    _repTotalTAs++;

                    // keep reference key to company on bta to speed up things
                    MapCompany mp = CreateMapCompany(bta.TA);
                    bta.MapCompRef = mp.MapCompRef();

                    if ( string.IsNullOrEmpty(bta.MapCompRef))
                    {   // Any TA's those dont target company we fully reject atm (not shown even 3/5)
                        _repRejected.Add(bta.Orig);
                        continue;
                    }

                    // set initial status for bta. 'Acceptable' for potential ones
                    if (string.IsNullOrEmpty(bta.ErrMsg) == false)
                    {
                        bta.Status = BtAction.TAStatus.ErrConversion;
                        _repFailedConv.Add(bta.Orig);
                    }
                    else if (bta.TA.Action == TaType.Unknown)
                    {
                        bta.Status = BtAction.TAStatus.Ignored;
                        _repIgnoredTAs++;
                    }
                    else if (bta.TA.IsRateMissing())
                    {
                        bta.Status = BtAction.TAStatus.MisRate;
                    }
                    else if ( bta.TA.Action == TaType.Close)
                    {
                        bta.Status = BtAction.TAStatus.Manual;
                    }
                    else
                        bta.Status = BtAction.TAStatus.Acceptable;

                    if (bta.TA.Action == TaType.Unknown && _typeFilterAll.Contains(bta.BrokerAction) == false )
                        // Addition to buy,sell,divident.. we also add filter component all broker names of unknowns
                        _typeFilterAll.Add(bta.BrokerAction);

                    _brokerTransactions.Add(bta);
                }

                // Filter on 3/5 always starts w all selected
                _typeFilterSelection = new HashSet<string>(_typeFilterAll);
                return true;
            }
            catch (Exception e)
            {
                _viewRawTextual = $"Failed to Exception! Conversion() with selected provider failed. Msg=[{e.Message}]";
                _progress = Progress.ViewResultLog;
                return false;
            }
        }

        // 4) Validate Transactions to have proper ranged information for stalker use
        void Local_ValidateTransactions()
        {
            // Recreate this always if needs validation, as Stalker gets updated per user actions
            StalkerDoCmd stalkerCopy = Pfs.Stalker().GetCopyOfStalker();

#if false
            foreach (BtAction entry in _brokerTransactions)
                StalkerAddOn_ExtTransactions.Validate(entry, stalkerCopy);                                      // !!!TODO!!!
#endif
        }

        // 5) From Broker Transactions find all different broker provided companies, for company list
        void Local_ListAllTargetCompanies()
        {
            _targetCompanies = new();

            foreach (BtAction bta in _brokerTransactions)
            {
                if (string.IsNullOrWhiteSpace(bta.TA.ISIN) == false)
                {   // ISIN is unique, so if thats given then thats what we use
                    if (_targetCompanies.Values.Any(c => c.ExtISIN == bta.TA.ISIN) == false)
                    {
                        TaMapCompany tmc = CreateMapCompany(bta.TA);
                        _targetCompanies.Add(tmc.MapCompRef(), tmc);
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bta.TA.Symbol) == true && string.IsNullOrWhiteSpace(bta.TA.CompanyName) == true)
                    continue; // missing basic info, No ISIN/Symbol/Name -> NO-GO

                if (string.IsNullOrWhiteSpace(bta.TA.Symbol) == false && _targetCompanies.Values.Any(c => c.ExtSymbol == bta.TA.Symbol) == true)
                    continue; // got this symbol already

                if (string.IsNullOrWhiteSpace(bta.TA.CompanyName) == false && _targetCompanies.Values.Any(c => c.ExtCompanyName == bta.TA.CompanyName) == true)
                    continue;

                TaMapCompany mc = CreateMapCompany(bta.TA);
                _targetCompanies.Add(mc.MapCompRef(), mc);
            }
            return;
        }

        // 6) Check against user's existing companies to see if can automatically map bta's to them
        void Local_MapCompany2UserStockMetas()
        {
            foreach (TaMapCompany company in _targetCompanies.Values)
            {
                // Note! Ignores potential market information of search criteria.. goes after first matching symbol in any market
                StockMeta stockMeta = Pfs.Stalker().FindStock(company.ExtSymbol.ToUpper(), company.ExtMarketCurrencyId, company.ExtISIN);

                if (stockMeta == null)
                    continue;

                // Updates record on '_targetCompanies' list
                company.StockMeta = stockMeta;
            }
        }
    }

    // Decision! Even two MapCompRef may end up pointing same StockMeta those are not to be grouped together but just kept 
    //           next to each other on view.. as things just flow better if keeping this solely MapCompRef grouped
    Dictionary<string, List<ViewBtAction>> _viewByCompany; // key = MapCompRef

    // actual mapped StockMeta is part of _targetCompanies, but for viewing we need StockMeta so have view structure
    protected class ViewBtAction()  // Note! This is recreated all the time, cant contain any states etc
    {
        public BtAction bta;
        public StockMeta stockMeta;
    }

    // 7) Show all Broker Transactions, grouped by MapCompRef (==broker company, NOT by StockMeta)
    private void ViewAllPerCompany()
    {
        _viewByCompany = new();

        foreach (BtAction bta in _brokerTransactions)
        {
            if (IsBtaIncluded(bta) == false)
                continue;
            
            if (_viewByCompany.ContainsKey(bta.MapCompRef) == false)
                _viewByCompany.Add(bta.MapCompRef, new List<ViewBtAction>());

            TaMapCompany mc = _targetCompanies[bta.MapCompRef];

            _viewByCompany[bta.MapCompRef].Add(new ViewBtAction()
            {
                bta = bta,
                stockMeta = mc?.StockMeta,
            });
        }
        return;
    }

    // Little Santas helper that per progress state, filters etc knows whats shown/processed
    protected bool IsBtaIncluded(BtAction bta) 
    {
        // On 3/5 and 4/5 we show all actions, known and unknowns, as long user hasent specially ask to ignore them
        if (bta.TA.Action != TaType.Unknown && _typeFilterSelection.Contains(bta.TA.Action.ToString()) == false ||
            bta.TA.Action == TaType.Unknown && _typeFilterSelection.Contains(bta.BrokerAction) == false)
            return false;

        if (_progress == Progress.ViewTestRun)
        {
            TaMapCompany mc = _targetCompanies[bta.MapCompRef];

            // On 4/5 all unmapped companies & unknown actions are hidden
            if (mc == null || mc.StockMeta == null || bta.TA.Action == TaType.Unknown)
                return false;
        }
        return true;
    }

    // 8) Allow launch dialog to automatically/manually assign records to companies, and add new tracked stocks for records
    protected async Task OnBtnAddNewCompanies()
    {
        List<MapCompany> mapCompanies = new();

        foreach (TaMapCompany mc in _targetCompanies.Values)
        {
            if (_viewByCompany.ContainsKey(mc.MapCompRef()) == false)
                continue; // companys events may have been processed/deleted already

            if (_viewByCompany[mc.MapCompRef()].Any(a => a.bta.Status == BtAction.TAStatus.Acceptable || a.bta.Status == BtAction.TAStatus.Ready))
                // Lets just go mapping with companies those actually has TAs we can process
                mapCompanies.Add(mc.DeepCopy());
        }

        var parameters = new DialogParameters {
            { "Companies",  mapCompanies.ToArray() },
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };

        var dialog = Dialog.Show<DlgMapCompany>("", parameters, maxWidth);

        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // 9) Update bta's with matched/added StockMeta's 

            // Lets ignore here all previous matches 
            foreach (TaMapCompany mc in _targetCompanies.Values)
                mc.StockMeta = null;

            // And update them per latest mappings
            foreach ( MapCompany mc in (List<MapCompany>)result.Data)
            {
                if (mc.StockMeta == null)
                    continue;

                _targetCompanies.Values.Where(a => a.MapCompRef() == mc.MapCompRef()).ToList().ForEach(c => c.StockMeta = mc.StockMeta);
            }

            ViewAllPerCompany();
            StateHasChanged();
        }
    }

    // 4/5 is setup by running all 'acceptable' state un-filtered transactions as dry run against stalker => ready, err, duplicate etc
    private async Task OnBtnViewTestRunAsync()
    {
        _progress = Progress.ViewTestRun;

        StalkerDoCmd dryContent = Pfs.Stalker().GetCopyOfStalker();
        dryContent.TrackActions();

        foreach (BtAction bta in _brokerTransactions.OrderBy(a => a.TA.RecordDate))
        {
            if (IsBtaIncluded(bta) == false)
                continue;

            try
            {
                TaMapCompany mc = _targetCompanies.Values.First(c => c.MapCompRef() == bta.MapCompRef);

                if (bta.TA.Action == TaType.Close)
                {
                    _repManual.Add($"{mc.StockMeta.GetSRef()} close manually at {bta.TA.PaymentDate}");
                    bta.Status = BtAction.TAStatus.Manual;
                    continue;
                }

                string action = StalkerAddOn_Transactions.Convert(bta.TA, mc.StockMeta, _selPortfolio);

                Result stalkerRes = dryContent.DoAction(action);

                if (stalkerRes.Ok)
                { 
                    _repAdded[(int)bta.TA.Action]++;
                    bta.Status = BtAction.TAStatus.Ready;
                    bta.ErrMsg = string.Empty;
                }
                else
                {
                    string stalkerErr = (stalkerRes as FailResult).Message;

                    bta.ErrMsg = $"Failed to Processing! {mc.StockMeta.marketId}${mc.StockMeta.symbol}" +
                                   $" with ActionCmd [{action}]" +
                                   $" got error msg [{stalkerErr}]";

                    if (stalkerErr.StartsWith(StalkerErr.Duplicate.ToString()))
                    {
                        bta.Status = BtAction.TAStatus.ErrTestDupl;
                        _repFailedDupl++;
                    }
                    else if (stalkerErr.StartsWith(StalkerErr.UnitMismatch.ToString()))
                    {
                        bta.Status = BtAction.TAStatus.ErrTestUnits;
                        _repFailedUnit.Add(bta.Orig);
                    }
                    else
                        bta.Status = BtAction.TAStatus.ErrTest;
                }

                // Lets also make sure each stock get tracked by specific PF
                dryContent.DoAction($"Follow-Portfolio PfName=[{_selPortfolio}] SRef=[{mc.StockMeta.GetSRef()}]");
            }
            catch ( Exception ex)
            {
                bta.ErrMsg = $"StalkerDryRun failed to exception [{ex.Message}]";
                bta.Status = BtAction.TAStatus.ErrTest;
            }
        }

        // Getting here means DryRun was successfull... so ready to push in ones those get accepted
        _acceptableActionSet = dryContent.GetActions();

        ViewAllPerCompany();
        StateHasChanged();
        return;
    }

    protected async Task OnBtnFetchCurrencyAsync(ViewBtAction entry)
    {
        List<DlgFetchRates.Fetch> fetch = new();
        fetch.Add(new DlgFetchRates.Fetch()
        {
            Date = entry.bta.TA.RecordDate,
            Currency = entry.bta.TA.Currency,
        });

        var parameters = new DialogParameters {
            { "Missing",  fetch },
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium };

        var dialog = Dialog.Show<DlgFetchRates>("", parameters, maxWidth);

        var result = await dialog.Result;

        if (!result.Canceled)
        {
            List<DlgFetchRates.Fetch> respData = (List<DlgFetchRates.Fetch>)result.Data;

            if (respData.Count == 1 && respData[0].Rate.HasValue )
                UpdateMissingRate(entry.bta, respData[0].Rate.Value);
        }
    }

    protected void OnBtnViewTransaction(ViewBtAction entry)
    {
        StringBuilder sb = new();

        sb.AppendLine($"Action_________ = {entry.bta.TA.Action}");
        sb.AppendLine($"UniqueId_______ = {entry.bta.TA.UniqueId}");
        sb.AppendLine($"RecordDate_____ = {entry.bta.TA.RecordDate}");
        sb.AppendLine($"Note___________ = {entry.bta.TA.Note}");
        sb.AppendLine($"ISIN___________ = {entry.bta.TA.ISIN}");
        sb.AppendLine($"Market_________ = {entry.bta.TA.Market}");
        sb.AppendLine($"Symbol_________ = {entry.bta.TA.Symbol}");
        sb.AppendLine($"CompanyName____ = {entry.bta.TA.CompanyName}");
        sb.AppendLine($"Currency_______ = {entry.bta.TA.Currency}");
        sb.AppendLine($"CurrencyRate___ = {entry.bta.TA.CurrencyRate}");
        sb.AppendLine($"Units__________ = {entry.bta.TA.Units}");
        sb.AppendLine($"McAmountPerUnit = {entry.bta.TA.McAmountPerUnit}");
        sb.AppendLine($"McFee__________ = {entry.bta.TA.McFee}");

        var parameters = new DialogParameters
        {
            { "Title",  "Transaction fields" },
            { "Text",  sb.ToString() }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };

        Dialog.Show<DlgSimpleTextViewer>("", parameters, maxWidth);
    }

    protected void OnBtnOriginalBTA(ViewBtAction entry)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Original content" },
            { "Text",  Convert2Debug(entry.bta.Orig, entry.bta.TA) }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };

        Dialog.Show<DlgSimpleTextViewer>("", parameters, maxWidth);
    }

    protected void OnBtnDeleteBTA(ViewBtAction entry)
    {
        _brokerTransactions.Remove(entry.bta);

        ViewAllPerCompany();
        StateHasChanged();
    }

    protected void OnBtnErrorBTA(ViewBtAction entry)
    {
        var parameters = new DialogParameters
        {
            { "Title",  "Error Info" },
            { "Text",  entry.bta.ErrMsg }
        };

        DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };

        Dialog.Show<DlgSimpleTextViewer>("", parameters, maxWidth);
    }

    protected void OnBtnFlagCompBTA(string mapCompRef)
    {
        _targetCompanies[mapCompRef].Flagged = !_targetCompanies[mapCompRef].Flagged;
    }

    protected void OnBtnTestAndRunCompany(string mapCompRef)
    {
        TaMapCompany mc = _targetCompanies[mapCompRef];

        switch ( mc.Test )
        {
            case TaMapCompany.TestState.UnTested:
                mc.AcceptableActionSet = Local_Test();

                if ( mc.AcceptableActionSet != null )
                    mc.Test = TaMapCompany.TestState.TestOk;
                else
                    mc.Test = TaMapCompany.TestState.Failed;
                break;

            case TaMapCompany.TestState.TestOk:
                if (Local_Save())
                {
                    _brokerTransactions.RemoveAll(c => c.MapCompRef == mapCompRef);
                    _targetCompanies.Remove(mapCompRef);
                }
                else
                    mc.Test = TaMapCompany.TestState.Failed;
                break;
        }

        ViewAllPerCompany();
        StateHasChanged();
        return;

        List<string> Local_Test()
        {
            bool success = true;
            StalkerDoCmd dryContent = Pfs.Stalker().GetCopyOfStalker();
            dryContent.TrackActions();

            foreach (ViewBtAction vbta in _viewByCompany[mapCompRef].OrderBy(a => a.bta.TA.RecordDate))
            {
                BtAction bta = vbta.bta;

                if (bta.Status != BtAction.TAStatus.Acceptable)
                    continue;

                try
                {
                    string action = StalkerAddOn_Transactions.Convert(bta.TA, mc.StockMeta, _selPortfolio);

                    Result stalkerRes = dryContent.DoAction(action);

                    if (stalkerRes.Ok)
                        bta.Status = BtAction.TAStatus.Ready;
                    else
                    {
                        string stalkerErr = (stalkerRes as FailResult).Message;

                        bta.ErrMsg = $"Failed to Processing! {mc.StockMeta.marketId}${mc.StockMeta.symbol}" +
                                       $" with ActionCmd [{action}]" +
                                       $" got error msg [{stalkerErr}]";

                        if (stalkerErr.StartsWith(StalkerErr.Duplicate.ToString()))
                            bta.Status = BtAction.TAStatus.ErrTestDupl;
                        else if (stalkerErr.StartsWith(StalkerErr.UnitMismatch.ToString()))
                        {
                            success = false;
                            bta.Status = BtAction.TAStatus.ErrTestUnits;
                        }
                        else
                        {
                            success = false;
                            bta.Status = BtAction.TAStatus.ErrTest;
                        }
                    }
                }
                catch (Exception ex)
                {
                    bta.ErrMsg = $"StalkerDryRun failed to exception [{ex.Message}]";
                    bta.Status = BtAction.TAStatus.ErrTest;
                    success = false;
                }
            }

            // Lets also make sure stock get tracked by specific PF
            dryContent.DoAction($"Follow-Portfolio PfName=[{_selPortfolio}] SRef=[{mc.StockMeta.GetSRef()}]");

            List<string> ret = dryContent.GetActions();

            if (success && ret.Count > 1)
                return ret;
            else
                return null;
        }

        bool Local_Save()
        {
            Result processRes = Pfs.Stalker().DoActionSet(mc.AcceptableActionSet);

            return processRes.Ok;
        }
    }

    protected void OnBtnDeleteCompany(string mapCompRef)
    {
        _brokerTransactions.RemoveAll(c => c.MapCompRef == mapCompRef);
        _targetCompanies.Remove(mapCompRef);

        ViewAllPerCompany();
        StateHasChanged();
    }

    protected List<string> _acceptableActionSet = new();

    private async Task OnBtnProcessAsync()
    {
        _progress = Progress.ViewResultLog;
        Result processRes = Pfs.Stalker().DoActionSet(_acceptableActionSet);

        if (processRes.Ok == false )
        {
            _viewRawTextual = "Failed as: " + (processRes as FailResult).Message;
            return;
        }

        StringBuilder sb = new();

        LogDetails();
        LogManual();
        LogFailedConversions();
        LogFailedUnitMismatch();
        LogFailedTest();
        LogFailedUnmappedCompanies();
        LogNoCompanyInfo();

        _viewRawTextual = sb.ToString();
        await Dialog.ShowMessageBox("Success!", "All transactions acceptable transactions added now to account", yesText: "Ok");
        return;

        void LogDetails()
        {
            sb.AppendLine($"Broker Total TA's: {_repTotalTAs, -30}");
            sb.AppendLine($"     Unknown TA's: {_repIgnoredTAs,-30}");
            sb.AppendLine($"       Duplicates: {_repFailedDupl,-30}");
            sb.AppendLine($"Added        Buys: {_repAdded[(int)TaType.Buy],-30}");
            sb.AppendLine($"Added       Sales: {_repAdded[(int)TaType.Sell],-30}");
            sb.AppendLine($"Added        Divs: {_repAdded[(int)TaType.Divident],-30}");
        }

        void LogManual()
        {
            if (_repManual.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine($"***All stock closings needs to be done by you, manually, under tracking report***");
            foreach (string ba in _repManual)
                sb.AppendLine(ba);
        }

        void LogFailedConversions()
        {
            if (_repFailedConv.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine($"***Following CSV actions failed conversion, this is bit strange***");
            sb.AppendLine(_origCsvHeaderLine);
            foreach (string ba in _repFailedConv)
                sb.AppendLine(ba);
        }

        void LogFailedUnitMismatch()
        {
            if (_repFailedUnit.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine($"***Following CSV actions failed w unit amount. Often more dividents than holdings, example if some***");
            sb.AppendLine($"***M&A shares didnt get correctly changed to new symbol, or similar issue w selling more than owning.***");
            sb.AppendLine(_origCsvHeaderLine);
            foreach (string ba in _repFailedUnit)
                sb.AppendLine(ba);
        }

        void LogFailedTest()
        {
            if (_repFailedTest.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine($"***Following CSV actions failed test run, this is bit strange***");
            sb.AppendLine(_origCsvHeaderLine);
            foreach (string ba in _repFailedTest)
                sb.AppendLine(ba);
        }

        void LogFailedUnmappedCompanies()
        {
            Dictionary<string, List<string>> unmapped = new();

            foreach (BtAction bta in _brokerTransactions)
            {
                if (IsBtaIncluded(bta) == false)
                    continue;

                TaMapCompany mc = _targetCompanies[bta.MapCompRef];

                if (mc != null && mc.StockMeta == null)
                {
                    if (unmapped.ContainsKey(bta.MapCompRef) == false)
                        unmapped.Add(bta.MapCompRef, new());

                    unmapped[bta.MapCompRef].Add(bta.Orig);
                }
            }

            foreach (KeyValuePair<string, List<string>> kvp in unmapped)
            {
                sb.AppendLine();
                sb.AppendLine($"***Unmapped/handled company with identification [{kvp.Key}] ***");
                sb.AppendLine(_origCsvHeaderLine);
                foreach (string ba in kvp.Value)
                    sb.AppendLine(ba);
            }
        }

        void LogNoCompanyInfo()
        {
            if (_repRejected.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine($"***Following CSV actions rejected as no company info at all***");
            sb.AppendLine(_origCsvHeaderLine);
            foreach (string ba in _repRejected)
                sb.AppendLine(ba);
        }
    }
}
