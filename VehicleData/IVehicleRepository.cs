using System.Collections.Generic;

namespace VehicleData
{
    public interface IVehicleRepository
    {
        IList<Vehicle> GetVehicle(string mdl);
    }
}
