using Pfs.Config;
using Pfs.Types;

namespace Pfs.CmdLine;

public class PfsCmdLine : IPfsCmdLine
{
    IPfsGetFetchConfig? _getFetchConfig = null;
    IPfsSetFetchConfig? _setFetchConfig = null;

    IPfsGetProvConfig? _getProvConfig = null;
    IPfsSetProvConfig? _setProvConfig = null;

    public PfsCmdLine(IServiceProvider serviceProvider)
    {
        _getFetchConfig = serviceProvider.GetService(typeof(IPfsGetFetchConfig)) as IPfsGetFetchConfig;
        _setFetchConfig = serviceProvider.GetService(typeof(IPfsGetFetchConfig)) as IPfsSetFetchConfig;
        _getProvConfig = serviceProvider.GetService(typeof(IPfsGetFetchConfig)) as IPfsGetProvConfig;
        _setProvConfig = serviceProvider.GetService(typeof(IPfsGetFetchConfig)) as IPfsSetProvConfig;


#if false

var myInterfaces = serviceProvider.GetServices<IMyInterface>();
foreach (var myInterface in myInterfaces)
{
    myInterface.MyMethod();
}

#endif
    }

    /* FETCH CONFIGS: (implements Interface thats tied up to 'fcfg' and 'fetchcfg')
     * - list: returns list of current configs, giving each ID 1,2,3,4
     * 
     * 
     * 
     * - Implement somekind of mask parser, that can be told general layout of specific commands, and names of fields
     *   and it returns it parsed w dictionary<string,string> where key = name of field and value = value
     *   => Pfs.Helpers
     * 
     */



    public async Task<Result<string>> CmdAsync(string cmd)
    {
        return new OkResult<string>("Hello from commandline");
    }
}

#if false
public interface IPfsGetFetchConfig
{
    // Pretty much any change, and should just read all settings again
    event EventHandler EventFetchConfigsChanged;

    record ProvMap(MarketID market, string symbols, ExtProviderID[] providers);

    ProvMap[] GetProvForSending(MarketID market, string symbols);
}

// Simply get/set to identify different markets or market+symbols groups those fetched with defined providers
public interface IPfsSetFetchConfig
{
    record Cfg(MarketID market, string symbols, ExtProviderID[] providers);

    Cfg[] GetAll();

    void SetAll(Cfg[] cfg);
}
#endif