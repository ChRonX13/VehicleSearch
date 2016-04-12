using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace VehicleSearchImporter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var cloudStorageAccount = InitializeCloudStorage();

            var blobContainer = GetCloudBlobContainer(cloudStorageAccount, "trafficdata");

            var vehicleData = ReadDataFromFile(@"G:\MSWorkspace\incident.csv");
            //var vehicleData = ReadDataFromCloudBlob(blobContainer, "incident.csv");

            var batchOperations = CreateBatchOperations(vehicleData);
            var tableOperations = CreateOperations(vehicleData);

            var table = GetCloudTable(cloudStorageAccount, "VehicleSearch");

            UploadToTheCloudzBatch(table, batchOperations).Wait();
            TuneThisUp(table, tableOperations);

            Console.ReadLine();
        }

        public static IList<VehicleData> ReadDataFromFile(string fileLocation)
        {
            using (var sr = new StreamReader(fileLocation))
            {
                Console.Out.WriteLine("Parsing file: {0}", fileLocation);
                Stopwatch watch = Stopwatch.StartNew();
                var csv = new CsvReader(sr);
                var records =
                    csv.GetRecords<VehicleData>().Take(100)
                        .Where(v => v.Mdl.All(char.IsLetterOrDigit))
                        .OrderBy(v => v.Mdl)
                        .ThenBy(v => v.IncidentDateTime)
                        .ToList();

                Console.Out.WriteLine("Parsed {0} records, {1} per second, in {2} seconds", records.Count,
                    $"{records.Count/watch.Elapsed.TotalSeconds:0.0}", $"{watch.Elapsed.TotalSeconds:0.0}");

                return records;
            }
        }

        public static IList<VehicleData> ReadDataFromCloudBlob(CloudBlobContainer container, string blobname)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-au", false); // hack to fix datetime without being a performance hog!

            CloudBlockBlob blob = container.GetBlockBlobReference(blobname);

            using (var stream = blob.OpenRead())
            {
                using (var csv = new CsvReader(new StreamReader(stream)))
                {
                    Console.Out.WriteLine("Parsing file: {0}", blobname);
                    Console.Out.WriteLine("Current Culture:" + Thread.CurrentThread.CurrentCulture);
                    Stopwatch watch = Stopwatch.StartNew();

                    var records =
                        csv.GetRecords<VehicleData>()
                            .Where(v => v.Mdl.All(char.IsLetterOrDigit))
                            .OrderBy(v => v.Mdl)
                            .ThenBy(v => v.IncidentDateTime)
                            .ToList();

                    Console.Out.WriteLine("Parsed {0} records, {1} per second, in {2} seconds", records.Count,
                        $"{records.Count / watch.Elapsed.TotalSeconds:0.0}", $"{watch.Elapsed.TotalSeconds:0.0}");

                    return records;
                }
            }
        }

        private static CloudBlobContainer GetCloudBlobContainer(CloudStorageAccount storageAccount, string container)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();

            return blobClient.GetContainerReference(container);
        }

        private static CloudStorageAccount InitializeCloudStorage()
        {
            return CreateStorageAccountFromConnectionString(CloudConfigurationManager.GetSetting("StorageConnectionString"));
        }

        private static CloudTable GetCloudTable(CloudStorageAccount storageAccount, string tableName)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(tableName);
            table.CreateIfNotExists();

            return table;
        }

        private static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the application.");
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }

        private static IList<TableBatchOperation> CreateBatchOperations(IList<VehicleData> vehicleData)
        {
            Stopwatch watch = Stopwatch.StartNew();

            Console.Out.WriteLine("Getting batch vehicle keys...");

            var batchVehicles = GetBatchVehicles(vehicleData);

            var batchOperations = batchVehicles.Select(GenerateBatchOperation).ToList();

            Console.Out.WriteLine("Generated all the batch operations {0} in {1} seconds at {2} per second", batchVehicles.Count,
                $"{watch.Elapsed.TotalSeconds:0.0}", $"{batchVehicles.Count/watch.Elapsed.TotalSeconds:0.0}");

            return batchOperations;
        }

        private static IList<TableOperation> CreateOperations(IList<VehicleData> vehicleData)
        {
            Stopwatch watch = Stopwatch.StartNew();

            Console.Out.WriteLine("Getting single vehicle keys...");

            var uniqueVehicles = GetSingleVehicles(vehicleData);

            var tableOperations = uniqueVehicles.Select(GenerateTableOperation).ToList();

            Console.Out.WriteLine("Generated all the single operations {0} in {1} seconds at {2} per second",
                uniqueVehicles.Count,
                $"{watch.Elapsed.TotalSeconds:0.0}", $"{uniqueVehicles.Count/watch.Elapsed.TotalSeconds:0.0}");
            

            return tableOperations;
        }

        private static IList<List<VehicleData>> GetBatchVehicles(IList<VehicleData> vehicleData)
        {
            return
                vehicleData.GroupBy(v => v.Mdl.Trim())
                    .Where(v => v.Count() > 1 && !string.IsNullOrWhiteSpace(v.Key) && v.Key.Any(k => k != '0'))
                    .Select(group => group.ToList())
                    .ToList();
        }

        private static IList<VehicleData> GetSingleVehicles(IList<VehicleData> vehicleData)
        {
            return
                vehicleData.GroupBy(v => v.Mdl.Trim())
                    .Where(v => v.Count() == 1 && !string.IsNullOrWhiteSpace(v.Key) && v.Key.Any(k => k != '0'))
                    .Select(group => group.First())
                    .ToList();
        }

        private static TableBatchOperation GenerateBatchOperation(IList<VehicleData> vehicleData)
        {
            var batch = new TableBatchOperation();

            foreach (var data in vehicleData)
            {
                batch.InsertOrReplace(new VehicleSearch
                {
                    PartitionKey = data.Mdl,
                    RowKey = Guid.NewGuid().ToString(),
                    Latitude = data.Latitude.StartsWith("-") ? data.Latitude : "-" + data.Latitude,
                    Longitude = data.Longitude,
                    IncidentDateTime = data.IncidentDateTime
                });
            }

            return batch;
        }

        private static TableOperation GenerateTableOperation(VehicleData vehicleData)
        {
            return TableOperation.InsertOrReplace(new VehicleSearch
            {
                PartitionKey = vehicleData.Mdl,
                RowKey = Guid.NewGuid().ToString(),
                Latitude = vehicleData.Latitude.StartsWith("-") ? vehicleData.Latitude : "-" + vehicleData.Latitude,
                Longitude = vehicleData.Longitude,
                IncidentDateTime = vehicleData.IncidentDateTime
            });
        }

        private static async Task UploadToTheCloudzBatch(CloudTable table, IList<TableBatchOperation> batchOperations)
        {
            Console.Out.WriteLine("To the batch cl0udz!!");

            Stopwatch watch = Stopwatch.StartNew();

            foreach (var batchOperation in batchOperations)
            {
                try
                {
                    await table.ExecuteBatchAsync(batchOperation);
                }
                catch (StorageException se)
                {
                    Console.Out.WriteLine("Unexpected: {0} - {1}", se.Message, se.RequestInformation.ExtendedErrorInformation.ErrorMessage);
                }

            }

            Console.Out.WriteLine("Uploaded batch data {0} to storage in {1} seconds at {2} per second", batchOperations.Count, $"{watch.Elapsed.TotalSeconds:0.0}", $"{batchOperations.Count / watch.Elapsed.TotalSeconds:0.0}");
        }

        private static void TuneThisUp(CloudTable table, IList<TableOperation> operations)
        {
            int batchSize = 50000;
            var batched = operations.Select((x, i) => new { Val = x, Idx = i })
                                        .GroupBy(x => x.Idx / batchSize,
                                                 (k, g) => g.Select(x => x.Val));

            int batchCount = 0;
            var uploadTasks = new List<Task>();

            ThreadPool.SetMaxThreads(500, 5000);

            foreach (var batch in batched)
            {
                batchCount++;
                var internalNumber = batchCount;

                Console.WriteLine("Processing batch({0})...", internalNumber);


                uploadTasks.Add(Task.Run(async () =>
                {
                    await (UploadToTheCloudz(table, batch.ToList(), internalNumber));
                    Console.Out.WriteLine("Finished batch({0})", internalNumber);
                }));
            }

            Task.WaitAll(uploadTasks.ToArray());
        }

        private static async Task UploadToTheCloudz(CloudTable table, IList<TableOperation> operations, int batchNumber)
        {
            Console.Out.WriteLine("({0}) To the cl0udz!!", batchNumber);

            Stopwatch watch = Stopwatch.StartNew();

            foreach (var operation in operations)
            {
                await table.ExecuteAsync(operation);
            }

            Console.Out.WriteLine("({0}) Uploaded data {1} to storage in {2} seconds at {3} per second", batchNumber,
                operations.Count, $"{watch.Elapsed.TotalSeconds:0.0}",
                $"{operations.Count/watch.Elapsed.TotalSeconds:0.0}");
        }
    }
}