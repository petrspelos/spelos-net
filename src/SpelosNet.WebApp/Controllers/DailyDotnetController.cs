using Microsoft.AspNetCore.Mvc;
using SpelosNet.Core.Services;

namespace SpelosNet.WebApp.Controllers
{
    public class DailyDotnetController : Controller
    {
        private readonly IDailyDotnetService _service;

        public DailyDotnetController(IDailyDotnetService service)
        {
            _service = service;
        }

        public IActionResult Today()
        {
            return Redirect(_service.GetTodaysUrl());
        }

        public IActionResult Random()
        {
            return Redirect(_service.GetRandomUrl());
        }
    }
}
