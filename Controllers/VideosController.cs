using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PexelsDotNetSDK.Api;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoGenerator.Models;

namespace VideoGenerator.Controllers
{
    public class VideosController : Controller
    {
        private readonly string pexelsApiKey;
        private readonly string geminiBaseUrl;
        private readonly string geminiApiKey;
        private readonly CloudinaryService _cloudinaryService;

        public VideosController(IConfiguration configuration)
        {
            pexelsApiKey = configuration["PexelsApiKey"];
            geminiBaseUrl = configuration["GeminiApi:BaseUrl"];
            geminiApiKey = configuration["GeminiApi:ApiKey"];

            _cloudinaryService = new CloudinaryService(
                configuration["Cloudinary:CloudName"],
                configuration["Cloudinary:ApiKey"],
                configuration["Cloudinary:ApiSecret"]
            );
        }

        public IActionResult Search()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SearchQuotes(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                ModelState.AddModelError("prompt", "The search prompt is required.");
                return View("Search", prompt);
            }

            var responseContent = await GetQuotesFromGeminiApi(prompt);
            var jsonResponse = JsonDocument.Parse(responseContent);

            if (jsonResponse.RootElement.TryGetProperty("candidates", out JsonElement candidatesElement) &&
                candidatesElement[0].TryGetProperty("content", out JsonElement contentElement) &&
                contentElement.TryGetProperty("parts", out JsonElement partsElement) &&
                partsElement[0].TryGetProperty("text", out JsonElement textElement))
            {
                var textContent = textElement.GetString();
                var allQuotes = textContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                var filteredQuotes = allQuotes.Select(quote => RemoveAuthorAttribution(quote))
                    .Where(quote => CountWords(quote) >= 3 && CountWords(quote) <= 4).ToList();

                return RedirectToAction("SearchQuotesResults", new { quotes = filteredQuotes });
            }
            else
            {
                return RedirectToAction("SearchQuotesResults", new { quotes = new List<string> { "No quotes found or unexpected response structure." } });
            }
        }

        [HttpGet]
        public IActionResult SearchQuotesResults(List<string> quotes)
        {
            return View("SearchQuotesResults", quotes);
        }

        [HttpPost]
        public async Task<IActionResult> DisplayVideo(string selectedQuote)
        {
            if (string.IsNullOrEmpty(selectedQuote))
            {
                return View("Error", new { message = "No quote selected." });
            }

            try
            {
               
                var videoUrl = await GetVideoUrlFromPexels();

              
                var processedVideoUrl = await _cloudinaryService.AddTextToVideoAsync(videoUrl, selectedQuote);

                return View("DisplayVideo", new DisplayVideoViewModel
                {
                    VideoUrl = processedVideoUrl,
                    DownloadLink = processedVideoUrl
                });
            }
            catch (Exception ex)
            {
               
                return View("Error", new { message = $"Failed to process video: {ex.Message}" });
            }
        }

        private async Task<string> GetVideoUrlFromPexels()
        {
            using (var httpClient = new HttpClient())
            {
                var requestUri = "https://api.pexels.com/videos/popular";
                httpClient.DefaultRequestHeaders.Add("Authorization", pexelsApiKey);

                var response = await httpClient.GetStringAsync(requestUri);
                var jsonResponse = JsonDocument.Parse(response);

                if (jsonResponse.RootElement.TryGetProperty("videos", out JsonElement videosElement) &&
                    videosElement.GetArrayLength() > 0)
                {
                   
                    var videoFiles = videosElement
                        .EnumerateArray()
                        .SelectMany(video => video.GetProperty("video_files").EnumerateArray())
                        .ToList();

                    if (videoFiles.Count > 0)
                    {
                        // Video Generate
                        var random = new Random();
                        var randomVideoFile = videoFiles[random.Next(videoFiles.Count)];

                        if (randomVideoFile.TryGetProperty("link", out JsonElement linkElement))
                        {
                            return linkElement.GetString();
                        }
                    }
                }

                throw new Exception("No video found in Pexels API response.");
            }
        }

        private async Task<string> GetQuotesFromGeminiApi(string prompt)
        {
            using (var httpClient = new HttpClient())
            {
                var requestUri = $"{geminiBaseUrl}?key={geminiApiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Request failed with status code {response.StatusCode}: {errorResponse}");
                }

                return await response.Content.ReadAsStringAsync();
            }
        }

        private int CountWords(string quote)
        {
            return quote.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private string RemoveAuthorAttribution(string quote)
        {
            var delimiters = new[] { "-", "—", ",", "–" };
            foreach (var delimiter in delimiters)
            {
                if (quote.Contains(delimiter))
                {
                    quote = quote.Split(delimiter)[0].Trim();
                }
            }
            return quote;
        }
    }
}
