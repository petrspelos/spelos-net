using System;
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

        private InMemoryCache<Task<IEnumerable<SpotifyPlaylist>>> _myPlaylists;

        public SpotifyService(SpotifyApi api)
        {
            _api = api;
            _myPlaylists = new InMemoryCache<Task<IEnumerable<SpotifyPlaylist>>>(GetMyPlaylistsFromApiAsync, TimeSpan.FromHours(1));
        }

        public async Task<IEnumerable<SpotifyPlaylist>> GetMyPlaylistsAsync() => await _myPlaylists.GetValue();

        private async Task<IEnumerable<SpotifyPlaylist>> GetMyPlaylistsFromApiAsync()
        {
            await _api.InitializeAsync();

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
