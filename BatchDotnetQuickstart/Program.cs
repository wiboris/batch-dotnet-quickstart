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
        public async static Task Main(string[] args)
        {
            await new BatchDotNetQuickStart().Run();

            Console.WriteLine("Press return to exit...");
            Console.ReadLine();
        }
    }
}
