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
using System.Text;

using Serilog;

using Pfs.Types;

using static Pfs.Data.UserEvent;

namespace Pfs.Data;

// These event's track things are shown user daily as potentially important, like: Triggered Alarms, Order Expires, etc
public class StoreUserEvents : IUserEvents, IDataOwner
{
    protected const string _componentName = "events";
    protected readonly IPfsPlatform _platform;
    protected IPfsStatus _pfsStatus;

    public class UserEventInfo
    {
        protected static int _runningID = 1;  // Next available ID for assigning (memory only ID)

        public UserEventStatus Status { get; internal set; }
        public int Id { get; internal set; }
        public UserEvent Data { get; internal set; }

        public UserEventInfo(UserEventStatus status, UserEvent data)
        {
            Status = status;
            Data = data;
            Id = _runningID++;
        }
    }

    protected List<UserEventInfo> _events = null;

    public StoreUserEvents(IPfsPlatform pfsPlatform, IPfsStatus pfsStatus)
    {
        _platform = pfsPlatform;
        _pfsStatus = pfsStatus;

        LoadStorageContent();
    }

    protected void Init()
    {
        _events = new();
    }

    public void CreateAlarmTriggerEvent(string sRef, SAlarm alarm, FullEOD eod)
    {
        Dictionary<EvFieldId, object> evPrms = new() {
            { EvFieldId.Type,       alarm.AlarmType },
            { EvFieldId.SRef,       sRef },   
            { EvFieldId.Date,       eod.Date },
            { EvFieldId.Value,      alarm.Level },
            { EvFieldId.EodClose,   eod.Close },
        };

        if ( alarm.AlarmType == SAlarmType.Over && eod.HasHigh())
            evPrms.Add(EvFieldId.EodHigh, eod.GetSafeHigh());

        else if ( alarm.AlarmType == SAlarmType.Under && eod.HasLow()) 
            evPrms.Add(EvFieldId.EodLow, eod.GetSafeLow());

        else if ( alarm.AlarmType == SAlarmType.TrailingSellP )
            evPrms.Add(EvFieldId.AlarmDropP, (alarm as SAlarmTrailingSellP).DropP);

        _events.Add(new UserEventInfo(UserEventStatus.Unread, Create(evPrms)));

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public void CreateOrderExpiredEvent(string sRef, string pfName, SOrder order)
    {
        Dictionary<EvFieldId, object> evPrms = new() {
            { EvFieldId.SRef,       sRef },
            { EvFieldId.Portfolio,  pfName },
            { EvFieldId.Date,       order.LastDate },
            { EvFieldId.Value,      order.PricePerUnit },
            { EvFieldId.Units,      order.Units },
        };

        if (order.Type == SOrder.OrderType.Buy)
            evPrms.Add(EvFieldId.Type, UserEventType.OrderBuyExpired);
        else if (order.Type == SOrder.OrderType.Sell)
            evPrms.Add(EvFieldId.Type, UserEventType.OrderSellExpired);
        else
            throw new NotImplementedException();

        _events.Add(new UserEventInfo(UserEventStatus.Unread, Create(evPrms)));

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public void CreateOrderTriggerEvent(string sRef, string pfName, SOrder order, FullEOD eod)
    {
        Dictionary<EvFieldId, object> evPrms = new() {
            { EvFieldId.SRef,       sRef },
            { EvFieldId.Portfolio,  pfName },
            { EvFieldId.Date,       eod.Date },
            { EvFieldId.Value,      order.PricePerUnit },
            { EvFieldId.Units,      order.Units },
        };

        if (order.Type == SOrder.OrderType.Buy)
            evPrms.Add(EvFieldId.Type, UserEventType.OrderBuy);
        else if (order.Type == SOrder.OrderType.Sell)
            evPrms.Add(EvFieldId.Type, UserEventType.OrderSell);

        _events.Add(new UserEventInfo(UserEventStatus.Unread, Create(evPrms)));

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public void CreateAvrgOwning2NegEvent(string sRef, string pfName, DateOnly date)
    {
        Dictionary<EvFieldId, object> evPrms = new() {
            { EvFieldId.Type,       UserEventType.OwningNegative},
            { EvFieldId.SRef,       sRef },
            { EvFieldId.Portfolio,  pfName },
            { EvFieldId.Date,       date }
        };

        _events.Add(new UserEventInfo(UserEventStatus.Unread, Create(evPrms)));

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public void CreateAvrgOwning2PosEvent(string sRef, string pfName, DateOnly date)
    {
        Dictionary<EvFieldId, object> evPrms = new() {
            { EvFieldId.Type,       UserEventType.OwningPositive},
            { EvFieldId.SRef,       sRef },
            { EvFieldId.Portfolio,  pfName },
            { EvFieldId.Date,       date }
        };

        _events.Add(new UserEventInfo(UserEventStatus.Unread, Create(evPrms)));

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public UserEventAmounts GetAmounts()
    {
        int unread = 0;
        int unreadImp = 0;
        int read = 0;
        int starred = 0;

        foreach (UserEventInfo ev in _events )
        {
            switch (ev.Status)
            {
                case UserEventStatus.Unread: unread++; break;
                case UserEventStatus.UnreadImp: unreadImp++; break;
                case UserEventStatus.Read: read++; break;
                case UserEventStatus.Starred: starred++; break;
            }
        }
        return new UserEventAmounts(unread, unreadImp, read, starred);
    }

    public ReadOnlyCollection<UserEventInfo> GetAll()
    {
        return _events.AsReadOnly();
    }

    public void UpdateUserEventStatus(int id, UserEventStatus status)
    {
        _events.Single(e => e.Id == id).Status = status;

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public void DeleteUserEvent(int id)
    {
        if (id == 0)
        {
            // ID=0 means deleteAll, but we do it two steps.. as if has only starred/important ones then we delete all as all!
            //      if has mix of starred/important with some other state events, then we save starred/imp and just delete 'rest' 

            if (_events.Any(e => e.Status != UserEventStatus.Starred && e.Status != UserEventStatus.UnreadImp))
                _events.RemoveAll(e => e.Status != UserEventStatus.Starred && e.Status != UserEventStatus.UnreadImp);
            else
                _events = new();
        }
        else
            _events.RemoveAll(e => e.Id == id);

        EventNewUnsavedContent?.Invoke(this, _componentName);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.UserEventStatus, GetAmounts());
    }

    public event EventHandler<string> EventNewUnsavedContent;                                       // IDataOwner
    public string GetComponentName() { return _componentName; }
    public void OnDataInit() { Init(); }

    public void OnDataSaveStorage()
    {
        _platform.PermWrite(_componentName, CreateStorageFormatContent());
    }

    public string CreateBackup() // Plan! No backups for UserEvents
    {
        return string.Empty;
    }

    public string CreatePartialBackup(List<string> symbols)
    {
        return string.Empty;
    }

    public Result RestoreBackup(string content)
    {
        Init();

        return new OkResult();
    }

    protected string CreateStorageFormatContent()
    {
        StringBuilder store = new();

        foreach (UserEventInfo ev in _events)
        {
            string status = ev.Status.GetEnumMemberValue() + '#';

            store.AppendLine(status + ev.Data.GetStorageFormat());
        }

        return store.ToString();
    }

    public void LoadStorageContent()
    {
        try
        {
            Init();

            string stored = _platform.PermRead(_componentName);

            if (string.IsNullOrWhiteSpace(stored))
                return;

            using (StringReader reader = new StringReader(stored))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line[1] != '#')
                        continue;

                    UserEventStatus status = EnumExtensions.ConvertBack<UserEventStatus>(line.Substring(0, 1));

                    _events.Add(new UserEventInfo(status, new UserEvent(line.Substring(2))));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"{_componentName} LoadStorageContent failed to exception: [{ex.Message}]");
            Init();
            _platform.PermRemove(_componentName);
        }
    }
}
