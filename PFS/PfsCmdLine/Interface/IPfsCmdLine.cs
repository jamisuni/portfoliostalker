using Pfs.Types;

namespace Pfs.CmdLine;

public interface IPfsCmdLine
{
    Task<Result<string>> CmdAsync(string cmd);
}
