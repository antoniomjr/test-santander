using System.Net;
using System.Text.Json;
using codingTest.Models;
using codingTest.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace HackerNewsApi.Tests;

public class HackerNewServiceTests
{
    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStoriesOrderedByScore()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1, 2 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = request.RequestUri!.AbsolutePath.Contains("/1")
                ? JsonSerializer.Serialize(new HackerNewItem { Title = "Story 1", Url = "http://1", By = "a", Time = 1, Score = 10, Descendants = 1 })
                : JsonSerializer.Serialize(new HackerNewItem { Title = "Story 2", Url = "http://2", By = "b", Time = 2, Score = 30, Descendants = 2 });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(2)).ToList();

        stories.Should().HaveCount(2);
        stories.Should().BeInDescendingOrder(s => s.Score);
        stories.Select(s => s.Title).Should().ContainInOrder("Story 2", "Story 1");
    }

    [Fact]
    public async Task GetBestStoriesAsync_SkipsStoriesThatReturnErrors()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1, 2 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            if (request.RequestUri!.AbsolutePath.Contains("/1"))
            {
                var itemPayload = JsonSerializer.Serialize(new HackerNewItem { Title = "Story 1", Url = "http://1", By = "a", Time = 1, Score = 10, Descendants = 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(itemPayload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(2)).ToList();

        stories.Should().HaveCount(1);
        stories.First().Title.Should().Be("Story 1");
    }

    [Fact]
    public async Task GetBestStoriesAsync_UsesCacheForIdsAndStories()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1, 2 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem { Title = "Story", Url = "http://story", By = "author", Time = 1, Score = 10, Descendants = 1 });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        await service.GetBestStoriesAsync(2);
        await service.GetBestStoriesAsync(2);

        var bestStoriesCalls = handler.Requests.Count(r => r.RequestUri!.AbsolutePath.EndsWith("beststories.json"));
        var itemCalls = handler.Requests.Count(r => r.RequestUri!.AbsolutePath.Contains("/item/"));

        bestStoriesCalls.Should().Be(1, "best story ids should be cached");
        itemCalls.Should().Be(2, "each story is cached and should be fetched once");
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsEmptyList_WhenBestStoriesApiReturnsEmptyArray()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            var bestIds = JsonSerializer.Serialize(Array.Empty<int>());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(bestIds)
            };
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(10)).ToList();

        stories.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsRequestedNumber_WhenMoreStoriesAvailable()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1, 2, 3, 4, 5 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var id = request.RequestUri!.AbsolutePath.Split('/').Last().Replace(".json", "");
            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = $"Story {id}", 
                Url = $"http://{id}", 
                By = "author", 
                Time = int.Parse(id), 
                Score = int.Parse(id) * 10, 
                Descendants = 1 
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(3)).ToList();

        stories.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBestStoriesAsync_HandlesMultipleFailedRequests()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1, 2, 3, 4, 5 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            if (request.RequestUri!.AbsolutePath.Contains("/1") || request.RequestUri!.AbsolutePath.Contains("/3"))
            {
                var id = request.RequestUri!.AbsolutePath.Split('/').Last().Replace(".json", "");
                var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
                { 
                    Title = $"Story {id}", 
                    Url = $"http://{id}", 
                    By = "author", 
                    Time = int.Parse(id), 
                    Score = int.Parse(id) * 10, 
                    Descendants = 1 
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(itemPayload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(5)).ToList();

        stories.Should().HaveCount(2);
        stories.Select(s => s.Title).Should().Contain(new[] { "Story 1", "Story 3" });
    }

    [Fact]
    public async Task GetBestStoriesAsync_MapsAllFieldsCorrectly()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = "Test Story", 
                Url = "http://test.com", 
                By = "testauthor", 
                Time = 1234567890, 
                Score = 100, 
                Descendants = 50 
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(1)).ToList();

        stories.Should().HaveCount(1);
        var story = stories.First();
        story.Title.Should().Be("Test Story");
        story.Uri.Should().Be("http://test.com");
        story.PostedBy.Should().Be("testauthor");
        story.Time.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1234567890).DateTime);
        story.Score.Should().Be(100);
        story.CommentCount.Should().Be(50);
    }

    [Fact]
    public async Task GetBestStoriesAsync_ThrowsException_WhenBestStoriesApiCallFails()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetBestStoriesAsync(10));
    }

    [Fact]
    public async Task GetBestStoriesAsync_LogsError_WhenExceptionOccurs()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var loggerMock = new Mock<ILogger<HackerNewService>>();
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HackerNewService(httpClient, cache, loggerMock.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetBestStoriesAsync(10));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error fetching best stories")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_LogsInformation_WhenStoryIdsRetrievedFromCache()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = "Story", 
                Url = "http://story", 
                By = "author", 
                Time = 1, 
                Score = 10, 
                Descendants = 1 
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var loggerMock = new Mock<ILogger<HackerNewService>>();
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HackerNewService(httpClient, cache, loggerMock.Object);

        await service.GetBestStoriesAsync(1);
        await service.GetBestStoriesAsync(1);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Story IDs retrived from cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_LogsInformation_WhenStoryIdsRetrievedFromApi()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = "Story", 
                Url = "http://story", 
                By = "author", 
                Time = 1, 
                Score = 10, 
                Descendants = 1 
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var loggerMock = new Mock<ILogger<HackerNewService>>();
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HackerNewService(httpClient, cache, loggerMock.Object);

        await service.GetBestStoriesAsync(1);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Story IDs retrieved from API")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_LogsInformation_WhenStoryDetailsRetrievedFromCache()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = "Story", 
                Url = "http://story", 
                By = "author", 
                Time = 1, 
                Score = 10, 
                Descendants = 1 
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var loggerMock = new Mock<ILogger<HackerNewService>>();
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HackerNewService(httpClient, cache, loggerMock.Object);

        await service.GetBestStoriesAsync(1);
        await service.GetBestStoriesAsync(1);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ID 1 | Detalis retrieved from cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_LogsWarning_WhenStoryDetailsNotFound()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null")
            };
        });

        var loggerMock = new Mock<ILogger<HackerNewService>>();
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HackerNewService(httpClient, cache, loggerMock.Object);

        await service.GetBestStoriesAsync(1);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ID 1 | Detalis  not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_LogsError_WhenStoryDetailsFetchFails()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var loggerMock = new Mock<ILogger<HackerNewService>>();
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new HackerNewService(httpClient, cache, loggerMock.Object);

        await service.GetBestStoriesAsync(1);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ID 1 | Error fetching story details")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStoriesAsync_HandlesNullFieldsInStoryItem()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = null, 
                Url = null, 
                By = null, 
                Time = 1234567890, 
                Score = 100, 
                Descendants = 50 
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(1)).ToList();

        stories.Should().HaveCount(1);
        var story = stories.First();
        story.Title.Should().Be(string.Empty);
        story.Uri.Should().Be(string.Empty);
        story.PostedBy.Should().Be(string.Empty);
    }

    [Fact]
    public async Task GetBestStoriesAsync_CachesStoryDetailsWithCorrectExpiration()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = "Story", 
                Url = "http://story", 
                By = "author", 
                Time = 1, 
                Score = 10, 
                Descendants = 1 
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        await service.GetBestStoriesAsync(1);
        
        var itemCalls = handler.Requests.Count(r => r.RequestUri!.AbsolutePath.Contains("/item/1"));
        itemCalls.Should().Be(1);

        await service.GetBestStoriesAsync(1);
        
        itemCalls = handler.Requests.Count(r => r.RequestUri!.AbsolutePath.Contains("/item/1"));
        itemCalls.Should().Be(1, "story should be cached");
    }

    [Fact]
    public async Task GetBestStoriesAsync_FetchesOnlyRequestedNumberOfStories()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(Enumerable.Range(1, 100).ToArray());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            var id = request.RequestUri!.AbsolutePath.Split('/').Last().Replace(".json", "");
            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = $"Story {id}", 
                Url = $"http://{id}", 
                By = "author", 
                Time = int.Parse(id), 
                Score = int.Parse(id) * 10, 
                Descendants = 1 
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        await service.GetBestStoriesAsync(5);

        var itemCalls = handler.Requests.Count(r => r.RequestUri!.AbsolutePath.Contains("/item/"));
        itemCalls.Should().Be(5, "should only fetch 5 stories");
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsAllValidStories_WhenSomeRequestsFail()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("beststories.json"))
            {
                var bestIds = JsonSerializer.Serialize(new[] { 1, 2, 3 });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bestIds)
                };
            }

            if (request.RequestUri!.AbsolutePath.Contains("/2"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var id = request.RequestUri!.AbsolutePath.Split('/').Last().Replace(".json", "");
            var itemPayload = JsonSerializer.Serialize(new HackerNewItem 
            { 
                Title = $"Story {id}", 
                Url = $"http://{id}", 
                By = "author", 
                Time = int.Parse(id), 
                Score = int.Parse(id) * 10, 
                Descendants = 1 
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(itemPayload)
            };
        });

        var service = CreateService(handler);

        var stories = (await service.GetBestStoriesAsync(3)).ToList();

        stories.Should().HaveCount(2);
        stories.Select(s => s.Title).Should().Contain(new[] { "Story 1", "Story 3" });
    }

    private static HackerNewService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewService>>();
        return new HackerNewService(httpClient, cache, logger);
    }

    private class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = _handler(request);
            return Task.FromResult(response);
        }
    }
}