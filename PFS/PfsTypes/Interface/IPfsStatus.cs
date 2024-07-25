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

// Global 'variables', 'states' and 'control'
public interface IPfsStatus
{
    AccountTypeId AccountType { get; set; }

    bool AllowUseStorage { get; set; }

    Task SendPfsClientEvent(PfsClientEventId id, object data = null);

    delegate Task CallbackEvPfsClientArgs(PfsClientEventArgs args);
    event CallbackEvPfsClientArgs EvPfsClientAsync;                 // <= register for 'global' PFS Client events

    int GetAppCfg(string id); // Also match to 'AppCfgId' enums

    int GetAppCfg(AppCfgId id);
}
