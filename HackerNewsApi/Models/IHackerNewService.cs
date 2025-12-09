namespace codingTest.Models;

public interface IHackerNewService
{
    Task<IEnumerable<HackerNewStory>> GetBestStoriesAsync(int count);   
}