using System.Text.Json;
using codingTest.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace codingTest.Services;

public class HackerNewService: IHackerNewService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HackerNewService> _logger;
        private const string BaseUrl = "https://hacker-news.firebaseio.com/v0";
        private const string BestStoriesKey = "BestStoriesIds";
        private const int CacheExpirationMinutes = 5;

        public HackerNewService(
            HttpClient httpClient, 
            IMemoryCache cache,
            ILogger<HackerNewService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<HackerNewStory>> GetBestStoriesAsync(int count)
        {
            try
            {
                var storyIds = await GetBestStoryIdsAsync();
                var topStoryIds = storyIds.Take(count).ToList();
                
                var storyTasks = topStoryIds.Select(id => GetStoryDetailsAsync(id));
                var stories = await Task.WhenAll(storyTasks);
                
                var validStories = stories.Where(s => s != null).ToList();
                
                return validStories.OrderByDescending(s => s.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching best stories");
                throw;
            }
        }

        private async Task<List<int>> GetBestStoryIdsAsync()
        {
            List<int> cachedIds;
            if (_cache.TryGetValue(BestStoriesKey, out cachedIds))
            {
                _logger.LogInformation("Story IDs retrived from cache");
                return cachedIds;
            }
            
            _logger.LogInformation("Story IDs retrieved from API");
            var response = await _httpClient.GetAsync($"{BaseUrl}/beststories.json");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var storyIds = JsonSerializer.Deserialize<List<int>>(content);
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheExpirationMinutes));
            _cache.Set(BestStoriesKey, storyIds, cacheOptions);

            return storyIds;
        }

        private async Task<HackerNewStory> GetStoryDetailsAsync(int storyId)
        {
            var cacheKey = $"Story_{storyId}";
            
            if (_cache.TryGetValue(cacheKey, out HackerNewStory cachedStory))
            {
                _logger.LogInformation($"ID {storyId} | Detalis retrieved from cache");
                return cachedStory;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/item/{storyId}.json");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var item = JsonSerializer.Deserialize<HackerNewItem>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (item == null)
                {
                    _logger.LogWarning($"ID {storyId} | Detalis  not found");
                    return null;
                }
                
                var story = new HackerNewStory
                {
                    Title = item.Title ?? string.Empty,
                    Uri = item.Url ?? string.Empty,
                    PostedBy = item.By ?? string.Empty,
                    Time = DateTimeOffset.FromUnixTimeSeconds(item.Time).DateTime,
                    Score = item.Score,
                    CommentCount = item.Descendants
                };
                
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheExpirationMinutes));

                _cache.Set(cacheKey, story, cacheOptions);

                return story;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID {storyId} | Error fetching story details ");
                return null;
            }
        }
    }