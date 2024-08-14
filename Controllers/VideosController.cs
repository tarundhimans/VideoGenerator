using VideoGenerator.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PexelsDotNetSDK.Api;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VideoGenerator.Controllers
{
    public class VideosController : Controller
    {
        private readonly string pexelsApiKey;
        private readonly string geminiBaseUrl;
        private readonly string geminiApiKey;

        public VideosController(IConfiguration configuration)
        {
            pexelsApiKey = configuration["PexelsApiKey"];
            geminiBaseUrl = configuration["GeminiApi:BaseUrl"];
            geminiApiKey = configuration["GeminiApi:ApiKey"];
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
        public async Task<IActionResult> SearchVideos(string selectedQuote)
        {
            if (string.IsNullOrEmpty(selectedQuote))
            {
                return View(new List<VideoViewModel>());
            }

            var pexelsClient = new PexelsClient(pexelsApiKey);
            var result = await pexelsClient.SearchVideosAsync(selectedQuote);
            var videoModels = new List<VideoViewModel>();

            if (result?.videos != null && result.videos.Any())
            {
                videoModels = result.videos.Select(v => new VideoViewModel
                {
                    Url = v.videoFiles.FirstOrDefault()?.link,
                    ThumbnailUrl = v.image,
                    VideoDownloadUrl = v.videoFiles.FirstOrDefault()?.link}).ToList();
            }

            return View("SearchResults", videoModels);
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
    }


    public class Part
    {
        public string Text { get; set; }
    }

    public class Content
    {
        public List<Part> Parts { get; set; }
    }

    public class Candidate
    {
        public Content Content { get; set; }
    }

    public class GeminiQuotesResponse
    {
        public List<Candidate> Candidates { get; set; }
    }

}