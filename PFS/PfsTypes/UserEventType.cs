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

using System.Runtime.Serialization;

namespace Pfs.Types;

[DataContract]
public enum UserEventType : int
{
    Unknown = 0,
    [EnumMember(Value = "EB")]
    OrderBuyExpired,
    [EnumMember(Value = "ES")]
    OrderSellExpired,
    [EnumMember(Value = "B")]
    OrderBuy,
    [EnumMember(Value = "S")]
    OrderSell,
    [EnumMember(Value = "U")]
    AlarmUnder,
    [EnumMember(Value = "O")]
    AlarmOver,
}

[DataContract]
public enum UserEventStatus : int // Following values are used on local storage (must! be one letters as 'UserEvent:UpdateStatus')
{
    Unknown = 0,
    [EnumMember(Value = "U")]
    Unread,                     // orange
    [EnumMember(Value = "I")]
    UnreadImp,                  // red
    [EnumMember(Value = "R")]
    Read,                       // yellow
    [EnumMember(Value = "S")]
    Starred,                    // red (== user market as important)
}

public record UserEventAmounts(int Unread, int UnreadImp, int Read, int Starred)
{
    public int Total { get {  return Unread + UnreadImp + Read + Starred;} }
}

/* !!!DOCUMENT!!! UserEvents
 * 
 * Plan:
 * - Triggered events are shown on UI with separate popup dialog that is opened from Page Header
 * - Dedicated StoreUserEvents component is used to keep them
 * - 'UserEvent' provides compact storage format for events, capsulated by simple to access read functions
 */
