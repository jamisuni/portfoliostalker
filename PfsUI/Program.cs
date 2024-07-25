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

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Blazored.LocalStorage;
using Pfs.Client;
using Pfs.Config;
using Pfs.Types;
using PfsUI;
using Serilog;
using Pfs.Shared;
using BlazorDownloadFile;
using Pfs.ExtFetch;

// Think! Maybe 'AddSingleton' is not need, as its one session so AddScoped should be ok?

Log.Logger = new LoggerConfiguration()
            .WriteTo.BrowserConsole()
            .Enrich.FromLogContext()
            .CreateLogger();

Log.Information("Started PfsUI(Args: {a})", args);

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();
builder.Services.AddBlazorDownloadFile();
builder.Services.AddBlazoredLocalStorageAsSingleton();

builder.Services.AddSingleton<PfsClientAccess>();
builder.Services.AddSingleton<Client>();
builder.Services.AddSingleton<IFEWaiting>(x => x.GetRequiredService<Client>());
builder.Services.AddSingleton<ClientData>();
builder.Services.AddSingleton<PfsUiState>();
builder.Services.AddSingleton<IFECmdTerminal, ClientCmdTerminal>();
builder.Services.AddSingleton<IPfsPlatform, BlazorPlatform>();
builder.Services.AddSingleton<ClientReportPreCalcs>();


// !!!CODE!!! How to register a service with multiple interfaces, (2) from https://andrewlock.net/how-to-register-a-service-with-multiple-interfaces-for-in-asp-net-core-di/

// !!!THINK!!! All following could be just moved to Client... and all those classes can be internal's   !!!TODO!!! builder.Services.AddPfsClientServices()

builder.Services.AddSingleton<ClientContent>();         // Provides 'global' variables, events, and some multi component target operations
builder.Services.AddSingleton<IPfsStatus>(x => x.GetRequiredService<ClientContent>());

builder.Services.AddSingleton<FEAccount>();
builder.Services.AddSingleton<IFEAccount>(x => x.GetRequiredService<FEAccount>());

builder.Services.AddSingleton<FEConfig>();
builder.Services.AddSingleton<IFEConfig>(x => x.GetRequiredService<FEConfig>());

builder.Services.AddSingleton<FEStalker>();
builder.Services.AddSingleton<IFEStalker>(x => x.GetRequiredService<FEStalker>());

builder.Services.AddSingleton<FEReport>();
builder.Services.AddSingleton<IFEReport>(x => x.GetRequiredService<FEReport>());

builder.Services.AddSingleton<ClientStalker>();         // Owns & Stores actual portfolio information with all user ownings
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<ClientStalker>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<ClientStalker>());

builder.Services.AddSingleton<FetchConfig>(); 
builder.Services.AddSingleton<IPfsFetchConfig>(x => x.GetRequiredService<FetchConfig>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<FetchConfig>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<FetchConfig>());

builder.Services.AddSingleton<ProvConfig>();
builder.Services.AddSingleton<IPfsProvConfig>(x => x.GetRequiredService<ProvConfig>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<ProvConfig>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<ProvConfig>());

builder.Services.AddSingleton<MarketConfig>();
builder.Services.AddSingleton<IMarketMeta>(x => x.GetRequiredService<MarketConfig>());
builder.Services.AddSingleton<IPfsSetMarketConfig>(x => x.GetRequiredService<MarketConfig>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<MarketConfig>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<MarketConfig>());

builder.Services.AddSingleton<FetchRates>();
builder.Services.AddSingleton<IFetchRates>(x => x.GetRequiredService<FetchRates>());

builder.Services.AddSingleton<FetchEod>();
builder.Services.AddSingleton<IFetchEod>(x => x.GetRequiredService<FetchEod>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<FetchEod>());
builder.Services.AddSingleton<IOnUpdate>(x => x.GetRequiredService<FetchEod>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<FetchEod>());

builder.Services.AddSingleton<StoreLatestEod>();
builder.Services.AddSingleton<ILatestEod>(x => x.GetRequiredService<StoreLatestEod>());
builder.Services.AddSingleton<IChangeEod>(x => x.GetRequiredService<StoreLatestEod>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreLatestEod>());

builder.Services.AddSingleton<StoreLatesRates>();
builder.Services.AddSingleton<ILatestRates>(x => x.GetRequiredService<StoreLatesRates>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<StoreLatesRates>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreLatesRates>());

builder.Services.AddSingleton<StoreStockMetaHist>();
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreStockMetaHist>());

builder.Services.AddSingleton<StoreStockMeta>();
builder.Services.AddSingleton<IStockMeta>(x => x.GetRequiredService<StoreStockMeta>());
builder.Services.AddSingleton<IStockMetaUpdate>(x => x.GetRequiredService<StoreStockMeta>());
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<StoreStockMeta>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreStockMeta>());

builder.Services.AddSingleton<StoreUserEvents>();
builder.Services.AddSingleton<IUserEvents>(x => x.GetRequiredService<StoreUserEvents>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreUserEvents>());

builder.Services.AddSingleton<StoreNotes>();
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreNotes>());

builder.Services.AddSingleton<StoreReportFilters>();
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<StoreReportFilters>());

builder.Services.AddSingleton<AppConfig>();
builder.Services.AddSingleton<ICmdHandler>(x => x.GetRequiredService<AppConfig>());
builder.Services.AddSingleton<IDataOwner>(x => x.GetRequiredService<AppConfig>());

builder.Services.AddSingleton<StoreExtraColumns>();
builder.Services.AddSingleton<IExtraColumns>(x => x.GetRequiredService<StoreExtraColumns>());

await builder.Build().RunAsync();
