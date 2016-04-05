using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace VehicleData
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly CloudTable _table;

        public VehicleRepository()
        {
            var cloudStorageAccount = InitializeCloudStorage();
            _table = GetCloudTable(cloudStorageAccount, "VehicleSearch");
        }

        public VehicleResults GetVehicle(string mdl)
        {
            if (string.IsNullOrWhiteSpace(mdl))
            {
                return null;
            }


            TableQuery<Vehicle> query =
                new TableQuery<Vehicle>().Where(TableQuery.GenerateFilterCondition("PartitionKey",
                    QueryComparisons.Equal, mdl));

            Stopwatch watch = Stopwatch.StartNew();

            var vehicles = _table.ExecuteQuery(query).ToList();

            return new VehicleResults
            {
                TimeTaken = watch.ElapsedMilliseconds,
                Vehicles = vehicles
            };
        }

        private CloudTable GetCloudTable(CloudStorageAccount storageAccount, string tableName)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(tableName);

            if (!table.Exists())
            {
                throw new ArgumentException("{0} does not exist", tableName);
            }

            return table;
        }

        private CloudStorageAccount InitializeCloudStorage()
        {
            return CreateStorageAccountFromConnectionString(CloudConfigurationManager.GetSetting("StorageConnectionString"));
        }

        private CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
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
                throw;
            }

            return storageAccount;
        }


    }
}
