using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using codingTest.Controllers;
using codingTest.Models;
using codingTest.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace HackerNewsApi.Tests;

public class StoriesControllerTests
{
    private readonly Mock<IHackerNewService> _serviceMock = new();
    private readonly Mock<ILogger<StoriesController>> _loggerMock = new();

    [Fact]
    public async Task GetBestStories_ReturnsBadRequest_WhenNIsZeroOrLess()
    {
        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(0);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Parameter 'n' must be greater than zero" });
        _serviceMock.Verify(s => s.GetBestStoriesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBestStories_ReturnsBadRequest_WhenNIsGreaterThan500()
    {
        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(501);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Parameter 'n' cannot be greater than 500" });
        _serviceMock.Verify(s => s.GetBestStoriesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBestStories_ReturnsOkWithStories_WhenRequestIsValid()
    {
        var stories = new List<HackerNewStory>
        {
            new() { Title = "Story B", Score = 2 },
            new() { Title = "Story A", Score = 3 }
        };
        _serviceMock.Setup(s => s.GetBestStoriesAsync(2)).ReturnsAsync(stories);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(2);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(stories);
        _serviceMock.Verify(s => s.GetBestStoriesAsync(2), Times.Once);
    }
}

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
