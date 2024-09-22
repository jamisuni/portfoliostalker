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
    protected ILatestEod _latestEod;
    protected StoreLatesRates _storeLatestRates;
    protected IFetchRates _fetchRates;
    protected ILatestRates _latestRatesProv;
    protected IUserEvents _userEvents;
    protected ClientReportPreCalcs _reportPreCalcCollection;
    protected Timer _timer;
    protected bool _busy = false;

    public Client(IEnumerable<IOnUpdate> onUpdateClients, IPfsStatus pfsStatus, ClientData clientData, ClientStalker clientStalker, IPfsPlatform platform, ClientReportPreCalcs reportPreCalcCollection,
                  StoreLatestEod storeLatestEod, StoreLatesRates storeLatestRates, IFetchRates fetchRates, ILatestRates latestRatesProv, ILatestEod latestEod, IUserEvents userEvents)
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
        _reportPreCalcCollection = reportPreCalcCollection;

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

    public void AddEod(MarketId marketId, string symbol, FullEOD eod)
    {
        _storeLatestEod.Store(marketId, symbol, [eod]);

        _pfsStatus.SendPfsClientEvent(PfsClientEventId.StockUpdated, $"{marketId}${symbol}");
    }

    protected async Task OnPfsClientEventHandlerAsync(PfsClientEventArgs args)  // Only consumer of PfsClientLib side events!
    {
        switch (args.ID)                              // *** Maps events from PfsLibComp to operations/etc ***
        {
            case PfsClientEventId.FetchEodsFinished:
                _reportPreCalcCollection.InitClean();
                break;

            case PfsClientEventId.StoredLatestEod:

                string sRef = args.data as string;
                FullEOD eod = _latestEod.GetFullEOD(sRef);

                ProcessLatestEodPerAlarmsForUserEvents(sRef, eod);
                ProcessLatestEodPerOrders(sRef, eod);
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

    protected void ProcessLatestEodPerAlarmsForUserEvents(string sRef, FullEOD eod)
    {
        try
        {
            SStock stock = _clientStalker.StockRef(sRef);

            if (stock == null || stock.Alarms == null || stock.Alarms.Count == 0)
                return;

            foreach (SAlarm alarm in stock.Alarms)
            {
                if (alarm.AlarmType.IsUnderType() && alarm.GetAlarmDistance(eod.GetSafeLow()).procent >= 0 ||
                     alarm.AlarmType.IsOverType() && alarm.GetAlarmDistance(eod.GetSafeHigh()).procent >= 0)
                {   // Alarm level has been passed, at least momentarily so lets create user event
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
