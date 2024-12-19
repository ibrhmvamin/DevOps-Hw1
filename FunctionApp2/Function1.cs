using System;
using System.Net.Http.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FunctionApp2
{
    public class Function1
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { },
            User = "",
            Password = ""
        });

        private readonly ILogger<Function1> _logger;
        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function(nameof(Function1))]
        public async Task Run(
            [QueueTrigger("mychannel", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            try
            {
                string? queueMessage = message.Body.ToString();
                _logger.LogInformation($"Message: {queueMessage}");

                string omdbApiKey = "e750289a";
                string omdbApiUrl = $"http://www.omdbapi.com/?apikey={omdbApiKey}&t={queueMessage}";

                var movieResponse = await _httpClient.GetFromJsonAsync<MovieResponse>(omdbApiUrl);

                if (movieResponse == null || string.IsNullOrEmpty(movieResponse.Title) || string.IsNullOrEmpty(movieResponse.Poster))
                {
                    _logger.LogWarning("API key error");
                    return;
                }

                _logger.LogInformation($"Film: {movieResponse.Title}, Poster: {movieResponse.Poster}");

                var db = _redis.GetDatabase();

                string movieKey = movieResponse.Title;
                string guidKey = Guid.NewGuid().ToString();

                await db.StringSetAsync(movieKey, movieResponse.Poster);
                await db.ListRightPushAsync("images", movieResponse.Poster);

                _logger.LogInformation($"Film Sended SuccesFully: {movieKey}, Poster: {movieResponse.Poster}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
            }
        }
    }
    public class MovieResponse
    {
        public string Title { get; set; }
        public string Poster { get; set; }
    }
}

