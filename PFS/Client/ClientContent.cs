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

using Pfs.Config;
using Pfs.Types;

namespace Pfs.Client;

// Provides high level state information for client components
public class ClientContent : IPfsStatus
{
    protected readonly AppConfig _appConfig = null;

    protected AccountTypeId _accountTypeId = AccountTypeId.Offline;

    protected bool _allowUseStorage = true;

    public ClientContent(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    /* IStatus: Main provider all 'global' information to components
     * --------
     * - AccountProperties? Ala additional pay etc features -> split them separate properties!
     */

    public AccountTypeId AccountType { get { return _accountTypeId; } set { _accountTypeId = value; } } // 'IStatus'

    public bool AllowUseStorage { get { return _allowUseStorage; } set { _allowUseStorage = value; } }

    public event IPfsStatus.CallbackEvPfsClientArgs EvPfsClientAsync;

    public async Task SendPfsClientEvent(PfsClientEventId id, object data = null)
    {
        await EvPfsClientAsync.Invoke(new PfsClientEventArgs(id, data));
    }

    public int GetAppCfg(string id)
    {
        return GetAppCfg((AppCfgId)Enum.Parse(typeof(AppCfgId), id));
    }

    public int GetAppCfg(AppCfgId id)
    {
        return _appConfig.Get(id);
    }
}
