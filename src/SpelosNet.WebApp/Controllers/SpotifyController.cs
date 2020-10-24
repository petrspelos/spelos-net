using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SpelosNet.Core.Services;
using SpelosNet.WebApp.Models;

namespace SpelosNet.WebApp.Controllers
{
    public class SpotifyController : Controller
    {
        private readonly ISpotifyService _spotify;

        public SpotifyController(ISpotifyService spotify)
        {
            _spotify = spotify;
        }

        public async Task<IActionResult> Index()
        {
            var playlists = await _spotify.GetMyPlaylistsAsync();

            return View(new SpotifyModel
            {
                Playlists = playlists
            });
        }
    }
}
