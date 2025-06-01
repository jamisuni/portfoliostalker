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

using System.Collections.ObjectModel;
using System.Data;

using Pfs.Types;

namespace Pfs.Data.Stalker;

// Wraps StalkerData w textual-command-API that allows editing StalkerData
public class StalkerDoCmd : StalkerData
{
    // Has ability to collect list of performed transactions, but requires activation
    protected List<string> _actions = null;

    public new void Init()
    {
        base.Init();
        _actions = new List<string>();
    }

    // This is main interface to operate StalkerContent with Action cmdLine -commands, requires Named parameters
    public Result DoAction(string cmdLine)
    {
        StalkerAction stalkerAction;
        Result error = ParseAction(cmdLine, out stalkerAction);

        if (error.Ok == false)
            return error;

        error = DoAction(stalkerAction);

        if (error.Ok && _actions != null)
            _actions.Add(cmdLine);

        return error;
    }

    public static void DeepCopy(StalkerDoCmd from, StalkerDoCmd to) // Main issue w C# to C++ is that just cant get strong feeling what happens on "assembler" level, too automatic,
    {                                                               // so just in case well do it this way.. even this is risky code as future changes easily forgets do steps 
        StalkerData.DeepCopy(from, to);

        to._actions = new();
    }

    public void TrackActions()
    {
        if ( _actions == null )
            _actions = new();
    }

    public List<string> GetActions()
    {
        if (_actions == null)
            return null;

        // Not allowed to clean, just return copy
        return new List<string>(_actions);
    }

    static public Result ParseAction(string cmdLine, out StalkerAction stalkerAction)
    {
        stalkerAction = null;

        List<string> cmdSegments = StalkerSplit.SplitLine(cmdLine);
        cmdSegments.RemoveAll(s => string.IsNullOrWhiteSpace(s) == true);

        int segmentID = 0;

        foreach (string segment in cmdSegments)
        {
            segmentID++;

            // First segment is always expected to be Operation-Element combo defining whats done and for what...
            if ( segmentID == 1 )
            {
                string[] param1Split = segment.Split('-');

                if (param1Split.Length != 2)
                    return new FailResult($"{segment} is supposed to be Operation-Element combo");

                StalkerOperation operation = (StalkerOperation)Enum.Parse(typeof(StalkerOperation), param1Split[0]);
                StalkerElement element = (StalkerElement)Enum.Parse(typeof(StalkerElement), param1Split[1]);

                stalkerAction = StalkerAction.Create(operation, element);

                if (stalkerAction == null)
                    return new FailResult($"{operation}-{element} is not supported!");

                continue;
            }

            // Rest of segments are parameters, each and every one of them. If 'strictParamNaming' is set then each 
            // given parameter must be formatted w Name=Value (but then actually allows mixed order). If its not set,
            // then parameters must be given exactly correct order but Name=Value is optional, as can use plain Value.
            // RULE: All internal code should use Name=Value, but speed commands expects exact ordering wo names.

            // In case of strict naming, we dont pass segmentID, as expecting segment to be Name=Value
            Result paramResp = stalkerAction.SetParam(segment);

            if (paramResp.Ok == false)
                return paramResp;
        }

        // All parsing is done now, and should have all parameters set with acceptable value 
        return stalkerAction.IsReady();
    }

    protected Result DoAction(StalkerAction stalkerAction)
    {
        switch ( stalkerAction.Element)     
        {
            case StalkerElement.Stock:              // STOCK: Delete

                switch (stalkerAction.Operation)
                {
                    case StalkerOperation.Delete:   // Delete-Stock SRef    (Note! Should be used as prep step before StockMeta is removed)

                        return StockDelete(
                            (string)stalkerAction.Param("SRef"));

                    case StalkerOperation.Set:      // Set-Stock UpdSRef OldSRef

                        return StockSet(
                            (string)stalkerAction.Param("UpdSRef"),
                            (string)stalkerAction.Param("OldSRef"));
                        
                    case StalkerOperation.Split:    // Split-Stock SRef SplitFactor
                        return StockSplit(
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("SplitFactor"));

                    case StalkerOperation.Close:    // Close-Stock SRef Date Note

                        return StockClose(
                            (string)stalkerAction.Param("SRef"),
                            (DateOnly)stalkerAction.Param("Date"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.DeleteAll:   // DeleteAll-Stock SRef (everything related sRef is removed)

                        return StockDeleteAll(
                            (string)stalkerAction.Param("SRef"));
                }
                break;

            case StalkerElement.Portfolio:          // PORTFOLIO: Add / Edit / Delete / Top / Follow / Unfollow

                switch ( stalkerAction.Operation ) 
                {
                    case StalkerOperation.Add:      // Add-Portfolio PfName

                        return PortfolioAdd( 
                            (string)stalkerAction.Param("PfName"));

                    case StalkerOperation.Edit:     // Edit-Portfolio PfCurrName PfNewName

                        return PortfolioEdit(
                            (string)stalkerAction.Param("PfCurrName"),
                            (string)stalkerAction.Param("PfNewName"));

                    case StalkerOperation.Delete:   // Delete-Portfolio PfName

                        return PortfolioDelete(
                            (string)stalkerAction.Param("PfName"));

                    case StalkerOperation.Top:      // Top-Portfolio PfName

                        return PortfolioTop(        
                            (string)stalkerAction.Param("PfName"));

                    case StalkerOperation.Follow:   // Follow-Portfolio PfName SRef

                        return PortfolioFollow(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"));

                    case StalkerOperation.Unfollow: // Unfollow-Portfolio PfName SRef

                        return PortfolioUnfollow(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"));
                }
                break;

            case StalkerElement.Holding:            // HOLDING: Add, Edit, Note, Delete

                switch (stalkerAction.Operation)
                {
                    case StalkerOperation.Add:      // Add-Holding PfName SRef PurhaceId Date Units Price Fee CurrencyRate Note

                        return HoldingAdd(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (string)stalkerAction.Param("PurhaceId"),
                            (DateOnly)stalkerAction.Param("Date"),
                            (decimal)stalkerAction.Param("Units"),
                            (decimal)stalkerAction.Param("Price"),
                            (decimal)stalkerAction.Param("Fee"),
                            (decimal)stalkerAction.Param("CurrencyRate"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.Edit:     // Edit-Holding PurhaceId Date Units Price Fee CurrencyRate Note

                        return HoldingEdit(                                             
                            (string)stalkerAction.Param("PurhaceId"),
                            (DateOnly)stalkerAction.Param("Date"),
                            (decimal)stalkerAction.Param("Units"),
                            (decimal)stalkerAction.Param("Price"),
                            (decimal)stalkerAction.Param("Fee"),
                            (decimal)stalkerAction.Param("CurrencyRate"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.Note:     // Note-Holding PurhaceId Note 

                        return HoldingNote(                                             // can be edited as long as part is still unsold
                            (string)stalkerAction.Param("PurhaceId"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.Delete:   // Delete-Holding PurhaceId         (can be deleted if no trades and no dividents => those needs to be deleted first)

                        return HoldingDelete(
                            (string)stalkerAction.Param("PurhaceId"));

                    case StalkerOperation.Round:    // Round-Holding PfName SRef Units

                        return HoldingRound(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("Units"));
                }
                break;

            case StalkerElement.Trade:              // TRADE: Add, Delete, Note         (do not support edit, as can just delete-add)

                switch (stalkerAction.Operation)
                {
                    case StalkerOperation.Add:      // Add-Trade PfName SRef Date Units Price Fee TradeId OptPurhaceId CurrencyRate Note

                        return TradeAdd(    
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (DateOnly)stalkerAction.Param("Date"),
                            (decimal)stalkerAction.Param("Units"),
                            (decimal)stalkerAction.Param("Price"),
                            (decimal)stalkerAction.Param("Fee"),
                            (string)stalkerAction.Param("TradeId"),
                            (string)stalkerAction.Param("OptPurhaceId"),    // if "" then fifo, otherwise specific holding
                            (decimal)stalkerAction.Param("CurrencyRate"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.Note:     // Note-Trade TradeId Note

                        return TradeNote(
                            (string)stalkerAction.Param("TradeId"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.Delete:   // Delete-Trade TradeId

                        return TradeDelete(
                            (string)stalkerAction.Param("TradeId"));
                }
                break;

            case StalkerElement.Order:              // ORDER: Add, Edit, Delete

                switch (stalkerAction.Operation)
                {
                    case StalkerOperation.Add:      // Add-Order PfName Type SRef Units Price LastDate

                        return OrderAdd(
                            (string)stalkerAction.Param("PfName"),
                            (SOrder.OrderType)stalkerAction.Param("Type"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("Units"),
                            (decimal)stalkerAction.Param("Price"),
                            (DateOnly)stalkerAction.Param("LastDate"));

                    case StalkerOperation.Edit:     // Edit-Order PfName Type SRef EditedPrice Units Price LastDate

                        return OrderEdit(
                            (string)stalkerAction.Param("PfName"),
                            (SOrder.OrderType)stalkerAction.Param("Type"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("EditedPrice"),
                            (decimal)stalkerAction.Param("Units"),
                            (decimal)stalkerAction.Param("Price"),
                            (DateOnly)stalkerAction.Param("LastDate"));

                    case StalkerOperation.Delete:   // Delete-Order PfName SRef Price

                        return OrderDelete(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("Price"));

                    case StalkerOperation.Set:      // Set-Order PfName SRef Price         == RESET 'FillDate', yeah name could be better than set

                        return OrderReset(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("Price"));
                }
                break;

            case StalkerElement.Divident:           // DIVIDENT: Add, Delete    (often leave opt-params empty, and just tell SRef+Units+ExDivDate for automagic assigment)

                switch (stalkerAction.Operation) // DeleteAll
                {
                    case StalkerOperation.Add:      // Add-Divident PfName SRef OptPurhaceId OptTradeId ExDivDate PaymentDate Units PaymentPerUnit CurrencyRate Currency

                        return DividentAdd(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (string)stalkerAction.Param("OptPurhaceId"),        // if given limits operation to specific holdings, and potentially its resend trades
                            (string)stalkerAction.Param("OptTradeId"),          // if given (together with OptPurhaceId) then targets very specific PurhaceId+TradeId
                            (DateOnly)stalkerAction.Param("ExDivDate"),
                            (DateOnly)stalkerAction.Param("PaymentDate"),
                            (decimal)stalkerAction.Param("Units"),
                            (decimal)stalkerAction.Param("PaymentPerUnit"),
                            (decimal)stalkerAction.Param("CurrencyRate"),
                            (CurrencyId)stalkerAction.Param("Currency"));

                    case StalkerOperation.DeleteAll: // DeleteAll-Divident PfName SRef ExDivDate    (==deletes all dividents on holdings/trades of specific stock on specific date)

                        return DividentDeleteAll(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (DateOnly)stalkerAction.Param("ExDivDate"));

                    case StalkerOperation.Delete:   // Delete-Divident PfName SRef ExDivDate PurhaceId (==deletes divident record from specific PurhaceId (and potential its Trades))

                        return DividentDelete(
                            (string)stalkerAction.Param("PfName"),
                            (string)stalkerAction.Param("SRef"),
                            (DateOnly)stalkerAction.Param("ExDivDate"),
                            (string)stalkerAction.Param("PurhaceId"));
                }
                break;

            case StalkerElement.Alarm:              // ALARM: Add, Delete, Edit, DeleteAll
                                                    
                switch (stalkerAction.Operation)
                {
                    case StalkerOperation.Add:      // Add-Alarm Type SRef Level Prms Note

                        return AlarmAdd(
                            (SAlarmType)stalkerAction.Param("Type"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("Level"),
                            (string)stalkerAction.Param("Prms"),
                            (string)stalkerAction.Param("Note"));

                    case StalkerOperation.Delete:   // Delete-Alarm SRef Level

                        return AlarmDelete(
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("Level"));

                    case StalkerOperation.DeleteAll: // DeleteAll-Alarm SRef

                        return AlarmDeleteAll(
                            (string)stalkerAction.Param("SRef"));

                    case StalkerOperation.Edit:     // Edit-Alarm Type SRef OldLevel NewLevel Prms Note

                        return AlarmEdit(
                            (SAlarmType)stalkerAction.Param("Type"),
                            (string)stalkerAction.Param("SRef"),
                            (decimal)stalkerAction.Param("OldLevel"),
                            (decimal)stalkerAction.Param("NewLevel"),
                            (string)stalkerAction.Param("Prms"),
                            (string)stalkerAction.Param("Note"));
                }
                break;

            case StalkerElement.Sector:

                switch ( stalkerAction.Operation)
                {   
                    case StalkerOperation.Set:      // Set-Sector SectorId SectorName       (create sector or update its name)

                        return SectorSet(
                            (int)stalkerAction.Param("SectorId"),
                            (string)stalkerAction.Param("SectorName"));

                    case StalkerOperation.DeleteAll:// DeleteAll-Sector SectorId            (delete sector, fields, and stock refs)

                        return SectorDeleteAll(
                            (int)stalkerAction.Param("SectorId"));

                    case StalkerOperation.Edit:     // Edit-Sector SectorId FieldId FieldName (edit field name)

                        return SectorEdit(
                            (int)stalkerAction.Param("SectorId"),
                            (int)stalkerAction.Param("FieldId"),
                            (string)stalkerAction.Param("FieldName"));

                    case StalkerOperation.Delete:   // Delete-Sector SectorId FieldId       (delete field, and stock refs to it)

                        return SectorDelete(
                            (int)stalkerAction.Param("SectorId"),
                            (int)stalkerAction.Param("FieldId"));

                    case StalkerOperation.Follow:   // Follow-Sector SRef SectorId FieldId  (set stock to be refer sector's field)

                        return SectorFollow(
                            (string)stalkerAction.Param("SRef"),
                            (int)stalkerAction.Param("SectorId"),
                            (int)stalkerAction.Param("FieldId"));

                    case StalkerOperation.Unfollow: // Unfollow-Sector SRef SectorId        (set stock not to be set on sector)

                        return SectorUnfollow(
                            (string)stalkerAction.Param("SRef"),
                            (int)stalkerAction.Param("SectorId"));
                }
                break;
        }
        return new FailResult($"{stalkerAction.Operation}-{stalkerAction.Element} is not supported!");
    }

    #region STOCK

    protected SStock GetOrAddStock(string sRef)
    {   // This is not required operation, but its step that is done example before Add-Alarm
        SStock ret = _stocks.SingleOrDefault(s => s.SRef == sRef);
        if (ret != null)
            return ret;

        ret = new SStock(sRef);
        _stocks.Add(ret);
        return ret;
    }

    protected Result StockDelete(string sRef) // yes, anything, absolute anything.. prevents delete
    {   // Delete-Stock SRef
        SStock stock = StockRef(sRef);

        if (stock == null)
            return new FailResult($"{sRef} is unknown stock");

        if (_portfolios.Any(p => p.StockHoldings.Where(h => h.SRef == sRef).ToList().Count() > 0) == true)
            // Pretty much stock comes immune to delete after has Holdings added to it (or close to immune)
            return new FailResult($"Has holdings cant delete yet");

        if (_portfolios.Any(p => p.StockTrades.Where(h => h.SRef == sRef).ToList().Count() > 0) == true)
            return new FailResult($"Has trades cant delete yet");

        if (_portfolios.Any(p => p.StockOrders.Where(h => h.SRef == sRef).ToList().Count() > 0) == true)
            return new FailResult($"Has orders cant delete yet");

        if (_portfolios.Any(p => p.SRefs.Contains(sRef)))
            return new FailResult($"Has trackings cant delete yet");

        _stocks.RemoveAll(s => s.SRef == sRef);

        return new OkResult();
    }

    protected Result StockSet(string updSRef, string oldSRef)
    {   // Set-Stock UpdSRef OldSRef
        if (StockMeta.ParseSRef(updSRef).marketId == MarketId.CLOSED && _portfolios.Any(p => p.StockHoldings.Where(h => h.SRef == oldSRef).ToList().Count() > 0) == true)
            return new FailResult($"Has holdings cant close");

        SStock stock = StockRef(oldSRef);
        if (stock != null)
            stock.SRef = updSRef;

        foreach (SPortfolio pf in  _portfolios)
        {
            int index = pf.SRefs.FindIndex(s => s == oldSRef);

            if (index > 0)
                pf.SRefs[index] = updSRef;

            foreach (SOrder so in pf.StockOrders)
                if ( so.SRef == oldSRef)
                    so.SRef = updSRef;

            foreach (SHolding sh in pf.StockHoldings)
                if (sh.SRef == oldSRef)
                    sh.SRef = updSRef;

            foreach (SHolding st in pf.StockTrades)
                if (st.SRef == oldSRef)
                    st.SRef = updSRef;
        }
        return new OkResult();
    }

    protected Result StockSplit(string sRef, decimal splitFactor)
    {   // Split-Stock SRef SplitFactor
        if (splitFactor <= 0.01m)
            return new FailResult($"SplitFactor must be > 0!");
        SStock stock = StockRef(sRef);
        if (stock == null)
            return new FailResult($"{sRef} is unknown stock");
        if (splitFactor == 1)
            return new OkResult(); // No change

        foreach (SPortfolio pf in _portfolios)
        {
            foreach (SHolding sh in pf.StockHoldings)
                if (sh.SRef == sRef)
                {
                    sh.Units = (sh.Units / splitFactor).Round3();
                    sh.PricePerUnit = (sh.PricePerUnit * splitFactor).Round3();
                    sh.FeePerUnit = (sh.FeePerUnit * splitFactor).Round3();
                    sh.OriginalUnits = (sh.OriginalUnits / splitFactor).Round3();

                    for ( int i = 0; i < sh.Dividents.Count; i++ )
                        sh.Dividents[i] = sh.Dividents[i] with { PaymentPerUnit = (sh.Dividents[i].PaymentPerUnit * splitFactor).Round3() };
                }
        }
        return new OkResult();
    }

    protected Result StockClose(string sRef, DateOnly date, string note) 
    {   // Close-Stock SRef Date Note

        var parsedSRef = StockMeta.ParseSRef(sRef);

        foreach (SPortfolio pf in _portfolios)
        {
            List<SHolding> pfHolds2Close = new();

            foreach (SHolding sh in pf.StockHoldings)
                if (sh.SRef == sRef)
                    pfHolds2Close.Add(sh);

            foreach (SHolding hc in pfHolds2Close)
            {
                pf.StockHoldings.Remove(hc);
                hc.Sold = new SHolding.Sale(hc.PurhaceId + "_CLOSED", date, hc.PricePerUnit + hc.FeePerUnit, 0, hc.CurrencyRate, note);
                pf.StockTrades.Add(hc);
            }
        }

        return StockSet($"{MarketId.CLOSED}${parsedSRef.symbol}", sRef);
    }

    protected Result StockDeleteAll(string sRef) // yes, anything, absolute anything.. gets destroyed
    {   // DeleteAll-Stock SRef
        foreach ( SPortfolio pf in _portfolios)
        {
            pf.StockHoldings.RemoveAll(h => h.SRef == sRef);

            pf.StockTrades.RemoveAll(h => h.SRef == sRef);

            pf.StockOrders.RemoveAll(h => h.SRef == sRef);

            pf.SRefs.Remove(sRef);
        }
        _stocks.RemoveAll(s => s.SRef == sRef);

        return new OkResult();
    }

    #endregion

    #region PORTFOLIO

    protected Result PortfolioAdd(string name)
    {
        if (_portfolios.Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Any() == true)
            // No duplicate names allowed
            return new FailResult($"Cant add duplicate portfolio!");

        _portfolios.Add(new SPortfolio()
        {
            Name = name,
            StockHoldings = new List<SHolding>(),
        });

        return new OkResult();
    }

    protected Result PortfolioEdit(string pfCurrName, string pfNewName)
    {
        SPortfolio pf = PortfolioRef(pfCurrName);

        if ( pf == null)
            return new FailResult($"{pfCurrName} is unknown portfolio");

        if (string.Equals(pfCurrName, pfNewName, StringComparison.OrdinalIgnoreCase) == false)
        {   // not just lower/upper update, but actual new name.. so check duplicates
            if (_portfolios.Where(p => string.Equals(p.Name, pfNewName, StringComparison.OrdinalIgnoreCase)).Any() == true)
                return new FailResult($"Cant add duplicate portfolio!");
        }

        pf.Name = pfNewName;
        return new OkResult();
    }

    protected Result PortfolioDelete(string pfName)
    {
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        ReadOnlyCollection<SHolding> holdings = PortfolioHoldings(pfName);

        if (holdings.Count > 0)
            return new FailResult($"Cant delete as has holdings!");

        ReadOnlyCollection<SHolding> trades = PortfolioTrades(pfName);

        if (trades.Count > 0)
            return new FailResult($"Cant delete as has trades!");

        _portfolios.RemoveAll(p => p.Name == pfName);

        return new OkResult();
    }

    protected Result PortfolioTop(string pfName)
    {
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        int position = _portfolios.IndexOf(pf);

        _portfolios.RemoveAt(position);
        _portfolios.Insert(0, pf);

        return new OkResult();
    }

    protected Result PortfolioFollow(string pfName, string sRef)
    {
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        if (pf.SRefs.Contains(sRef))
            // Yeah could return error, but really same to return OK as its already there
            return new OkResult();

        pf.SRefs.Add(new string(sRef));
        return new OkResult();
    }

    protected Result PortfolioUnfollow(string pfName, string sRef)
    {
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        pf.SRefs.RemoveAll(s => s == sRef);
        return new OkResult();
    }

    #endregion

    #region ALARMS

    protected Result AlarmAdd(SAlarmType Type, string sRef, decimal level, string prms, string note)
    {   // Add-Alarm Type SRef Value Prms Note
        SStock stock = GetOrAddStock(sRef);

        level = decimal.Round(level, 2);

        if (stock.Alarms.FirstOrDefault(a => a.Level == level) != null)
            // Multiple alarms w same Value level for one stock are NOT allowed, not even different types
            return new FailResult($"Cant add duplicate Alarm as has already existing alarm for {level.ToString("0.00")}!");

        stock.Alarms.Add(SAlarm.Create(Type, level, note, prms));

        return new OkResult();
    }

    protected Result AlarmDelete(string sRef, decimal level)
    {   // Delete-Alarm SRef Level
        SStock stock = StockRef(sRef);

        if (stock == null)
            return new FailResult($"{sRef} is unknown stock");

        stock.Alarms.RemoveAll(a => a.Level == level);

        return new OkResult();
    }

    protected Result AlarmDeleteAll(string sRef)
    {   // DeleteAll-Alarm SRef
        SStock stock = StockRef(sRef);

        if (stock == null)
            return new FailResult($"{sRef} is unknown stock");

        stock.Alarms = new();

        return new OkResult();
    }

    protected Result AlarmEdit(SAlarmType Type, string sRef, decimal OldLevel, decimal NewLevel, string prms, string note)
    {   // Edit-Alarm Type SRef OldLevel NewLevel Prms Note
        SStock stock = StockRef(sRef);

        if (stock == null)
            return new FailResult($"{sRef} is unknown stock");

        NewLevel = decimal.Round(NewLevel, 2);

        int pos = stock.Alarms.FindIndex(a => a.Level == OldLevel);

        if ( pos < 0 )
            return new FailResult($"Could not find this alarm to be edited with level {OldLevel.ToString("0.00")}!");

        stock.Alarms[pos] = SAlarm.Create(Type, NewLevel, note, prms);

        return new OkResult();
    }

    #endregion

    #region HOLDINGS

    protected Result HoldingAdd(string pfName, string sRef, string purhaceId, DateOnly purhaceDate, decimal units, decimal pricePerUnit, 
                                      decimal fee, decimal currencyRate, string note)
    {   // Add-Holding PfName SRef PurhaceId Date Units Price Fee CurrencyRate Note
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        if (_portfolios.Any(p => p.StockHoldings.Any(h => h.PurhaceId == purhaceId)) ||
            _portfolios.Any(p => p.StockTrades.Any(h => h.PurhaceId == purhaceId)))
            return new FailResult($"{StalkerErr.Duplicate}! {purhaceId} already exists!");

        // Later! Duplicate detection purely bases to purhaceId, its given manually as mandatory, or on import.
        //        May even able to come as empty from ImportTransactions... but Nordnet has it always. If comes
        //        empty then start adding some "GEN-random" as it. That case may has to add checking if "GEN-"
        //        then mark as duplicate also ones that "same day, same amount, same price, with GEN-". 
        //        Its not rush, and actually it effects edits, and trades, etc so dont do before has to.
        //        That time could also allow manually give empty, so can hit same verifications. 
        //        Problem is doing partial small sale could get duplicated on imports if purhaceId is not match.

        pf.StockHoldings.Add(new SHolding()
        {
            SRef = sRef,
            PurhaceId = purhaceId,
            PurhaceDate = purhaceDate,
            Units = units,
            PricePerUnit = pricePerUnit,
            FeePerUnit = fee == 0 ? 0 : Math.Round(fee / units, 5),
            OriginalUnits = units,
            CurrencyRate = currencyRate,
            PurhaceNote = note,
        });

        return new OkResult();
    }

    protected Result HoldingEdit(string purhaceId, DateOnly purhaceDate, decimal units, decimal pricePerUnit,
                                       decimal fee, decimal currencyRate, string note)
    {   // Edit-Holding PurhaceId Date Units Price Fee CurrencyRate Note
        SPortfolio pf = _portfolios.Where(p => p.StockHoldings.Any(h => h.PurhaceId == purhaceId)).SingleOrDefault();

        if ( pf == null )
            pf = _portfolios.Where(p => p.StockTrades.Any(h => h.PurhaceId == purhaceId)).SingleOrDefault();

        if (pf == null)
            return new FailResult($"Cant find this purhaceId from any portfolio.");

        if ( pf.StockTrades.Any(h => h.PurhaceId == purhaceId) )
            // part of purhace is already sold, so not supporting editing anymore...sorry..
            return new FailResult($"Cant edit as part of this purhace is already sold.");

        SHolding holding = pf.StockHoldings.SingleOrDefault(h => h.PurhaceId == purhaceId);

        holding.PurhaceDate = purhaceDate;
        holding.Units = units;
        holding.PricePerUnit = pricePerUnit;
        holding.FeePerUnit = fee == 0 ? 0 : Math.Round(fee / units, 5);
        holding.OriginalUnits = units;
        holding.CurrencyRate = currencyRate;
        holding.PurhaceNote = note;

        return new OkResult();
    }

    protected Result HoldingNote(string purhaceId, string note)
    {   // Note-Holding PurhaceId Note
        SPortfolio pf = _portfolios.Where(p => p.StockHoldings.Any(h => h.PurhaceId == purhaceId)).SingleOrDefault();

        if (pf == null) // if all sold this doesnt allow editing note anymore
            return new FailResult($"Cant find this purhaceId");

        SHolding holding = pf.StockHoldings.SingleOrDefault(h => h.PurhaceId == purhaceId);
        holding.PurhaceNote = note;

        foreach (SHolding trade in pf.StockTrades.Where(h => h.PurhaceId == purhaceId))         // !!!TEST!!!2024!!! This hasent yet tested!
            trade.PurhaceNote = note;

        return new OkResult();
    }

    protected Result HoldingDelete(string purhaceId)
    {   // Delete-Holding PurhaceId
        SPortfolio pf = _portfolios.Where(p => p.StockHoldings.Any(h => h.PurhaceId == purhaceId)).SingleOrDefault();

        if (pf == null)
            return new FailResult($"Cant find this purhaceId");

        SHolding holding = pf.StockHoldings.SingleOrDefault(h => h.PurhaceId == purhaceId);

        if (pf.StockTrades.Any(h => h.PurhaceId == purhaceId))
            // Trades needs to be rolled back first if wants to delete
            return new FailResult($"Partially sold! Trades needs to be removed first before holding can be removed");

        if ( holding.AnyDividents())
            // Dividents needs to be removed first if wants to delete
            return new FailResult($"Dividents needs to be removed first before holding can be removed");

        pf.StockHoldings.RemoveAll(h => h.PurhaceId == purhaceId);

        return new OkResult();
    }

    protected Result HoldingRound(string pfName, string sRef, decimal units)
    {   // Used to cut decimals from holding thats received example as M&A, so XX.YY => XX...0.YY goes cash
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        decimal totalHoldings = pf.StockHoldings.Where(h => h.SRef == sRef).Select(s => s.Units).Sum();

        if ((int)totalHoldings != (int)units)
            return new FailResult($"{pfName} unit amounts didnt match");

        // Yet we update also original, as really this is like never got those decimals
        pf.StockHoldings.Where(h => h.SRef == sRef).ToList().ForEach(s => { s.Units = (int)s.Units; s.OriginalUnits = (int)s.OriginalUnits; } );

        // Later! Really should recalculate also unit price.. by reducing it with cash we got from these decimal sales

        return new OkResult();
    }

    #endregion

    #region TRADES / SALES

    protected Result TradeAdd(string pfName, string sRef, DateOnly saleDate, decimal units, decimal pricePerUnit,
                                    decimal fee, string tradeId, string optPurhaceId, decimal currencyRate, string note)
    {   // Add-Trade PfName SRef Date Units Price Fee TradeId OptPurhaceId CurrencyRate Note
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        if (_portfolios.Any(p => p.StockTrades.Any(t => t.Sold.TradeId == tradeId)) == true)
            return new FailResult($"{StalkerErr.Duplicate}! has already this {tradeId} already exists!");

        decimal feePerUnit = fee == 0 ? 0 : Math.Round(fee / units, 5);

        if (string.IsNullOrEmpty(optPurhaceId) == true)
            // 'optPurhaceId' acts as optional parameter, needs to be given
            return LocalSaleAsFIFO();

        // but if empty then does default First-In-First-Out selection
        return LocalSaleSpecificHolding(optPurhaceId, units);

        Result LocalSaleAsFIFO()
        {
            decimal available = pf.StockHoldings.Where(h => h.SRef == sRef).ToList().ConvertAll(h => h.Units).Sum();

            if (available < units)
                return new FailResult($"{StalkerErr.UnitMismatch}! Attempt to sell more units than owns!");

            decimal unitsLeftToSale = units;

            // Loop remaining holdings for stock under this portfolio, in FIFO order and 'sell' holdings until required amount done
            foreach (SHolding holding in pf.StockHoldings.Where(h => h.SRef == sRef).OrderBy(h => h.PurhaceDate))
            {
                if ( unitsLeftToSale > holding.Units + 0.001m )
                {   // full holding is sold, and needs more
                    Result temp = LocalSaleSpecificHolding(holding.PurhaceId, holding.Units);
                    unitsLeftToSale -= holding.Units;

                    if ( temp.Ok == false)
                        return new FailResult($"Coding error? Failed to sell one of holdings: {(temp as FailResult).Message}");
                }
                else
                {   // either full or partial holding is sold, but all required units found their place
                    return LocalSaleSpecificHolding(holding.PurhaceId, unitsLeftToSale);
                }
            }
            // Should never come here as already checked that there is enough holdings for this sale...
            return new FailResult($"Coding error! Shouldnt come here as already counter to have enough units");
        }

        Result LocalSaleSpecificHolding(string purhaceId, decimal unitsSoldFromHolding)
        {   // plz note that used also from fifo, so most tuff needs to be received as parameters!
            int holdingPos = pf.StockHoldings.FindIndex(h => h.PurhaceId == purhaceId);

            if (holdingPos < 0)
                return new FailResult($"Could not find holding: {purhaceId}");

            if (pf.StockHoldings[holdingPos].Units < unitsSoldFromHolding)
                return new FailResult($"{StalkerErr.UnitMismatch}! Attempt to sell more units than owns on this holding!");

            SHolding.Sale sale = new(tradeId, saleDate, pricePerUnit, feePerUnit, currencyRate, note);

            SHolding holding = pf.StockHoldings[holdingPos];

            if (holding.Units <= unitsSoldFromHolding + 0.001m)
            {   // selling all (remaining) units from this holding => full struct removed from holdings -> trade
                pf.StockHoldings.RemoveAt(holdingPos);
                holding.Sold = sale;
                pf.StockTrades.Add(holding);
            }
            else
            {   // create duplicate w sold units
                SHolding trade = holding.DeepCopy();
                trade.Units = unitsSoldFromHolding;
                trade.Sold = sale;
                pf.StockTrades.Add(trade);

                // selling part of holding, so reduce left overs
                holding.Units -= unitsSoldFromHolding;
            }
            return new OkResult();
        }
    }

    protected Result TradeNote(string tradeId, string note)
    {   // Note-Trade TradeId Note
        SPortfolio pf = _portfolios.Where(p => p.StockTrades.Any(h => h.Sold.TradeId == tradeId)).SingleOrDefault();

        if (pf == null)
            return new FailResult($"Cant find this tradeId");

        // Note! Need to loop as if sold w one sale a more than one holding then has duplicate TradeId's on StockTrades
        foreach ( SHolding trade in pf.StockTrades.Where(t => t.Sold.TradeId == tradeId))
            trade.Sold = trade.Sold.NewWithUpdatedNote(note);

        return new OkResult();
    }

    /* !!!THINK!!! Generally with many holdings, and TradeAdd's there is lot of potential issues w automatic FIFO base functionality.
    *             This really makes TradeDelete something that user has to be very carefull or can easily start messing up order of
    *             FIFO and make things off sync. Sadly cant see easy way limiting it here, as enforcing it requires enforcing
    *             holding add/edit orders and so many other hard to test limitiations.
    *             
    *             => Atm this is almost too powerfull to have, but then dont have edit so user must be able to do something to fix errors.
    */
    protected Result TradeDelete(string tradeId)
    {
        // Delete-Trade TradeId
        SPortfolio pf = _portfolios.Where(p => p.StockTrades.Any(h => h.Sold.TradeId == tradeId)).SingleOrDefault();

        if (pf == null)
            return new FailResult($"Cant find this tradeId");

        // Lets start by making sure that for each holding sold on under this TradeId has newer trades done
        // as things get way too complicate if trying to cancel anything but latest part of holdings trades

        foreach (SHolding trade in pf.StockTrades.Where(t => t.Sold.TradeId == tradeId))
        {
            if (pf.StockTrades.Where(t => t.PurhaceId == trade.PurhaceId && t.Sold.SaleDate > trade.Sold.SaleDate).Any())
                return new FailResult($"Cant delete this trade as need to trade newer trada first from purhace {trade.PurhaceId}");
        }

        // This is bit more complex, as it may effect to multiple trade elements under same portfolio, and those each 
        // is referring to different holding... and this is not just 'delete' to remove entry but units needs to be
        // returned to original holding. Luckily dividents are expected to be same so those are ok

        List<SHolding> remove = new();

        foreach ( SHolding trade in pf.StockTrades.Where(t => t.Sold.TradeId == tradeId) )
        {
            SHolding holding = pf.StockHoldings.Where(h => h.PurhaceId == trade.PurhaceId).SingleOrDefault();

            remove.Add(trade); // cant do on this foreach so need to separately

            if ( holding == null )
            {   // original holding is fully sold, so return this trade back to holdings
                trade.Sold = null;
                pf.StockHoldings.Add(trade);
            }
            else
            {   // this trade was piece of holding that still there, so just return units
                holding.Units += trade.Units;
            }
        }

        while ( remove.Count() > 0 )
        {
            SHolding r = remove.First();
            remove.RemoveAt(0);
            pf.StockTrades.Remove(r);
        }
        return new OkResult();
    }

    #endregion

    #region ORDERS

    protected Result OrderAdd(string pfName, SOrder.OrderType type, string sRef, decimal units, decimal pricePerUnit, DateOnly lastDate)
    {   // Add-Order PfName Type SRef Units Price LastDate
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        if (pf.StockOrders.SingleOrDefault(a => a.PricePerUnit == pricePerUnit && a.SRef == sRef) != null)
            // Multiple orders w same Price for one specific stock are NOT allowed, not even different types (as pricePerUnit is used as reference ID)
            return new FailResult($"Not allowed to set for same stock a multiple orders for same price!");

        pf.StockOrders.Add(new SOrder()
        {
            SRef = sRef,
            Type = type,
            Units = units,
            PricePerUnit = pricePerUnit,
            LastDate = lastDate,
            FillDate = null,
        });

        return new OkResult();
    }

    protected Result OrderEdit(string pfName, SOrder.OrderType type, string sRef, decimal editedPrice, decimal units, decimal pricePerUnit, DateOnly lastDate)
    {   // Edit-Order PfName Type SRef EditedPrice Units Price LastDate
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        // 'PricePerUnit' is used as ID to refer specific order under portfolio/stock
        SOrder order = pf.StockOrders.SingleOrDefault(a => a.PricePerUnit == editedPrice && a.SRef == sRef);

        if (order == null)
            return new FailResult($"Could not find requested order to edit!");

        order.Type = type;
        order.Units = units;
        order.PricePerUnit = pricePerUnit;
        order.LastDate = lastDate;
        order.FillDate = null;

        return new OkResult();
    }

    protected Result OrderDelete(string pfName, string sRef, decimal pricePerUnit)
    {   // Delete-Order PfName SRef Price
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        if (pf.StockOrders.SingleOrDefault(a => a.PricePerUnit == pricePerUnit && a.SRef == sRef) == null)
            // Doesnt exist
            return new FailResult($"Could not find requested order to delete!");

        pf.StockOrders.RemoveAll(a => a.PricePerUnit == pricePerUnit && a.SRef == sRef);

        return new OkResult();
    }

    protected Result OrderReset(string pfName, string sRef, decimal pricePerUnit)
    {   // Set-Order PfName SRef Price
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        SOrder order = pf.StockOrders.SingleOrDefault(a => a.PricePerUnit == pricePerUnit && a.SRef == sRef);

        if (order == null)
            // Doesnt exist
            return new FailResult($"Could not find requested order to reset!");

        order.FillDate = null;
        return new OkResult();
    }

    #endregion

    #region DIVIDENTS

    /* Divident's are more complex than most, as multiple potential cases:
     * - Normal case is where 'units' matches to currently owned holdings total units so can just assign them dividents directly
     * - Bit more complex case is where one/more latest holdings is purhaced too recently to be part of dividents
     * - Also cases where dividents are partially/fully targeted to already sold holding 
     */

    protected Result DividentAdd(string pfName, string sRef, string optPurhaceId, string optTradeId, DateOnly exDivDate, DateOnly paymentDate, decimal units,
                                       decimal paymentPerUnit, decimal currencyRate, CurrencyId currency)
    {   // Add-Divident PfName SRef OptPurhaceId OptTradeId ExDivDate PaymentDate Units PaymentPerUnit CurrencyRate
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null )
            return new FailResult($"{pfName} is unknown portfolio");

        // Lets start by collecting lists of potential holdings & trades those could be effected by these dividents
        List<SHolding> holdings = new();
        List<SHolding> trades = new();

        if ( string.IsNullOrWhiteSpace(optPurhaceId) == false && string.IsNullOrWhiteSpace(optTradeId) == false )
        {   // this case we only target very specific single record
            trades = pf.StockTrades.Where(t => t.PurhaceId == optPurhaceId && t.Sold.TradeId == optTradeId && t.SRef == sRef).ToList();
        }
        else if (string.IsNullOrWhiteSpace(optPurhaceId) == false)
        {   // this targets one holdings and potentially some of its sold parts (actually full holding maybe sold already but still here)
            holdings = pf.StockHoldings.Where(h => h.PurhaceId == optPurhaceId && h.SRef == sRef).ToList();
            //trades = pf.StockTrades.Where(t => t.PurhaceId == optPurhaceId && t.SRef == sRef).ToList();       <== hmm.. I think NOT.. if targets then targets specially that record
        }
        else
        {   // targets potentially all records toward this stock under portfolio
            holdings = pf.StockHoldings.Where(h => h.SRef == sRef).ToList();
            trades = pf.StockTrades.Where(t => t.SRef == sRef).ToList();
        }

        // Limit off those records from given lists those are not owned correct time...         !!!LATER!!! What exactly date limits for this? Depends broker also?
        holdings = holdings.Where(h => h.PurhaceDate < exDivDate).OrderBy(h => h.PurhaceDate).ToList();
        trades = trades.Where(t => t.PurhaceDate < exDivDate && t.Sold.SaleDate >= exDivDate).OrderBy(t => t.Sold.SaleDate).ToList();

        decimal totalUnits = holdings.Select(h => h.Units).Sum() 
                           + trades.Select(h => h.Units).Sum();

        // Think! Per current understanding these remaining records units should match exactly amount those got divident.. so lets do error if not
        if (totalUnits != units)
            return new FailResult($"{StalkerErr.UnitMismatch}! Hmm was expecting that divident would be given for {totalUnits} units, those owned on that time?");

        if ( holdings.Any(h => h.Dividents.Where(d => d.ExDivDate == exDivDate).Any()) ||       // TEST! This needs verifying, not sure if correctly linqed
             trades.Any(t => t.Dividents.Where(d => d.ExDivDate == exDivDate).Any()))
            // No duplicate dividents allowed
            return new FailResult($"{StalkerErr.Duplicate}! Not allowed to have same ExDivDate a multiple dividents");

        foreach ( SHolding holding in holdings ) // all holdings & trades those survive this far are rewarded with dividents
            holding.Dividents.Add(new SHolding.Divident(paymentPerUnit, exDivDate, paymentDate, currencyRate, currency));

        foreach ( SHolding trade in trades )
            trade.Dividents.Add(new SHolding.Divident(paymentPerUnit, exDivDate, paymentDate, currencyRate, currency));

        return new OkResult();
    }

    protected Result DividentDeleteAll(string pfName, string sRef, DateOnly exDivDate)
    {   // DeleteAll-Divident PfName SRef ExDivDate
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        pf.StockHoldings.Where(h => h.SRef == sRef).ToList().ForEach(h => h.Dividents.RemoveAll(d => d.ExDivDate == exDivDate));

        pf.StockTrades.Where(h => h.SRef == sRef).ToList().ForEach(h => h.Dividents.RemoveAll(d => d.ExDivDate == exDivDate));

        return new OkResult();
    }

    protected Result DividentDelete(string pfName, string sRef, DateOnly exDivDate, string purhaceId)
    {   // Delete-Divident PfName SRef ExDivDate PurhaceId
        SPortfolio pf = PortfolioRef(pfName);

        if (pf == null)
            return new FailResult($"{pfName} is unknown portfolio");

        pf.StockHoldings.Where(h => h.SRef == sRef && h.PurhaceId == purhaceId).ToList().ForEach(h => h.Dividents.RemoveAll(d => d.ExDivDate == exDivDate));

        pf.StockTrades.Where(h => h.SRef == sRef && h.PurhaceId == purhaceId).ToList().ForEach(h => h.Dividents.RemoveAll(d => d.ExDivDate == exDivDate));

        return new OkResult();
    }

    #endregion

    #region SECTORS

    protected Result SectorSet(int sectorId, string sectorName)
    {   // Set-Sector SectorId SectorName
        if (_sectors[sectorId] == null)
            _sectors[sectorId] = new SSector(sectorName);
        else
            _sectors[sectorId].Name = new string(sectorName);

        return new OkResult();
    }

    protected Result SectorDeleteAll(int sectorId)
    {   // DeleteAll-Sector Sector
        if (_sectors[sectorId] == null)
            return new FailResult($"Requested one is already deleted?");

        _stocks.ForEach(s => s.Sectors[sectorId] = -1);
        _sectors[sectorId] = null;
        return new OkResult();
    }

    protected Result SectorEdit(int sectorId, int fieldId, string fieldName)
    {   // Edit-Sector SectorId FieldId FieldName
        if (_sectors[sectorId] == null)
            return new FailResult($"Sector is uninitialized");

        _sectors[sectorId].FieldNames[fieldId] = new string (fieldName);

        return new OkResult();
    }

    protected Result SectorDelete(int sectorId, int fieldId)
    {   // Delete-Sector SectorId FieldId
        if (_sectors[sectorId] == null)
            return new FailResult($"Sector is uninitialized");

        // May no stock refer to this fieldId anymore, as gone is gone
        _stocks.Where(s => s.Sectors[sectorId] == fieldId).ToList().ForEach(s => s.Sectors[sectorId] = -1);
        _sectors[sectorId].FieldNames[fieldId] = null;
        return new OkResult();
    }

    protected Result SectorFollow(string sRef, int sectorId, int fieldId)
    {   // Follow-Sector SRef SectorId FieldId
        if (_sectors[sectorId] == null || _sectors[sectorId].FieldNames[fieldId] == null)
            return new FailResult($"Sector is uninitialized");

        SStock stock = GetOrAddStock(sRef);

        stock.Sectors[sectorId] = fieldId;

        return new OkResult();
    }

    protected Result SectorUnfollow(string sRef, int sectorId)
    {   // Unfollow-Sector SRef SectorId
        if (_sectors[sectorId] == null )
            return new FailResult($"Sector is uninitialized");

        SStock stock = StockRef(sRef);

        if (stock == null)
            return new FailResult($"Could not find stock");

        stock.Sectors[sectorId] = -1;

        return new OkResult();
    }

    #endregion
}
