using System.Net;
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
    public async Task GetBestStories_ReturnsBadRequest_WhenNIsNegative()
    {
        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(-5);

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

    [Fact]
    public async Task GetBestStories_UsesDefaultValue_WhenNIsNotProvided()
    {
        var stories = new List<HackerNewStory>
        {
            new() { Title = "Story 1", Score = 10 }
        };
        _serviceMock.Setup(s => s.GetBestStoriesAsync(10)).ReturnsAsync(stories);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        _serviceMock.Verify(s => s.GetBestStoriesAsync(10), Times.Once);
    }

    [Fact]
    public async Task GetBestStories_ReturnsOk_WhenNIs500()
    {
        var stories = new List<HackerNewStory>();
        _serviceMock.Setup(s => s.GetBestStoriesAsync(500)).ReturnsAsync(stories);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(500);

        result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.GetBestStoriesAsync(500), Times.Once);
    }

    [Fact]
    public async Task GetBestStories_ReturnsOk_WhenNIs1()
    {
        var stories = new List<HackerNewStory>
        {
            new() { Title = "Story 1", Score = 10 }
        };
        _serviceMock.Setup(s => s.GetBestStoriesAsync(1)).ReturnsAsync(stories);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(1);

        result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(s => s.GetBestStoriesAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetBestStories_ReturnsInternalServerError_WhenServiceThrowsException()
    {
        _serviceMock.Setup(s => s.GetBestStoriesAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Service error"));

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(10);

        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
        statusCode.Value.Should().BeEquivalentTo(new { error = "Error processing request" });
    }

    [Fact]
    public async Task GetBestStories_LogsInformation_WhenRequestIsValid()
    {
        var stories = new List<HackerNewStory>();
        _serviceMock.Setup(s => s.GetBestStoriesAsync(10)).ReturnsAsync(stories);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        await controller.GetBestStories(10);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Request to get 10 best stories")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStories_LogsError_WhenExceptionOccurs()
    {
        var exception = new Exception("Service error");
        _serviceMock.Setup(s => s.GetBestStoriesAsync(It.IsAny<int>()))
            .ThrowsAsync(exception);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        await controller.GetBestStories(10);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing request")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBestStories_ReturnsEmptyList_WhenServiceReturnsEmpty()
    {
        var stories = new List<HackerNewStory>();
        _serviceMock.Setup(s => s.GetBestStoriesAsync(10)).ReturnsAsync(stories);

        var controller = new StoriesController(_serviceMock.Object, _loggerMock.Object);

        var result = await controller.GetBestStories(10);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedStories = ok.Value.Should().BeAssignableTo<IEnumerable<HackerNewStory>>().Subject;
        returnedStories.Should().BeEmpty();
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