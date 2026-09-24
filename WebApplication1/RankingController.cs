using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebApplication1
{
    [ApiController]
    [Route("api/ranking")]
    public class RankingController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        private const string ICC_CLIENT_ID =
            "tPZJbRgIub3Vua93/DWtyQ==";

        public RankingController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet("{compType}/{rankType}")]
        public async Task<IActionResult> GetRanking(
            string compType,
            string rankType)
        {
            try
            {
                var allowedCompTypes = new[]
                {
                    "test",
                    "odi",
                    "t20",
                    "testw",
                    "odiw",
                    "t20w"
                };

                var allowedRankTypes = new[]
                {
                    "team",
                    "bat",
                    "bowl",
                    "allrounder"
                };

                compType = (compType ?? "")
                    .Trim()
                    .ToLower();

                rankType = (rankType ?? "")
                    .Trim()
                    .ToLower();

                if (!Array.Exists(
                    allowedCompTypes,
                    x => x == compType))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid competition type."
                    });
                }

                if (!Array.Exists(
                    allowedRankTypes,
                    x => x == rankType))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid ranking type."
                    });
                }

                var url =
                    "https://assets-icc.sportz.io/cricket/v1/ranking" +
                    "?client_id=" +
                    Uri.EscapeDataString(ICC_CLIENT_ID) +
                    "&comp_type=" +
                    Uri.EscapeDataString(compType) +
                    "&lang=en" +
                    "&feed_format=json" +
                    "&type=" +
                    Uri.EscapeDataString(rankType);

                var response =
                    await _httpClient.GetAsync(url);

                var content =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(
                        (int)response.StatusCode,
                        new
                        {
                            success = false,
                            message = "ICC ranking API failed.",
                            data = content
                        });
                }

                return Content(
                    content,
                    "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = "Unable to load ranking.",
                        error = ex.Message
                    });
            }
        }
    }
}