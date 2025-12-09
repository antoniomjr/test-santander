using codingTest.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace codingTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly IHackerNewService _hackerNewsService;
    private readonly ILogger<StoriesController> _logger;

    public StoriesController(
        IHackerNewService hackerNewsService,
        ILogger<StoriesController> logger)
    {
        _hackerNewsService = hackerNewsService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the best N stories from Hacker News sorted by score
    /// </summary>
    /// <param name="n">Number of stories to return</param>
    /// <returns>List of stories sorted by score in descending order</returns>
    [HttpGet("best")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBestStories([FromQuery] int n = 10)
    {
        if (n <= 0)
        {
            return BadRequest(new { error = "Parameter 'n' must be greater than zero" });
        }

        if (n > 500)
        {
            return BadRequest(new { error = "Parameter 'n' cannot be greater than 500" });
        }

        try
        {
            _logger.LogInformation($"Request to get {n} best stories");
            var stories = await _hackerNewsService.GetBestStoriesAsync(n);
            return Ok(stories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request");
            return StatusCode(500, new { error = "Error processing request" });
        }
    }
}
