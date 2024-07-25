
namespace Pfs.Client;

public interface IFEWaiting // drop here things those dont yet fit to final apis
{
    event EventHandler<FeEventArgs> EventPfsClient2PHeader; // Single EV to all possible events those PFS may send to FE (this is for PageHeader)
    event EventHandler<FeEventArgs> EventPfsClient2Page;    // Identical event to page itself

    public class FeEventArgs : EventArgs
    {
        public string Event { get; set; }
        public object Data { get; set; }
    }
}
