using System.Collections.Generic;
using SpelosNet.Core.Entities;

namespace SpelosNet.WebApp.Models
{
    public class SpotifyModel
    {
        public IEnumerable<SpotifyPlaylist> Playlists { get; internal set; }
    }
}
