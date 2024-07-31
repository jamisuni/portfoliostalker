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

namespace Pfs.Types;

// ReportCollection - Trades are moved from Stalker PF orientation to under RCStock 
public class RCTrade
{
    public SHolding ST {  get; internal set; } // always set, contains SRef

    public string PfName { get; internal set; }

    public RCTrade(SHolding trade, string pfName)
    {
        ST = trade;
        PfName = pfName;
    }
}