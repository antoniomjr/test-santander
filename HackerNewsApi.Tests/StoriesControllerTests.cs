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