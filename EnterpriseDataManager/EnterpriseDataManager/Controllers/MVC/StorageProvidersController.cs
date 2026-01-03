namespace EnterpriseDataManager.Controllers.MVC
{
    using Microsoft.AspNetCore.Mvc;

    public class StorageProvidersController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Breadcrumb"] = "Storage Providers";
            return View();
        }

        public IActionResult Create()
        {
            ViewData["Breadcrumb"] = "Storage Providers / Add New";
            return View();
        }

        public IActionResult Details(Guid id)
        {
            ViewData["Breadcrumb"] = "Storage Providers / Details";
            return View();
        }
    }
}
