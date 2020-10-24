using System.Collections.Generic;
using System.Threading.Tasks;
using SpelosNet.Core.Entities;

namespace SpelosNet.Core.Services
{
    public interface ISpotifyService
    {
        Task<IEnumerable<SpotifyPlaylist>> GetMyPlaylistsAsync();
    }
}
