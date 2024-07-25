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

namespace Pfs.Config;

public interface IPfsProvConfig
{
    // Send if private key is changed or something else updated on configs by user/admin
    event EventHandler<ExtProviderId> EventProvConfigsChanged;

    // This is also way to know that provider is active, and can be used at all
    string GetPrivateKey(ExtProviderId provider);

    // Returns only those that has key set atm
    List<ExtProviderId> GetActiveProviders(); 
}
