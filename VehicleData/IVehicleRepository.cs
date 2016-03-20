using System.Collections.Generic;

namespace VehicleData
{
    public interface IVehicleRepository
    {
        VehicleResults GetVehicle(string mdl);
    }
}
