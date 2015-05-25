using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Amazon;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using Amazon.Runtime;
using Newtonsoft.Json;

namespace IceMelt
{
    public class IceMeltClient
    {
        private readonly AmazonGlacierClient _glacier;
        private IEnumerable<IceMeltUploadInternal> SessionUploads { get; set; }        

        public IceMeltClient(string accessKey, string secretKey, string region)
        {
            var creds = new BasicAWSCredentials(accessKey, secretKey);
            _glacier = new AmazonGlacierClient(creds, RegionEndpoint.GetBySystemName(region));
            SessionUploads = new List<IceMeltUploadInternal>();
        }

        public async void UploadArchive(string vaultName,string fileName, string archiveDesc)
        {
            var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            string treeHash = TreeHashGenerator.CalculateTreeHash(fileStream);

            UploadArchiveRequest request = new UploadArchiveRequest
            {
                VaultName = vaultName,
                Body = fileStream,
                Checksum = treeHash,
                ArchiveDescription = archiveDesc
            };

            var cts = new CancellationTokenSource();
            var uploadTask = _glacier.UploadArchiveAsync(request, cts.Token);

            var upload = new IceMeltUploadInternal
            {
                Desc = archiveDesc,
                Vault = vaultName,
                TokenSource = cts,
                Stream = fileStream,
                Task = uploadTask
            };

            ((List<IceMeltUploadInternal>)SessionUploads).Add(upload);

            await uploadTask;

            upload.ArchiveId = uploadTask.Result.ArchiveId;

            if (upload.Stream != null)
            {
                upload.Stream.Dispose();
            }
        }

        public IEnumerable<IceMeltJob> GetJobsList(string vaultName)
        {
            var jobslist = _glacier.ListJobs(new ListJobsRequest(vaultName));
            List<IceMeltJob> output = new List<IceMeltJob>();

            foreach (var job in jobslist.JobList)
            {
                output.Add(new IceMeltJob
                {
                    Id = job.JobId,
                    Desc = job.JobDescription,
                    Status = job.StatusMessage,
                    Type = job.Action == ActionCode.ArchiveRetrieval ? JobType.ArchiveRetrieval : JobType.InventoryRetrieval
                });
            }

            return output;
        }

        public string StartArchiveRequestJob(string vaultName, string archiveId, string jobDesc)
        {
            var resp = _glacier.InitiateJob(new InitiateJobRequest(vaultName,
                new JobParameters(null, "archive-retrieval", archiveId, jobDesc)));
            return resp.JobId;
        }

        public string StartInventoryRetrievalJob(string vaultName, string format, string jobDesc)
        {
            var resp = _glacier.InitiateJob(new InitiateJobRequest(vaultName,
                new JobParameters(format, "inventory-retrieval", null, jobDesc)));
            return resp.JobId;
        }

        public Stream RetrieveJobOutput(string vaultName, string jobId)
        {
            var o = _glacier.GetJobOutput(new GetJobOutputRequest(vaultName, jobId, null));
            return o.Body;
        }

        public Inventory RetrieveInventoryOutput(string vaultName, string jobId)
        {
            Inventory i;
            using (StreamReader sr = new StreamReader(RetrieveJobOutput(vaultName, jobId)))
            {
                i = JsonConvert.DeserializeObject<Inventory>(sr.ReadToEnd());
            }

            return i;
        }

        public IEnumerable<IceMeltUpload> GetSessionUploadsList()
        {
            return SessionUploads.Select(upload => new IceMeltUpload
            {
                Desc = upload.Desc, Status = upload.Task.Status.ToString(), Vault = upload.Vault, ArchiveId = upload.ArchiveId
            });
        }
    }
}