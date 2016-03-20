using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace VehicleData
{
    public class Vehicle : TableEntity
    {
        public DateTime IncidentDateTime { get; set; }

        public string Mdl { get; set; }

        public string Latitude { get; set; }

        public string Longitude { get; set; }
    }
}
