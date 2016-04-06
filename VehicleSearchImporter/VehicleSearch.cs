using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace VehicleSearchImporter
{
    public class VehicleSearch : TableEntity
    {
        public string Latitude { get; set; }

        public string Longitude { get; set; }

        public DateTime IncidentDateTime { get; set; }
    }
}
