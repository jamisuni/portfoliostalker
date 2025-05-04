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

using Pfs.Data;
using Pfs.Types;
using System.Collections.ObjectModel;
using static Pfs.Data.UserEvent;

namespace Pfs.Client;

public class FEAccount : IFEAccount
{
    protected IPfsPlatform _pfsPlatform;
    protected IPfsStatus _pfsStatus;
    protected ClientData _clientData;
    protected IMarketMeta _marketMetaProv;
    protected IStockMeta _stockMetaProv;
    protected StoreUserEvents _storeUserEvents;
    protected IStockNotes _stockNotes;

    public FEAccount(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus, ClientData clientData, IMarketMeta marketMetaProv, IStockMeta stockMetaProv, 
                     StoreUserEvents storeUserEvents, IStockNotes stockNotes)
    {
        _pfsPlatform = pfsPlatform;
        _pfsStatus = pfsStatus;
        _clientData = clientData;
        _marketMetaProv = marketMetaProv;
        _stockMetaProv = stockMetaProv;
        _storeUserEvents = storeUserEvents;
        _stockNotes = stockNotes;
    }

    public AccountTypeId AccountType { get { return _pfsStatus.AccountType; } }

    public int GetAppCfg(AppCfgId id)
    {
        return _pfsStatus.GetAppCfg(id);
    }

    public void SaveData()
    {
        _clientData.DoSaveData();
    }

    public void ClearLocally()
    {
        // Nukes EVERYTHING
        _pfsPlatform.PermClearAll();

        _clientData.DoInitDataOwners();
    }

    public Result LoadDemo(byte[] zip)
    {
        if (_pfsStatus.AccountType != AccountTypeId.Offline)
            return new FailResult("Cant load on this state");

        _pfsStatus.AllowUseStorage = false;

        List<string> warnings = _clientData.ImportFromBackupZip(zip);

        _pfsStatus.AccountType = AccountTypeId.Demo;
        return new OkResult();
    }

    public IEnumerable<MarketMeta> GetActiveMarketsMeta()
    {
        return _marketMetaProv.GetActives();
    }

    public MarketMeta GetMarketMeta(MarketId marketId)
    {
        return _marketMetaProv.Get(marketId);
    }

    public MarketStatus[] GetMarketStatus()
    {
        return _marketMetaProv.GetMarketStatus();
    }

    public UserEventAmounts GetUserEventAmounts()
    {
        return _storeUserEvents.GetAmounts();
    }

    public void UpdateUserEventStatus(int id, UserEventStatus status)
    {
        _storeUserEvents.UpdateUserEventStatus(id, status);
    }

    public void DeleteUserEvent(int id)
    {
        _storeUserEvents.DeleteUserEvent(id);
    }

    public List<RepDataUserEvents> GetUserEventsData()
    {
        List<RepDataUserEvents> ret = new();

        ReadOnlyCollection<StoreUserEvents.UserEventInfo> events = _storeUserEvents.GetAll();

        foreach (StoreUserEvents.UserEventInfo ev in events)
        {
            Dictionary<EvFieldId, object> prms = ev.Data.GetFields();
            StockMeta sm = _stockMetaProv.Get((string)prms[EvFieldId.SRef]);

            if (sm == null) // We do wanna have this, or would need to delete automatic
                sm = _stockMetaProv.AddUnknown((string)prms[EvFieldId.SRef]);

            RepDataUserEvents entry = new RepDataUserEvents()
            {
                Date = prms.ContainsKey(EvFieldId.Date) ? (DateOnly)prms[EvFieldId.Date] : DateOnly.MinValue,
                Type = (UserEventType)prms[EvFieldId.Type],
                Status = ev.Status,
                Id = ev.Id,
                StockMeta = sm,
            };

            if (prms.ContainsKey(EvFieldId.Portfolio))
                entry.PfName = (string)prms[EvFieldId.Portfolio];

            switch (entry.Type)
            {
                case UserEventType.OrderBuy:
                case UserEventType.OrderSell:
                case UserEventType.OrderBuyExpired:
                case UserEventType.OrderSellExpired:
                    entry.Order = new()
                    {
                        PricePerUnit = (decimal)prms[EvFieldId.Value],
                        Units = (decimal)prms[EvFieldId.Units],
                    };

                    if ( entry.Type == UserEventType.OrderBuyExpired || entry.Type == UserEventType.OrderBuy)
                        entry.Order.Type = SOrder.OrderType.Buy;
                    else
                        entry.Order.Type = SOrder.OrderType.Sell;
                    break;

                case UserEventType.AlarmOver:
                case UserEventType.AlarmUnder:
                case UserEventType.OrderTrailingSell:
                case UserEventType.OrderTrailingBuy:
                    entry.Alarm = new()
                    {
                        AlarmValue = (decimal)prms[EvFieldId.Value],
                        DayClosed = (decimal)prms[EvFieldId.EodClose],
                    };

                    if (prms.ContainsKey(EvFieldId.EodLow))
                        entry.Alarm.DayLow = (decimal)prms[EvFieldId.EodLow];

                    if (prms.ContainsKey(EvFieldId.EodHigh))
                        entry.Alarm.DayHigh = (decimal)prms[EvFieldId.EodHigh];

                    if (prms.ContainsKey(EvFieldId.AlarmDropP))
                        entry.Alarm.AlarmDropP = (decimal)prms[EvFieldId.AlarmDropP];

                    if (prms.ContainsKey(EvFieldId.AlarmRecoverP))
                        entry.Alarm.AlarmRecoverP = (decimal)prms[EvFieldId.AlarmRecoverP];

                    break;
            }
            ret.Add(entry);
        }
        return ret;
    }

    public Note GetNote(string sRef)
    {
        return _stockNotes.Get(sRef);
    }

    public void StoreNote(string sRef, Note note)
    {
        _stockNotes.Store(sRef, note);
    }

    public byte[] ExportAccountBackupAsZip()
    {
        return _clientData.ExportAccountBackupAsZip();
    }

    public List<string> ImportAccountFromZip(byte[] zip)
    {
        // This is expected to be done here, as Import atm is only tested to clean account (merging is NOT supported)
        ClearLocally();

        // returns warnings
        return _clientData.ImportFromBackupZip(zip);
    }

    public byte[] ExportStorageDumpAsZip(string startupWarnings)
    {   // Debug purposes, gets all content of local storage to file
        return _clientData.ExportStorageDumpAsZip(startupWarnings);
    }
}
