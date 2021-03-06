﻿using NadekoBot.Extensions;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Impl
{
    public class RedisCache : IDataCache
    {
        public ConnectionMultiplexer Redis { get; }
        private readonly IDatabase _db;
        private readonly string _redisKey;

        public RedisCache(IBotCredentials creds)
        {
            Redis = ConnectionMultiplexer.Connect("127.0.0.1");
            Redis.PreserveAsyncOrder = false;
            _db = Redis.GetDatabase();
            _redisKey = creds.RedisKey();
        }

        // things here so far don't need the bot id
        // because it's a good thing if different bots 
        // which are hosted on the same PC
        // can re-use the same image/anime data
        public async Task<(bool Success, byte[] Data)> TryGetImageDataAsync(string key)
        {
            byte[] x = await _db.StringGetAsync("image_" + key);
            return (x != null, x);
        }

        public Task SetImageDataAsync(string key, byte[] data)
        {
            return _db.StringSetAsync("image_" + key, data);
        }

        public async Task<(bool Success, string Data)> TryGetAnimeDataAsync(string key)
        {
            string x = await _db.StringGetAsync("anime_" + key);
            return (x != null, x);
        }

        public Task SetAnimeDataAsync(string key, string data)
        {
            return _db.StringSetAsync("anime_" + key, data);
        }

        private readonly object timelyLock = new object();
        public TimeSpan? AddTimelyClaim(ulong id, int period)
        {
            if (period == 0)
                return null;
            lock (timelyLock)
            {
                var time = TimeSpan.FromHours(period);
                if ((bool?)_db.StringGet($"{_redisKey}_timelyclaim_{id}") == null)
                {
                    _db.StringSet($"{_redisKey}_timelyclaim_{id}", true);
                    _db.KeyExpire($"{_redisKey}_timelyclaim_{id}", time);
                    return null;
                }
                return _db.KeyTimeToLive($"{_redisKey}_timelyclaim_{id}");
            }
        }

        public void RemoveAllTimelyClaims()
        {
            var server = Redis.GetServer("127.0.0.1", 6379);
            foreach (var k in server.Keys(pattern: $"{_redisKey}_timelyclaim_*"))
            {
                _db.KeyDelete(k, CommandFlags.FireAndForget);
            }
        }
    }
}
