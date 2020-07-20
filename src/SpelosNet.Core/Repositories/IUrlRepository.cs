using System;
using System.Threading.Tasks;

namespace SpelosNet.Core.Repositories
{
    public interface IUrlRepository
    {
        Task<bool> KeyExistsAsync(string key);
        Task<Uri> GetByKeyAsync(string key);
    }
}
