using System.Collections.Generic;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Models;

namespace SpelosNet.Infrastructure.Spotify
{
    public class SpotifyApi
    {
        private readonly SpotifyConfig _config;
        private SpotifyWebAPI _spotify;

        public SpotifyApi(SpotifyConfig config)
        {
            _config = config;
        }

        public async Task InitializeAsync()
        {
            CredentialsAuth auth = new CredentialsAuth(_config.ClientId, _config.ClientSecret);
            Token token = await auth.GetToken();
            _spotify = new SpotifyWebAPI()
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };
        }

        public async Task<IEnumerable<SimplePlaylist>> GetMyPlaylists()
        {
            if(_spotify is null)
                await InitializeAsync();

            var response = await _spotify.GetUserPlaylistsAsync(_config.MyUserId);
            return response.Items;
        }
    }
}
