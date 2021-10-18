using Microsoft.AspNetCore.Mvc;
using SpelosNet.Core.Services;
using SpelosNet.WebApp.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SpelosNet.WebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUrlShortener _urlShortener;

        public HomeController(IUrlShortener urlShortener)
        {
            _urlShortener = urlShortener;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("/{redirectKey}")]
        public async Task<IActionResult> Index(string redirectKey)
        {
            var redirectUri = await _urlShortener.GetAsync(redirectKey);

            if(redirectUri is null)
                return View();
            else
                return Redirect(redirectUri.AbsoluteUri);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
