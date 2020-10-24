using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpelosNet.Core.Entities;
using SpelosNet.Core.Services;

namespace SpelosNet.Infrastructure.Spotify
{
    public class SpotifyService : ISpotifyService
    {
        private readonly SpotifyApi _api;

        public SpotifyService(SpotifyApi api)
        {
            _api = api;
        }

        public async Task<IEnumerable<SpotifyPlaylist>> GetMyPlaylistsAsync()
        {
            var playlists = await _api.GetMyPlaylists();

            return playlists.Select(p => new SpotifyPlaylist
            {
                ImageUrl = p.Images.First().Url,
                Title = p.Name,
                Url = p.Uri
            });
        }
    }
}
