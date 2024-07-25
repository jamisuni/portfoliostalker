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

public enum AccountTypeId : int
{
    Unknown = 0,
    Offline,            // PFS can be used without login on, that case 'Offline' is used w 'Free' features
    Free,               // This is what u get by default w registeration, mainly local browser internal features
    Gold,               // 
    Platinum,           // All features, and access to premium server
    Demo,               // For v2 demo is now per fixed backup for fixed date
    DemoEdit,           // 'Permanent Platinum' for all Demo accounts, this is editing part of demo w normal user defined account password
    Admin,              // Try keep Admin's as separate accounts, with only focusing to use them for AdminUI login
    Server,             // Servers internal only
}

public static partial class Extensions
{
    public static bool IsAdmin(this AccountTypeId accountTypeId)
    {
        if (accountTypeId == AccountTypeId.Admin || accountTypeId == AccountTypeId.Server)
            return true;
        return false;
    }
    public static bool IsPremium(this AccountTypeId accountTypeId)
    {
        if (accountTypeId == AccountTypeId.Gold || accountTypeId == AccountTypeId.Platinum ||
            accountTypeId == AccountTypeId.DemoEdit || accountTypeId.IsAdmin() == true )
            return true;

        return false;
    }
}
