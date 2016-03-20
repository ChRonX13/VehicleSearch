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
            IVehicleRepository vehicleRepository = new VehicleRepository();

            var vehicles = vehicleRepository.GetVehicle(searchString);

            return View("Index", vehicles);
        }
    }
}