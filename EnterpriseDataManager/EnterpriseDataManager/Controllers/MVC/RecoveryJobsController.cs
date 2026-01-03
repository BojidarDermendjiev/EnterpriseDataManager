namespace EnterpriseDataManager.Controllers.MVC
{
    using Microsoft.AspNetCore.Mvc;

    public class RecoveryJobsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Breadcrumb"] = "Recovery Jobs";
            return View();
        }

        public IActionResult Create()
        {
            ViewData["Breadcrumb"] = "Recovery Jobs / New Recovery";
            return View();
        }

        public IActionResult Details(Guid id)
        {
            ViewData["Breadcrumb"] = "Recovery Jobs / Details";
            return View();
        }
    }
}
