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

namespace Pfs.Types;

public enum PfsClientEventId
{
    Unknown = 0,

    StartupWarnings,                // (data=List<string>) Something was failing on loading local content on startup

    FetchEodsStarted,               // (data=null) Allows different fetch operations to control 'busy' spinner on pageheader

    FetchEodsFinished,              // (data=null) Send when last of EOD fetch requests finished -> report instand update for FE

    StatusUnsavedData,              // (data=bool) true has unsaved data, false all saved

    StoredLatestEod,                // (data=sRef) Send by EOD storage when new EOD is stored -> ex alarm processing / report upd

    UserEventStatus,                // (data=UserEventAmounts) Send by UserEvent storage, when added/remove/status of event changed

    ReceivedEod,                    // (data=ReceivedEodArgs) Send from FetchEod (not to be passed UI)

    ReceiveRates,                   // (data=ReceiveRatesArgs) Send from FetchRated (not to be used by UI)

    StockAdded,                     // (data=sRef) 

    StockUpdated,                   // (data=sRef) 
}

public record PfsClientEventArgs(PfsClientEventId ID, object data);

/* !!!DOCUMENT!!! Client Events & FE Events - reacting example to EOD fetch finished on reports to reload
 * 
 * Plan: Different PFS-Lib side components send events with '_pfsStatus.SendPfsClientEvent' but on library
 *       side only allowed one to register/handle these events is 'Pfs.Client' w 'OnPfsClientEventHandlerAsync'
 *       => Only one consumer prevents events to turn dependency mess between components, and 
 *          Pfs.Client can then call components real functions as need per events (=> centralized handling)
 *          (PfsComp -> PfsStatus -> PfsClient -> call some component function to perform something)
 *                                            (-> forward event to FE as 'FeEventArgs')
 * 
 * Plan: 
 * 
 * Components:
 *      'PfsClientEventId' Pfs Client Library side events as enum base
 *      'ClientContent : IPfsStatus' implements 'SendPfsClientEvent' that event is handled by Pfs.Client
 *      'Pfs.Client' is only library side handler of events with 'OnPfsClientEventHandlerAsync' function
 *      'ClientData' owner of 'IDataOwner's collecting all changes and posting 'StatusUnsavedData' -event
 * 
 * Exceptions:
 *  - 'ClientData' thats owner of all 'IDataOwner' gets directly called by owners when 'dirty', 
 *     and related master information is kept on ClientData that then per actual  'first dirty' and 
 *     'no more dirty' situations creates a 'StatusUnsavedData' event that goes also FE/UI

 
 * FE side: FeEventArgs
 * - Search: 'OnEventPfsFe' as function on UI side to catch these events
 * - On UI has its own passing system of events, but also 'PfsClientEventId' gets passed there 
 * - UI cannot send events to PFS Client, so UI oriented events are just for UI components
 * - 
 * 
 * 
 * 
 * 
 * Think! If wants to queue events someday, then can do it just by adding queue on Pfs.Client and handling 
 *        it under timeslots... but threads could be issue for UI so may need another queue there....
 */

public record ReceivedEodArgs(MarketId market, string symbol, FullEOD[] data);

public record ReceiveRatesArgs(DateOnly date, CurrencyId homeCurrency, CurrencyRate[] rates);
