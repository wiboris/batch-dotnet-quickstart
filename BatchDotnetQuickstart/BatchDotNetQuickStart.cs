using Azure.Compute.Batch;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Batch;
using Azure.ResourceManager.Batch.Models;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Compute.Batch.Quickstart
{
    public class BatchDotNetQuickStart
    {
        // The following constants are used to configure the Batch account and storage account, replace with valid storage and batch account values.
        private const string StorageAccountUri = "https://dotnotsdkbatchstorage1.blob.core.windows.net/";
        private const string BatchAccountResourceId = "/subscriptions/25d5d4d6-d9c8-4053-9e3f-c47c2960a69b/resourceGroups/automation/providers/Microsoft.Batch/batchAccounts/dotnotsdkbatchaccount2";
        private const string PoolId = "DotNetQuickstartPool";
        private const string JobId = "DotNetQuickstartJob";

        // The following fields are references to the service clients
        private BatchAccountResource _batchAccountResource;
        private BatchClient _batchClient;
        private BlobServiceClient _blobServiceClient;

        /// <summary>
        /// Creates a pool with a configurable number of nodes, then submits tasks which print the content of uploaded
        /// text files resulting stdout.txt or stderr.txt (depending on each task's exit code) is then printed to the console.
        /// 
        /// After running, the job will be terminated and the pool will be deleted.
        /// </summary>
        /// <returns>A task which completes when the sample has finished running.</returns>
        public async Task Run()
        {
            Console.WriteLine("Sample start: {0}", DateTime.Now);
            Console.WriteLine();
            var timer = new Stopwatch();
            timer.Start();

            // #1 Create the clients need for the operations

            // Get the default Azure credential, which will be used to authenticate the clients
            var credential = new DefaultAzureCredential();

            // Create the blob client, for use in obtaining references to blob storage containers
            _blobServiceClient = new BlobServiceClient(new Uri(StorageAccountUri), credential);

            // Create a reference to the Batch Account resource in Arm which will allows us to interact with the Azure.ResourceManager.Batch sdk.
            ArmClient armClient = new ArmClient(credential);
            var batchAccountIdentifier = ResourceIdentifier.Parse(BatchAccountResourceId);
            _batchAccountResource = await armClient.GetBatchAccountResource(batchAccountIdentifier).GetAsync();

            // Create the Batch client, which will be used to interact with the Azure.Compute.Batch sdk.
            _batchClient = new BatchClient(new Uri($"https://{_batchAccountResource.Data.AccountEndpoint}"), credential);

            // #2 Upload the test files to the storage blob

            // Use the blob client to create the input container in Azure Storage to store the files needed for the task operations.
            const string inputContainerName = "input";
            var containerClient = _blobServiceClient.GetBlobContainerClient(inputContainerName);
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

            // #3 Create a Batch pool
            await CreateBatchPool();

            // #4 Create a Batch job
            Console.WriteLine("Creating job [{0}]...", JobId);

            try
            {
                BatchPoolInfo batchPoolInfo = new BatchPoolInfo()
                {
                    PoolId = PoolId
                };
                BatchJobCreateContent batchJobCreateContent = new BatchJobCreateContent(JobId, batchPoolInfo);

                await _batchClient.CreateJobAsync(batchJobCreateContent);
            }
            catch (RequestFailedException e)
            {
                BatchError be = BatchError.FromException(e);
                // Accept the specific error code JobExists as that is expected if the job already exists
                if (be.Code == BatchErrorCodeStrings.JobExists)
                {
                    Console.WriteLine("The job {0} already existed when we tried to create it", JobId);
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }

            // #5 Create the tasks to be executed

            // Create a collection to hold the tasks that we'll be adding to the job
            Console.WriteLine("Adding {0} tasks to job [{1}]...", inputFiles.Count, JobId);
            var tasks = new List<BatchTaskCreateContent>();

            // Create each of the tasks to process one of the input files. 
            for (int i = 0; i < inputFiles.Count; i++)
            {
                string taskId = string.Format("Task{0}", i);
                string inputFilename = inputFiles[i].FilePath;
                string taskCommandLine = string.Format("cmd /c type {0}", inputFilename);

                BatchTaskCreateContent task = new BatchTaskCreateContent(taskId, taskCommandLine)
                {
                    ResourceFiles = {inputFiles[i]}
                };
                tasks.Add(task);
            }

            CreateTasksResult createTasksResult= await _batchClient.CreateTasksAsync(JobId, tasks);

            // Monitor task success/failure, specifying a maximum amount of time to wait for the tasks to complete.
            TimeSpan timeout = TimeSpan.FromMinutes(30);
            Console.WriteLine("Monitoring all tasks for 'Completed' state, timeout in {0}...", timeout);

            await waitForTasksToComplete(JobId);
            Console.WriteLine("All tasks reached state Completed.");

            // #4 Print task output

            Console.WriteLine();
            Console.WriteLine("Printing task output...");

            await foreach (BatchTask task in _batchClient.GetTasksAsync(JobId))
            {
                string nodeId = string.Format(task.NodeInfo.NodeId);
                BinaryData outputBinaryData = await _batchClient.GetTaskFileAsync(JobId, task.Id, "stdout.txt");
                string stdout = outputBinaryData.ToString();

                Console.WriteLine("Task: {0}", task.Id);
                Console.WriteLine("Node: {0}", nodeId);
                Console.WriteLine("Standard out:");
                Console.WriteLine(stdout);
            }

            // Print out some timing info
            timer.Stop();
            Console.WriteLine();
            Console.WriteLine("Sample end: {0}", DateTime.Now);
            Console.WriteLine("Elapsed time: {0}", timer.Elapsed);

            // #5 Clean up resources

            // Clean up Storage resources
            containerClient.DeleteIfExistsAsync().Wait();
            Console.WriteLine("Container [{0}] deleted.", inputContainerName);

            // Clean up Batch resources (if the user so chooses)
            Console.WriteLine();
            Console.Write("Delete job? [yes] no: ");
            string response = Console.ReadLine().ToLower();
            if (response != "n" && response != "no")
            {
                _batchClient.DeleteJob(JobId);
            }

            Console.Write("Delete pool? [yes] no: ");
            response = Console.ReadLine().ToLower();
            if (response != "n" && response != "no")
            {
               _batchClient.DeletePool(PoolId);
            }
        }

        /// <summary>
        /// Poll all the tasks in the given job and wait for them to reach the completed state.
        /// </summary>
        /// <param name="jobId">The ID of the job to poll</param>
        /// <returns>A task that will complete when all Batch tasks have completed.</returns>
        /// <exception cref="TimeoutException">Thrown if all tasks haven't reached the completed state after a certain period of time</exception>
        private async Task waitForTasksToComplete(String jobId)
        {
            // Note that this timeout should take into account the time it takes for the pool to scale up
            var timeoutAfter = DateTime.Now.AddMinutes(10);
            while (DateTime.Now < timeoutAfter)
            {
                var allComplete = true;
                var tasks = _batchClient.GetTasksAsync(jobId, select: ["id", "state"]);
                await foreach (BatchTask task in tasks)
                {
                    if (task.State != BatchTaskState.Completed)
                    {
                        allComplete = false;
                        break;
                    }
                }

                if (allComplete)
                {
                    return;
                }

                await Task.Delay(10000);
            }

            throw new TimeoutException("Task(s) did not complete within the specified time");
        }

        /// <summary>
        /// Code to create a Batch pool using the mgmt client. Both the Azure.Compute.Batch and 
        /// Azure.ResourceManager.Batch sdk's can create a pool but only the Azure.ResourceManager.Batch
        /// can create a pool with managed identities so its considered a best practice to use
        /// Azure.ResourceManager.Batch to create pools.
        /// </summary>
        ///
        public async Task CreateBatchPool()
        {
            try
            {
                // Create a Batch pool with a single node
                var imageReference = new BatchImageReference()
                {
                    Publisher = "MicrosoftWindowsServer",
                    Offer = "WindowsServer",
                    Sku = "2019-datacenter-smalldisk",
                    Version = "latest"
                };
                string nodeAgentSku = "batch.node.windows amd64";

                ArmOperation<BatchAccountPoolResource> armOperation = await _batchAccountResource.GetBatchAccountPools().CreateOrUpdateAsync(
                    WaitUntil.Completed, PoolId, new BatchAccountPoolData()
                    {
                        VmSize = "Standard_DS1_v2",
                        DeploymentConfiguration = new BatchDeploymentConfiguration()
                        {
                            VmConfiguration = new BatchVmConfiguration(imageReference, nodeAgentSku)
                        },
                        ScaleSettings = new BatchAccountPoolScaleSettings()
                        {
                            FixedScale = new BatchAccountFixedScaleSettings()
                            {
                                TargetDedicatedNodes = 1
                            }
                        }
                    });
                BatchAccountPoolResource pool = armOperation.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }


        /// <summary>
        /// gets a key that can be used to sign a user delegation SAS (shared access signature). 
        /// A user delegation SAS grants access to Azure Blob Storage resources by using Microsoft Entra credentials.
        /// </summary>
        /// <returns></returns>
        public async Task<UserDelegationKey> RequestUserDelegationKey()
        {
            // Get a user delegation key for the Blob service that's valid for 1 day
            UserDelegationKey userDelegationKey =
                await _blobServiceClient.GetUserDelegationKeyAsync(
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddDays(1));

            return userDelegationKey;
        }

        /// <summary>
        /// Uploads the specified file to the specified Blob container.
        /// </summary>
        /// <param name="containerClient">A <see cref="BlobContainerClient"/>.</param>
        /// <param name="containerName">The name of the blob storage container to which the file should be uploaded.</param>
        /// <param name="filePath">The full path to the file to upload to Storage.</param>
        /// <returns>A ResourceFile instance representing the file within blob storage.</returns>
        private ResourceFile UploadFileToContainer(BlobContainerClient containerClient, string containerName, string filePath, string storedPolicyName = null)
        {
            Console.WriteLine("Uploading file {0} to container [{1}]...", filePath, containerName);

            string blobName = Path.GetFileName(filePath);
            filePath = Path.Combine(Environment.CurrentDirectory, filePath);

            var blobClient = containerClient.GetBlobClient(blobName);
            blobClient.Upload(filePath, true);

            // get access UserDelegationKey to grant access to Azure Blob Storage
            UserDelegationKey userDelegationKey = RequestUserDelegationKey().Result;
            Uri sasUri = blobClient.GenerateUserDelegationSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1), userDelegationKey);

            return new ResourceFile
            {
                HttpUrl = sasUri.ToString(),
                FilePath = filePath
            };
        }
    }
}
