using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Compute.Batch;
using Azure.ResourceManager.Batch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Azure.ResourceManager;
using System.Threading.Tasks;

namespace Azure.Compute.Batch.Quickstart
{
    public class Program
    {

        private const string StorageAccountUri = "https://MYSTORAGEACCOUNT.blob.core.windows.net/";

        // Batch resource settings
        private const string PoolId = "DotNetQuickstartPool";
        private const string JobId = "DotNetQuickstartJob";
        private const int PoolNodeCount = 2;
        private const string PoolVMSize = "STANDARD_D1_V2";

        public async static Task Main(string[] args)
        {
            await new BatchDotNetQuickStart().Run("batchAccountResourceId");

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();

            /*
            try
            {
                Console.WriteLine("Sample start: {0}", DateTime.Now);
                Console.WriteLine();
                var timer = new Stopwatch();
                timer.Start();

                // Get the default Azure credential, which will be used to authenticate the clients
                var credential = new DefaultAzureCredential();

                // Create the blob client, for use in obtaining references to blob storage containers
                Uri accountUri = new Uri(StorageAccountUri);
                _blobServiceClient = new BlobServiceClient(accountUri, new DefaultAzureCredential());

                // Use the blob client to create the input container in Azure Storage 
                const string inputContainerName = "input";
                var containerClient = blobServiceClient.GetBlobContainerClient(inputContainerName);
                containerClient.CreateIfNotExistsAsync().Wait();

                // The collection of data files that are to be processed by the tasks
                List<string> inputFilePaths = new()
                {
                    "taskdata0.txt",
                    "taskdata1.txt",
                    "taskdata2.txt"
                };

                // Upload the data files to Azure Storage. This is the data that will be processed by each of the tasks that are
                // executed on the compute nodes within the pool.
                var inputFiles = new List<ResourceFile>();

                foreach (var filePath in inputFilePaths)
                {
                    inputFiles.Add(UploadFileToContainer(containerClient, inputContainerName, filePath));
                }

                // Get a Batch client using account creds
                var cred = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);

                using BatchClient batchClient = BatchClient.Open(cred);
                Console.WriteLine("Creating pool [{0}]...", PoolId);

                // Create a Windows Server image, VM configuration, Batch pool
                ImageReference imageReference = CreateImageReference();
                VirtualMachineConfiguration vmConfiguration = CreateVirtualMachineConfiguration(imageReference);
                CreateBatchPool(batchClient, vmConfiguration);

                // Create a Batch job
                Console.WriteLine("Creating job [{0}]...", JobId);

                try
                {
                    CloudJob job = batchClient.JobOperations.CreateJob();
                    job.Id = JobId;
                    job.PoolInformation = new PoolInformation { PoolId = PoolId };
                    job.Commit();
                }
                catch (BatchException be)
                {
                    // Accept the specific error code JobExists as that is expected if the job already exists
                    if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.JobExists)
                    {
                        Console.WriteLine("The job {0} already existed when we tried to create it", JobId);
                    }
                    else
                    {
                        throw; // Any other exception is unexpected
                    }
                }

                // Create a collection to hold the tasks that we'll be adding to the job
                Console.WriteLine("Adding {0} tasks to job [{1}]...", inputFiles.Count, JobId);
                var tasks = new List<CloudTask>();

                // Create each of the tasks to process one of the input files. 
                for (int i = 0; i < inputFiles.Count; i++)
                {
                    string taskId = string.Format("Task{0}", i);
                    string inputFilename = inputFiles[i].FilePath;
                    string taskCommandLine = string.Format("cmd /c type {0}", inputFilename);

                    var task = new CloudTask(taskId, taskCommandLine)
                    {
                        ResourceFiles = new List<ResourceFile> { inputFiles[i] }
                    };
                    tasks.Add(task);
                }

                // Add all tasks to the job.
                batchClient.JobOperations.AddTask(JobId, tasks);

                // Monitor task success/failure, specifying a maximum amount of time to wait for the tasks to complete.
                TimeSpan timeout = TimeSpan.FromMinutes(30);
                Console.WriteLine("Monitoring all tasks for 'Completed' state, timeout in {0}...", timeout);

                IEnumerable<CloudTask> addedTasks = batchClient.JobOperations.ListTasks(JobId);
                batchClient.Utilities.CreateTaskStateMonitor().WaitAll(addedTasks, TaskState.Completed, timeout);
                Console.WriteLine("All tasks reached state Completed.");

                // Print task output
                Console.WriteLine();
                Console.WriteLine("Printing task output...");

                IEnumerable<CloudTask> completedtasks = batchClient.JobOperations.ListTasks(JobId);
                foreach (CloudTask task in completedtasks)
                {
                    string nodeId = string.Format(task.ComputeNodeInformation.ComputeNodeId);
                    Console.WriteLine("Task: {0}", task.Id);
                    Console.WriteLine("Node: {0}", nodeId);
                    Console.WriteLine("Standard out:");
                    Console.WriteLine(task.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
                }

                // Print out some timing info
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine("Sample end: {0}", DateTime.Now);
                Console.WriteLine("Elapsed time: {0}", timer.Elapsed);

                // Clean up Storage resources
                containerClient.DeleteIfExistsAsync().Wait();
                Console.WriteLine("Container [{0}] deleted.", inputContainerName);

                // Clean up Batch resources (if the user so chooses)
                Console.WriteLine();
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(JobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(PoolId);
                }
            }
            catch(Exception e) 
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }*/
        }
    }
}
