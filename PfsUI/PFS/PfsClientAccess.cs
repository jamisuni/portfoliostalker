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

using Pfs.Client;
using Pfs.Types;
using System.Reflection;

namespace PfsUI;

public class PfsClientAccess
{
    public string PfsClientVersionNumber { get; internal set; } = "N/A";

    protected IPfsPlatform _platform;
    protected Client _client;
    protected IFEStalker _feStalker;
    protected IFECmdTerminal _feCmd;
    protected IFEAccount _feAccount;
    protected IFEConfig _feConfig;
    protected IFEClient _feClient;
    protected IFEReport _feReport;

    public PfsClientAccess(IPfsPlatform pfsClientPlatform, Client client, IFEStalker stalker, IFECmdTerminal clientCmdTerminal, IFEAccount feAccount, IFEConfig feConfig, IFEClient feWaiting, IFEReport feReport)
    {
        _platform = pfsClientPlatform;
        _client = client;
        _feStalker = stalker;
        _feCmd = clientCmdTerminal;

        // Note! Version number from project settings
        PfsClientVersionNumber = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

        _feAccount = feAccount;
        _feConfig = feConfig;
        _feClient = feWaiting;
        _feReport = feReport;
    }


    // !!!MUST DO!!! Be carefull here, this DI of classes allows to start uncontrolled usage of internal things
    //               and this FE->PFS needs to be kept minimal and clear => MUST HAVE INTERFACE FOR THAT REASON! 
    //               and really it should be pretty must interface thats just for this! So FeApi! 
    //               => This most propably means that PfsUI should only depend to Client? and Types


    public ref IFECmdTerminal Cmd() { return ref _feCmd; }

    public ref IFEAccount Account() { return ref _feAccount; }

    public ref IFEConfig Config() {  return ref _feConfig; }

    public ref IFEStalker Stalker() { return ref _feStalker; }

    public ref IFEClient Client() { return ref _feClient; }

    public ref IFEReport Report()   { return ref _feReport; }

    public ref IPfsPlatform Platform() { return ref _platform; }
}

