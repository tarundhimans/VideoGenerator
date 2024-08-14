using Microsoft.AspNetCore.Mvc;
using RestSharp;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TextToVideoApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Search", "Videos");
        }


    }
}
