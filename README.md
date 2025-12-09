# Hacker News API

RESTful API built with ASP.NET Core to retrieve the best stories from Hacker News.

## Requirements

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)

## How to Run

```bash
# Clone the repository
git clone https://github.com/antoniomjr/test-santander.git
cd test-santander

# Restore dependencies
dotnet restore

# Run the application
dotnet run --project HackerNewsApi
```

## Usage

### Endpoint
GET /api/stories/best?n={number}

### Parameters
n (optional): Number of stories to return (default: 10, max: 500)

### Example Request
```bash
curl http://localhost:****/api/stories/best?n=10
```

### Example Response
```
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01Z",
    "score": 1716,
    "commentCount": 572
  },
  {
    "title": "Another story...",
    "uri": "https://example.com",
    "postedBy": "username",
    "time": "2019-10-11T10:30:00Z",
    "score": 1500,
    "commentCount": 300
  }
]
```
## Architecture

```
HackerNewsApi/
├── Controllers/        # API endpoints
├── Services/          # Business logic
├── Models/            # Data models
└── Program.cs         # Application configuration
```

## Key Features
 - In-Memory Caching: Reduces API calls to Hacker News (5-minute cache)
 - Parallel Requests: Fetches multiple stories simultaneously
 - Error Handling: Robust error handling and logging
 - Input Validation: Validates request parameters

## Performance
 - First request (cache miss): ~2-3 seconds
 - Cached requests: ~50-100ms
 - Cache duration: 5 minutes

## Assumptions
 - Hacker News API is publicly available with no rate limits
 - Story data doesn't change frequently (5-minute cache is acceptable)
 - Maximum of 500 stories can be requested at once
 - Stories are returned in descending order by score

## Technologies
 - ASP.NET Core 9.0
 - Memory Cache
 - Swagger/OpenAPI
