using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Glacier.Model;

namespace IceMelt
{
    internal class IceMeltUploadInternal
    {
        public Task<UploadArchiveResponse> Task { get; set; }
        public string Vault { get; set; }
        public string Desc { get; set; }
        public string ArchiveId { get; set; }
        public CancellationTokenSource TokenSource { get; set; }
        public Stream Stream { get; set; }
    }
}