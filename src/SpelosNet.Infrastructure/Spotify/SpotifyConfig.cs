namespace SpelosNet.Infrastructure.Spotify
{
    public class SpotifyConfig
    {
        internal string ClientId { get; private set; }
        internal string ClientSecret { get; private set; }
        internal string MyUserId { get; private set; }

        public SpotifyConfig(string clientId, string clientSecret, string myUserId)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            MyUserId = myUserId;
        }
    }
}
