using System.Web.Mvc;
using VehicleData;

namespace VehicleSearchWeb.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Search(string searchString)
        {
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                IVehicleRepository vehicleRepository = new VehicleRepository();

                var vehicles = vehicleRepository.GetVehicle(searchString.Trim());

                return View("Index", vehicles);
            }

            return null;
        }
    }
}