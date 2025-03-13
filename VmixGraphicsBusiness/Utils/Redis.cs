using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.Utils
{
    public interface IRedisCache
    {
        Task SetAsync(string key, string value, TimeSpan? expiry = null);
        Task<string> GetAsync(string key);
        Task<bool> DeleteAsync(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<T> GetAsync<T>(string key);
    }

    public class RedisCache(ConnectionMultiplexer clientsManager, IConfiguration config) : IRedisCache
    {
        private readonly ConnectionMultiplexer _clientsManager = clientsManager;

        public Task SetAsync(string key, string value, TimeSpan? expiry = null)
        {
            var obj = new object();
            var asyncRedis = _clientsManager.GetDatabase(asyncState: obj);
            return asyncRedis.StringSetAsync(key, value, expiry);
        }

        public async Task<string> GetAsync(string key)
        {
            var obj = new object();
            var asyncRedis = _clientsManager.GetDatabase(asyncState: obj);
            var value = await asyncRedis.StringGetAsync(key);
            return value.ToString();
        }

        public async Task<bool> DeleteAsync(string key)
        {
            var obj = new object();
            var asyncRedis = _clientsManager.GetDatabase(asyncState: obj);
            var value = await asyncRedis.KeyDeleteAsync(key);
            return value;
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var sValue = JsonConvert.SerializeObject(value);
            return SetAsync(key, sValue, expiry);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var value = await GetAsync(key);
            var sVal = JsonConvert.DeserializeObject<T>(value);
            return sVal;
        }
    }
}
