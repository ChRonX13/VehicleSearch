using System.Collections.Generic;

namespace VehicleData
{
    public class VehicleResults
    {
        public long TimeTaken { get; set; }

        public IList<Vehicle> Vehicles { get; set; }
    }
}
