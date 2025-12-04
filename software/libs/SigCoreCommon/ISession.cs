using System.Threading.Tasks;

namespace SigCoreCommon {
    public interface ISession {
        uint SessionID { get; set; }
        bool IsCommander { get; set; }
        bool IsConnected { get; }
        Task Send(SigCoreMessage msg);
    }
}
