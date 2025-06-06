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

using Pfs.ExtFetch;
using Pfs.Data;
using Pfs.Types;
using Serilog;
using static Pfs.Client.IFEClient;

namespace Pfs.Client;

public class Client : IDisposable, IFEClient
{
    // All Client -> MudUI events.. with dual identical events so that this is for PageHeader only!
    private EventHandler<FeEventArgs> evPfsClient2PHeader;
    public event EventHandler<FeEventArgs> EventPfsClient2PHeader
    {
        add
        {
            evPfsClient2PHeader = null;
            evPfsClient2PHeader += value;

            // Note! This is risky for order, but seams to be well as BE side is initialized first.
            // At startup loading time 'ClientData' initializes all 'IDataOwners' using 'OnLoadStorage'
            // and collects returned errors those happen on loading locally stored data. These errors
            // are passed here to UI side PageHeader to be shown for user as means some data was lost!
            List<string> startupWarnings = _clientData.StartupWarnings;
            if ( startupWarnings.Count() > 0)
                _pfsStatus.SendPfsClientEvent(PfsClientEventId.StartupWarnings, startupWarnings);
        }
        remove
        {
            evPfsClient2PHeader -= value;
        }
    }

    private EventHandler<FeEventArgs> evPfsClient2Page;
    public event EventHandler<FeEventArgs> EventPfsClient2Page
    {
        add
        {
            evPfsClient2Page = null;
            evPfsClient2Page += value;
        }
        remove
        {
            evPfsClient2Page -= value;
        }
    }

    protected IPfsStatus _pfsStatus;
    protected ClientScheduler _scheduler;
    protected IPfsPlatform _platform;
    protected ClientData _clientData;
    protected ClientStalker _clientStalker;
    protected StoreLatestEod _storeLatestEod;
    protected IEodLatest _latestEod;
    protected StoreLatesRates _storeLatestRates;
    protected IFetchRates _fetchRates;
    protected ILatestRates _latestRatesProv;
    protected IUserEvents _userEvents;
    protected IEodHistory _eodHistoryProv;
    protected IMarketMeta _marketMetaProv;
    protected Timer _timer;
    protected bool _busy = false;

    public Client(IEnumerable<IOnUpdate> onUpdateClients, IPfsStatus pfsStatus, ClientData clientData, ClientStalker clientStalker, IPfsPlatform platform, 
                  StoreLatestEod storeLatestEod, StoreLatesRates storeLatestRates, IFetchRates fetchRates, 
                  ILatestRates latestRatesProv, IEodLatest latestEod, IUserEvents userEvents, IEodHistory eodHistoryProv, IMarketMeta marketMetaProv)
    {
        _platform = platform;
        _pfsStatus = pfsStatus;
        _storeLatestEod = storeLatestEod;
        _latestEod = latestEod;
        _storeLatestRates = storeLatestRates;
        _clientData = clientData;
        _clientStalker = clientStalker;
        _fetchRates = fetchRates;
        _latestRatesProv = latestRatesProv;
        _userEvents = userEvents;
        _eodHistoryProv = eodHistoryProv;
        _marketMetaProv = marketMetaProv;

        _scheduler = new(onUpdateClients, _platform);
        _pfsStatus.EvPfsClientAsync += OnPfsClientEventHandlerAsync; // events from comps -> here Client

        _timer = new Timer(async _ =>
        {
            await OnUpdateAsync();
        }, null, 0, 1000);
    }

    private async Task OnUpdateAsync()
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            await _scheduler.OnUpdateAsync();
        }
        catch ( Exception )
        {
        }
        _busy = false;
    }

    protected async Task OnPfsClientEventHandlerAsync(PfsClientEventArgs args)  // Only consumer of PfsClientLib side events!
    {
        await Task.CompletedTask;

        switch (args.ID)                              // *** Maps events from PfsLibComp to operations/etc ***
        {
            case PfsClientEventId.StoredLatestEod:

                string sRef = args.data as string;
                FullEOD eod = _latestEod.GetFullEOD(sRef);

                ProcessLatestEodPerAlarmsForUserEvents(sRef, eod);
                ProcessLatestEodPerOrders(sRef, eod);
                // NEG & POS events, telling when PF's owning moves to loosing or winning position
                ProcessLatestEodPerHoldingLvlEvents(sRef, eod);
                break;

            case PfsClientEventId.ReceivedEod:      // From FetchEod -> IStoreEod
                ReceivedEodArgs recvEodArgs = args.data as ReceivedEodArgs;
                _storeLatestEod.Store(recvEodArgs.market, recvEodArgs.symbol, recvEodArgs.data);
                break;

            case PfsClientEventId.ReceiveRates:      // From FetchEod -> IStoreEod
                ReceiveRatesArgs recvRatesArgs = args.data as ReceiveRatesArgs;
                _storeLatestRates.Store(recvRatesArgs.date, recvRatesArgs.rates);
                break;
        }

        switch (args.ID)                                // *** Forwards events to FE/UI ***
        {
            case PfsClientEventId.ReceivedEod: // -> instead use StoredLatestEod
            case PfsClientEventId.ReceiveRates:
                // These are *not* allowed to be passed for UI
                break;

            case PfsClientEventId.StartupWarnings:
            case PfsClientEventId.FetchEodsStarted:
            case PfsClientEventId.FetchEodsFinished:
            case PfsClientEventId.StatusUnsavedData:
            case PfsClientEventId.UserEventStatus:
            case PfsClientEventId.StoredLatestEod:
            case PfsClientEventId.StockAdded:
            case PfsClientEventId.StockUpdated:
                {
                    var feArgs = new FeEventArgs()
                    {
                        Event = args.ID.ToString(),
                        Data = args.data
                    };
                    evPfsClient2PHeader?.Invoke(this, feArgs); // Note! Passed thru IFEWaiting!
                    evPfsClient2Page?.Invoke(this, feArgs);
                }
                break;
        }
    }

    protected void ProcessLatestEodPerHoldingLvlEvents(string sRef, FullEOD eod)
    {
        try 
        {
            int holdingLvlPeriod = _pfsStatus.GetAppCfg(AppCfgId.HoldingLvlPeriod);

            if (AppCfgLimit.HoldingLvlPeriodMin <= holdingLvlPeriod && holdingLvlPeriod <= AppCfgLimit.HoldingLvlPeriodMax)
                // 'HoldingLvlEvent's are automatic events to notify when example avrg holding is dropping on loosing or back to winning
                HoldingLvlEvents.CheckAndCreateNegPosEvents(sRef, holdingLvlPeriod, eod, _eodHistoryProv, _latestRatesProv, _marketMetaProv, _clientStalker, _userEvents);



        }
        catch (Exception ex)
        {
            Log.Warning($"ProcessLatestEodPerHoldingLvlEvents for {sRef} failed to exception: {ex.Message}");
        }
    }

    protected void ProcessLatestEodPerAlarmsForUserEvents(string sRef, FullEOD eod)
    {
        try
        {
            SStock stock = _clientStalker.StockRef(sRef);

            if (stock == null)
                return;

            foreach (SAlarm alarm in stock.Alarms)
            {
                if (alarm.IsAlarmTriggered(eod))
                {
                    if (alarm.AlarmType == SAlarmType.TrailingBuyP && _pfsStatus.GetAppCfg(AppCfgId.UseBetaFeatures) == 0)
                        continue;

                    // Alarm level has been passed, at least momentarily, so lets create user event
                    _userEvents.CreateAlarmTriggerEvent(sRef, alarm, eod);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"ProcessLatestEodPerAlarmsForUserEvents for {sRef} failed to exception: {ex.Message}");
        }
    }

    protected void ProcessLatestEodPerOrders(string sRef, FullEOD eod)
    {
        try
        {
            foreach (SPortfolio pf in _clientStalker.Portfolios())
            {
                List<SOrder> removeOrder = new();

                foreach (SOrder order in _clientStalker.PortfolioOrders(pf.Name, sRef) )
                {
                    if (order.FillDate == null)
                    {   // See if order has been triggered 
                        if (order.Type == SOrder.OrderType.Buy  && order.PricePerUnit >= eod.GetSafeLow() ||
                            order.Type == SOrder.OrderType.Sell && order.PricePerUnit <= eod.GetSafeHigh() )
                        {
                            _userEvents.CreateOrderTriggerEvent(sRef, pf.Name, order, eod);
                            order.FillDate = eod.Date;
                        }
                    }

                    if ( order.LastDate <= eod.Date )
                    {   // order has been expired => user event & remove order
                        _userEvents.CreateOrderExpiredEvent(sRef, pf.Name, order);
                        removeOrder.Add(order);
                    }
                }

                foreach (SOrder remove in removeOrder)
                {   // Delete-Order PfName SRef Price
                    string cmd = $"Delete-Order PfName=[{pf.Name}] SRef=[{remove.SRef}] Price=[{remove.PricePerUnit}]";
                    _clientStalker.DoAction(cmd);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"ProcessLatestEodPerOrders for {sRef} failed to exception: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
