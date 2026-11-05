using System;
using System.Threading;
using System.Threading.Tasks;

namespace Certification {
    public interface ITest {
        string Name { get; }
        string Instructions { get; }

        Task<bool> RunAsync(IProgress<string> progress, CancellationToken token);
    }
}
