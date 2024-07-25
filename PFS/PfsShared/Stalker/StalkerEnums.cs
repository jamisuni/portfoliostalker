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

namespace Pfs.Shared.Stalker;

public enum StalkerOperation : int
{
    Unknown = 0,
    Add,
    Edit,
    Delete,
    Move,
    DeleteAll,                  // Atm w Alarms, allowing remove all alarms from specific stock, used by ImportWaveAlarms to clean table before setting again
    Set,                        // Mostly special commands w optional parameters to do different extension operations
    Top,                        // Allows to edit default order of things, by pushing defined one top on list
    Follow,                     // Mainly for Follow-Group [group] [stock] to add stocks to stock group
    Unfollow,                   // And similarly mainly to remove stocks from stock group
    Note,                       // Holdings, and specially Trade etc allow editing of note wo touching anything else using this command
    Round,
    Close,
}

public enum StalkerElement : int
{
    Unknown = 0,
    Portfolio,
    Stock,
    Holding,                    // Actual owning of specific Stock on PFS
    Trade,                      // PFS uses this as finalized transaction from Buy->Holding->Sale is called Trade, ala closed position for holding
    Alarm,
    Order,                      // Sell/Buy order waiting to filled up assuming price goes proper level in time order is on market
    Divident,                   // Dividents are always added to specific single holding on time
    Sector,                     // User defined Sector/Field combos to group stocks per different criterias
}

public enum StalkerErr
{
    Duplicate,
    UnitMismatch,
}
