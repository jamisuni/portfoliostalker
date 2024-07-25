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

using Pfs.Types;

namespace Pfs.Client;

public class ClientScheduler
{
    protected IPfsPlatform _platform;
    protected IEnumerable<IOnUpdate> _onUpdateClients;

    public ClientScheduler(IEnumerable<IOnUpdate> onUpdateClients, IPfsPlatform platform)
    {
        _platform = platform;
        _onUpdateClients = onUpdateClients;
    }

    public async Task OnUpdateAsync()
    {
        DateTime dateTime = DateTime.UtcNow;
        
        foreach ( var client in _onUpdateClients )
        {
            await client.OnUpdateAsync(dateTime);
        }
    }
}
