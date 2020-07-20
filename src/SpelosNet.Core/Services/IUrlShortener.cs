using System;
using System.Threading.Tasks;

namespace SpelosNet.Core.Services
{
    public interface IUrlShortener
    {
        Task<Uri> GetAsync(string urlKey);
    }
}
