using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;

namespace WebApplication1
{
    public class SquadPlayer { public int Id { get; set; } public string Name { get; set; } = ""; public string Role { get; set; } = ""; public string Team { get; set; } = ""; public string ProfileUrl { get; set; } = ""; public string ImageUrl { get; set; } = ""; public string Section { get; set; } = ""; }
    [ApiController]
    [Route("api/[controller]")]
    public class SquadController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public SquadController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }



        [HttpGet("{matchId:int}")]
        public async Task<IActionResult> GetSquad(int matchId)
        {
            try
            {
                if (matchId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid matchId"
                    });
                }

                _httpClient.DefaultRequestHeaders.Clear();

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/139.0.0.0 Safari/537.36"
                );

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
                );

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Accept-Language",
                    "en-US,en;q=0.9"
                );

                // IMPORTANT:
                // Sirf matchId chahiye
                string url =
                    $"https://www.cricbuzz.com/cricket-match-squads/{matchId}";

                var response = await _httpClient.GetAsync(url);

                // Cricbuzz ka actual status return karo
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(
                        (int)response.StatusCode,
                        new
                        {
                            success = false,
                            matchId = matchId,
                            message = "Cricbuzz request failed",
                            statusCode = (int)response.StatusCode
                        }
                    );
                }

                string html = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(html))
                {
                    return NotFound(new
                    {
                        success = false,
                        matchId = matchId,
                        message = "Empty Cricbuzz response"
                    });
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // =========================================================
                // TEAMS
                // =========================================================

                string team1Name = "";
                string team2Name = "";

                string team1Short = "";
                string team2Short = "";

                // ---------------------------------------------------------
                // Match title
                // ---------------------------------------------------------

                var titleNode = doc.DocumentNode.SelectSingleNode("//h1");

                string titleText = titleNode != null
                    ? HtmlEntity.DeEntitize(titleNode.InnerText).Trim()
                    : "";

                var vsMatch = Regex.Match(
                    titleText,
                    @"(.+?)\s+vs\s+(.+?)(?:,|$)",
                    RegexOptions.IgnoreCase
                );

                if (vsMatch.Success)
                {
                    team1Name = vsMatch.Groups[1].Value.Trim();
                    team2Name = vsMatch.Groups[2].Value.Trim();
                }

                // ---------------------------------------------------------
                // Team short names
                // ---------------------------------------------------------

                var teamImages = doc.DocumentNode.SelectNodes("//img");

                if (teamImages != null)
                {
                    foreach (var img in teamImages)
                    {
                        string alt = HtmlEntity.DeEntitize(
                            img.GetAttributeValue("alt", "")
                        ).Trim();

                        if (string.IsNullOrWhiteSpace(alt))
                            continue;

                        // Ignore generic image alts
                        if (alt.Equals("Cricbuzz", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (string.IsNullOrEmpty(team1Short))
                        {
                            team1Short = alt;
                            continue;
                        }

                        if (!alt.Equals(
                                team1Short,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.IsNullOrEmpty(team2Short))
                        {
                            team2Short = alt;
                            break;
                        }
                    }
                }


                // =========================================================
                // SECTIONS
                // =========================================================

                var sections =
                    new Dictionary<string, List<object>>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        ["Squad"] = new List<object>(),
                        ["Playing XI"] = new List<object>(),
                        ["Bench"] = new List<object>(),
                        ["Support Staff"] = new List<object>()
                    };


                // =========================================================
                // ALL NODES
                // =========================================================
                //
                // Cricbuzz heading h1-h6 me hi ho ye zaroori nahi.
                // Isliye poore DOM ko order me read karenge.
                // =========================================================

                var allNodes = doc.DocumentNode.SelectNodes("//*");

                if (allNodes == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        matchId = matchId,
                        message = "Cricbuzz page structure not found"
                    });
                }

                // ---------------------------------------------------------
                // Heading positions
                // ---------------------------------------------------------

                var sectionPositions =
                    new List<(int index, string section)>();

                for (int i = 0; i < allNodes.Count; i++)
                {
                    var node = allNodes[i];

                    string text = CleanSquadText(node.InnerText);

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    string section = GetSectionName(text);

                    if (!string.IsNullOrEmpty(section))
                    {
                        sectionPositions.Add(
                            (i, section)
                        );
                    }
                }


                // =========================================================
                // PLAYER LINKS
                // =========================================================

                var playerLinks = doc.DocumentNode.SelectNodes(
                    "//a[contains(@href,'/profiles/')]"
                );

                if (playerLinks != null)
                {
                    foreach (var link in playerLinks)
                    {
                        string href =
                            link.GetAttributeValue("href", "");

                        var idMatch = Regex.Match(
                            href,
                            @"/profiles/(\d+)"
                        );

                        if (!idMatch.Success)
                            continue;

                        int playerId;

                        if (!int.TryParse(
                                idMatch.Groups[1].Value,
                                out playerId))
                        {
                            continue;
                        }


                        // =================================================
                        // PLAYER NAME
                        // =================================================

                        string playerName = "";

                        // Pehle span try
                        var nameNode = link.SelectSingleNode(".//span");

                        if (nameNode != null)
                        {
                            playerName = CleanSquadText(nameNode.InnerText);
                        }

                        // Fallback
                        if (string.IsNullOrWhiteSpace(playerName))
                        {
                            playerName = CleanSquadText(link.InnerText);
                        }


                        // -------------------------------------------------
                        // Role
                        // -------------------------------------------------

                        string role = "";

                        var roleNode = link.SelectSingleNode(
                            ".//*[contains(@class,'text-cbTxtSec')]"
                        );

                        if (roleNode != null)
                        {
                            role = CleanSquadText(roleNode.InnerText);
                        }


                        // -------------------------------------------------
                        // Image
                        // -------------------------------------------------

                        string imageUrl = "";

                        var playerImg = link.SelectSingleNode(".//img");

                        if (playerImg != null)
                        {
                            imageUrl = playerImg.GetAttributeValue("src","");
                            if (string.IsNullOrWhiteSpace(imageUrl))
                            {
                                imageUrl = playerImg.GetAttributeValue("data-src","");
                            }
                        }


                        // =================================================
                        // FIND SECTION
                        // =================================================

                        int playerIndex = allNodes.IndexOf(link);

                        string sectionName = "Squad";

                        // Latest section heading before player
                        for (int i = sectionPositions.Count - 1;i >= 0;i--)
                        {
                            if (sectionPositions[i].index < playerIndex)
                            {
                                sectionName = sectionPositions[i].section;
                                break;
                            }
                        }


                        // =================================================
                        // TEAM
                        // =================================================

                        string playerTeam = "";

                        var parent = link.ParentNode;

                        int depth = 0;

                        while (parent != null && depth < 12)
                        {
                            string className = parent.GetAttributeValue("class","");

                            // Cricbuzz different layouts me
                            // ye classes use kar sakta hai.
                            bool isHalfColumn =
                                className.Contains("w-1/2") ||
                                className.Contains("cb-col-50") ||
                                className.Contains("col-50");

                            if (isHalfColumn)
                            {
                                var parentNode = parent.ParentNode;

                                if (parentNode != null)
                                {
                                    var children =
                                        parentNode.ChildNodes
                                            .Where(x =>
                                                x.NodeType ==
                                                HtmlNodeType.Element)
                                            .ToList();

                                    int childIndex =
                                        children.IndexOf(parent);

                                    if (childIndex == 0)
                                    {
                                        playerTeam =
                                            team1Short;
                                    }
                                    else if (childIndex > 0)
                                    {
                                        playerTeam =
                                            team2Short;
                                    }
                                }

                                break;
                            }

                            parent =
                                parent.ParentNode;

                            depth++;
                        }


                        // =================================================
                        // DUPLICATE
                        // =================================================

                        bool exists =
                            sections[sectionName].Any(x =>
                            {
                                var property =
                                    x.GetType().GetProperty("id");

                                return property != null &&
                                       property
                                           .GetValue(x)?
                                           .ToString() ==
                                       playerId.ToString();
                            });

                        if (exists)
                            continue;


                        // =================================================
                        // ADD PLAYER
                        // =================================================

                        sections[sectionName].Add(
                            new
                            {
                                id = playerId,
                                name = playerName,
                                role = role,
                                team = playerTeam,
                                profileUrl = href,
                                imageUrl = imageUrl,
                                section = sectionName
                            }
                        );
                    }
                }


                // =========================================================
                // SUPPORT STAFF
                // =========================================================
                //
                // Support Staff ke profile links bhi collect honge.
                // =========================================================

                // Agar heading mila hai to us heading ke baad wale
                // profile links ko Support Staff me rakhenge.
                //
                // Lekin player links already process ho chuke hain,
                // isliye section detection se jo Support Staff me aaye
                // wahi staff honge.

                var supportStaff =
                    sections["Support Staff"];


                // =========================================================
                // ANNOUNCED / NOT ANNOUNCED
                // =========================================================

                bool hasPlayingXI =
                    sections["Playing XI"].Count > 0;

                bool hasBench =
                    sections["Bench"].Count > 0;


                // =========================================================
                // IMPORTANT LOGIC
                // =========================================================
                //
                // Squad announce nahi hua:
                //
                // Squad
                // Support Staff
                //
                // hi dikhega.
                //
                // Playing XI announce hua:
                //
                // Squad
                // Playing XI
                // Bench
                // Support Staff
                //
                // =========================================================

                if (!hasPlayingXI)
                {
                    sections["Playing XI"].Clear();
                    sections["Bench"].Clear();
                }


                // =========================================================
                // RESPONSE
                // =========================================================

                return Ok(
                    new
                    {
                        success = true,

                        matchId = matchId,

                        team1 = new
                        {
                            name = team1Name,
                            shortName = team1Short
                        },

                        team2 = new
                        {
                            name = team2Name,
                            shortName = team2Short
                        },

                        hasPlayingXI = hasPlayingXI,

                        hasBench = hasBench,

                        squad = new
                        {
                            playerCount =
                                sections["Squad"].Count,

                            players =
                                sections["Squad"]
                        },

                        playingXI = new
                        {
                            playerCount =
                                sections["Playing XI"].Count,

                            players =
                                sections["Playing XI"]
                        },

                        bench = new
                        {
                            playerCount =
                                sections["Bench"].Count,

                            players =
                                sections["Bench"]
                        },

                        supportStaff = new
                        {
                            staffCount =
                                supportStaff.Count,

                            staff =
                                supportStaff
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        matchId = matchId,
                        error = ex.Message,
                        innerError =
                            ex.InnerException?.Message
                    }
                );
            }
        }

        private static string CleanSquadText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = HtmlEntity.DeEntitize(text);

            text = Regex.Replace(
                text,
                @"\s+",
                " "
            );

            return text.Trim();
        }



        private static string GetSectionName(string text)
        {
            text = CleanSquadText(text);

            if (text.Equals(
                    "Squad",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Squad";
            }

            if (text.Equals(
                    "Playing XI",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Playing XI";
            }

            if (text.Equals(
                    "Bench",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Bench";
            }

            if (text.Equals(
                    "Support Staff",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Support Staff";
            }

            return "";
        }



        [HttpGet("series/{seriesId}")]
        public async Task<IActionResult> GetSeriesMatches(int seriesId)
        {
            try
            {
                string url =
                    $"https://www.cricbuzz.com/cricket-series/{seriesId}/womens-asia-cup-2026/matches";

                _httpClient.DefaultRequestHeaders.Clear();

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/139.0 Safari/537.36"
                );

                string html = await _httpClient.GetStringAsync(url);

                // RSC scripts
                var scripts = Regex.Matches(
                    html,
                    @"self\.__next_f\.push\(\[1,""(.*?)""\]\)",
                    RegexOptions.Singleline
                );

                var rscText = string.Join("\n",
                    scripts.Select(x => x.Groups[1].Value)
                );

                // Escaping remove
                rscText = rscText
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                // -----------------------------------------
                // SERIES KE ACTUAL MATCHES
                // -----------------------------------------

                var matches = new List<object>();
                var matchBlocks = Regex.Matches(rscText, $@"""matchId"":(\d+),""seriesId"":{seriesId},", RegexOptions.IgnoreCase);
                var matchIds = matchBlocks.Select(x => x.Groups[1].Value).Distinct().ToList();

                return Ok(new
                {
                    success = true,
                    seriesId = seriesId,
                    matchCount = matchIds.Count,
                    matchIds = matchIds
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }


        [HttpGet("match/{matchId}")]
        public async Task<IActionResult> GetMatchDetails(int matchId)
        {
            try
            {
                string url = $"https://www.cricbuzz.com/live-cricket-scores/{matchId}";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/139.0 Safari/537.36");

                string html = await _httpClient.GetStringAsync(url);

                var scripts = Regex.Matches(html, @"self\.__next_f\.push\(\[1,""(.*?)""\]\)", RegexOptions.Singleline);

                var rscText = string.Join("\n", scripts.Select(x => x.Groups[1].Value));

                rscText = rscText.Replace("\\\"", "\"").Replace("\\\\", "\\");

                var matchIndex = rscText.IndexOf($"\"matchId\":{matchId}");

                if (matchIndex < 0)
                {
                    return Ok(new
                    {
                        success = false,
                        matchId,
                        error = "Match not found"
                    });
                }

                var start = Math.Max(0, matchIndex - 500);
                var length = Math.Min(20000, rscText.Length - start);
                var block = rscText.Substring(start, length);

                var matchDesc = GetValue(block, "matchDesc");
                var state = GetValue(block, "state");
                var status = GetValue(block, "status");
                var shortStatus = GetValue(block, "shortStatus");

                var startDateMatch = Regex.Match(block, @"""startDate"":(\d+)");
                var startDate = startDateMatch.Success ? startDateMatch.Groups[1].Value : "";

                var team1Match = Regex.Match(block, @"""team1"":\{.*?""teamName"":""([^""]+)""", RegexOptions.Singleline);
                var team2Match = Regex.Match(block, @"""team2"":\{.*?""teamName"":""([^""]+)""", RegexOptions.Singleline);

                var team1Name = team1Match.Success ? team1Match.Groups[1].Value : "";
                var team2Name = team2Match.Success ? team2Match.Groups[1].Value : "";

                var venueMatch = Regex.Match(block, @"""venueInfo"":\{.*?""ground"":""([^""]+)""", RegexOptions.Singleline);
                var venue = venueMatch.Success ? venueMatch.Groups[1].Value : "";

                string date = "";
                string time = "";

                if (long.TryParse(startDate, out long timestamp))
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                    date = dt.ToString("dd MMM yyyy");
                    time = dt.ToString("hh:mm tt");
                }

                var scoreMatches = Regex.Matches(block, @"""team\d+Score"":\{(.*?)\}", RegexOptions.Singleline);

                var scores = new List<string>();

                foreach (Match score in scoreMatches)
                {
                    var runs = Regex.Match(score.Groups[1].Value, @"""runs"":(\d+)");
                    var wickets = Regex.Match(score.Groups[1].Value, @"""wickets"":(\d+)");
                    var overs = Regex.Match(score.Groups[1].Value, @"""overs"":([\d.]+)");

                    if (runs.Success)
                    {
                        string scoreText = runs.Groups[1].Value;

                        if (wickets.Success)
                            scoreText += "-" + wickets.Groups[1].Value;

                        if (overs.Success)
                            scoreText += " (" + overs.Groups[1].Value + ")";

                        scores.Add(scoreText);
                    }
                }

                return Ok(new
                {
                    success = true,
                    matchId,
                    matchDesc,
                    date,
                    time,
                    team1 = team1Name,
                    team2 = team2Name,
                    team1Score = scores.Count > 0 ? scores[0] : "",
                    team2Score = scores.Count > 1 ? scores[1] : "",
                    state,
                    status = !string.IsNullOrEmpty(status) ? status : shortStatus,
                    venue
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    matchId,
                    error = ex.Message
                });
            }
        }


        [HttpGet("series/{seriesId}/details")]
        public async Task<IActionResult> GetSeriesMatchDetails(int seriesId)
        {
            try
            {
                string url = $"https://www.cricbuzz.com/cricket-series/{seriesId}/womens-asia-cup-2026/matches";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/139.0 Safari/537.36");

                string html = await _httpClient.GetStringAsync(url);

                var scripts = Regex.Matches(html, @"self\.__next_f\.push\(\[1,""(.*?)""\]\)", RegexOptions.Singleline);

                var rscText = string.Join("\n", scripts.Select(x => x.Groups[1].Value));

                rscText = rscText.Replace("\\\"", "\"").Replace("\\\\", "\\");

                var matchIds = Regex.Matches(rscText, $@"""matchId"":(\d+),""seriesId"":{seriesId},", RegexOptions.IgnoreCase).Select(x => x.Groups[1].Value).Distinct().ToList();
                var matches = new List<object>();

                foreach (var matchId in matchIds)
                {
                    var matchIndex = rscText.IndexOf($"\"matchId\":{matchId},\"seriesId\":{seriesId},", StringComparison.OrdinalIgnoreCase);

                    if (matchIndex < 0)
                        continue;

                    var nextMatchIndex = rscText.IndexOf("\"matchInfo\":{\"matchId\":", matchIndex + 1, StringComparison.OrdinalIgnoreCase);

                    var blockStart = matchIndex;
                    var blockLength = nextMatchIndex > blockStart ? nextMatchIndex - blockStart : Math.Min(20000, rscText.Length - blockStart);

                    var block = rscText.Substring(blockStart, blockLength);

                    var matchDesc = GetValue(block, "matchDesc");
                    var startDate = GetValue(block, "startDate");
                    var state = GetValue(block, "state");
                    var status = GetValue(block, "status");
                    var shortStatus = GetValue(block, "shortStatus");

                    var team1Match = Regex.Match(block, @"""team1"":\{.*?""teamName"":""([^""]+)""", RegexOptions.Singleline);
                    var team2Match = Regex.Match(block, @"""team2"":\{.*?""teamName"":""([^""]+)""", RegexOptions.Singleline);

                    var team1Name = team1Match.Success ? team1Match.Groups[1].Value : "";
                    var team2Name = team2Match.Success ? team2Match.Groups[1].Value : "";

                    var venueMatch = Regex.Match(block, @"""venueInfo"":\{.*?""ground"":""([^""]+)""", RegexOptions.Singleline);
                    var cityMatch = Regex.Match(block, @"""venueInfo"":\{.*?""city"":""([^""]+)""", RegexOptions.Singleline);

                    var venue = venueMatch.Success ? venueMatch.Groups[1].Value : "";
                    var city = cityMatch.Success ? cityMatch.Groups[1].Value : "";

                    string date = "";
                    string time = "";

                    if (long.TryParse(startDate, out long timestamp))
                    {
                        var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                        date = dt.ToString("dd MMM yyyy");
                        time = dt.ToString("hh:mm tt");
                    }

                    var scoreMatches = Regex.Matches(block, @"""team\d+Score"":\{(.*?)\}", RegexOptions.Singleline);

                    var scores = new List<string>();

                    foreach (Match score in scoreMatches)
                    {
                        var scoreBlock = score.Groups[1].Value;

                        var runs = Regex.Match(scoreBlock, @"""runs"":(\d+)");
                        var wickets = Regex.Match(scoreBlock, @"""wickets"":(\d+)");
                        var overs = Regex.Match(scoreBlock, @"""overs"":([\d.]+)");

                        if (runs.Success)
                        {
                            string scoreText = runs.Groups[1].Value;

                            if (wickets.Success)
                                scoreText += "-" + wickets.Groups[1].Value;

                            if (overs.Success)
                                scoreText += " (" + overs.Groups[1].Value + ")";

                            scores.Add(scoreText);
                        }
                    }

                    string finalStatus = !string.IsNullOrEmpty(status) ? status : shortStatus;

                    string result = "";

                    if (!string.IsNullOrEmpty(finalStatus) && !finalStatus.StartsWith("Match starts", StringComparison.OrdinalIgnoreCase))
                    {
                        result = finalStatus;
                    }

                    matches.Add(new
                    {
                        matchId,
                        matchDesc,
                        date,
                        time,
                        team1 = team1Name,
                        team2 = team2Name,
                        team1Score = scores.Count > 0 ? scores[0] : "",
                        team2Score = scores.Count > 1 ? scores[1] : "",
                        state,
                        status = finalStatus,
                        result,
                        venue,
                        city
                    });
                }

                return Ok(new
                {
                    success = true,
                    seriesId,
                    matchCount = matches.Count,
                    matches
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    seriesId,
                    error = ex.Message
                });
            }
        }

        [HttpGet("series/{seriesId}/points-table")]
        public async Task<IActionResult> GetPointsTable(int seriesId)
        {
            try
            {
                string url =
                    $"https://www.cricbuzz.com/cricket-series/{seriesId}/womens-asia-cup-2026/points-table";

                _httpClient.DefaultRequestHeaders.Clear();

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/139.0 Safari/537.36"
                );

                string html = await _httpClient.GetStringAsync(url);

                var scripts = Regex.Matches(
                    html,
                    @"self\.__next_f\.push\(\[1,""(.*?)""\]\)",
                    RegexOptions.Singleline
                );

                var rscText = string.Join(
                    "\n",
                    scripts.Select(x => x.Groups[1].Value)
                );

                // Decode Next.js RSC escaped text
                rscText = rscText
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                // Find pointsTableData
                var pointsIndex = rscText.IndexOf(
                    @"""pointsTableData"":",
                    StringComparison.OrdinalIgnoreCase
                );

                if (pointsIndex < 0)
                {
                    return Ok(new
                    {
                        success = false,
                        seriesId,
                        error = "Points table not found"
                    });
                }

                // Find opening {
                var jsonStart = rscText.IndexOf(
                    "{",
                    pointsIndex
                );

                if (jsonStart < 0)
                {
                    return Ok(new
                    {
                        success = false,
                        seriesId,
                        error = "Points table JSON start not found"
                    });
                }

                // Find matching closing }
                int depth = 0;
                int jsonEnd = -1;
                bool inString = false;
                bool escaped = false;

                for (int i = jsonStart; i < rscText.Length; i++)
                {
                    char c = rscText[i];

                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\' && inString)
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (!inString)
                    {
                        if (c == '{')
                        {
                            depth++;
                        }
                        else if (c == '}')
                        {
                            depth--;

                            if (depth == 0)
                            {
                                jsonEnd = i;
                                break;
                            }
                        }
                    }
                }

                if (jsonEnd < 0)
                {
                    return Ok(new
                    {
                        success = false,
                        seriesId,
                        error = "Points table JSON end not found"
                    });
                }

                // Extract pointsTableData JSON
                string pointsJson = rscText.Substring(
                    jsonStart,
                    jsonEnd - jsonStart + 1
                );

                using var doc = JsonDocument.Parse(pointsJson);

                if (!doc.RootElement.TryGetProperty(
                    "pointsTable",
                    out JsonElement pointsTableElement))
                {
                    return Ok(new
                    {
                        success = false,
                        seriesId,
                        error = "pointsTable property not found"
                    });
                }

                // IMPORTANT:
                // Clone before JsonDocument is disposed
                var pointsTable = pointsTableElement.Clone();

                return Ok(new
                {
                    success = true,
                    seriesId,
                    pointsTable
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    seriesId,
                    error = ex.Message
                });
            }
        }


        [HttpGet("series/{seriesId}/stats/{statsType}")]
        public async Task<IActionResult> GetSeriesStats(int seriesId, string statsType)
        {
            var allowedStats = new[] { "mostRuns", "highestScore", "highestAvg", "highestSr", "mostHundreds", "mostFifties", "mostFours", "mostSixes", "mostNineties", "mostWickets", "lowestAvg", "bestBowlingInnings", "mostFiveWickets", "lowestEcon", "lowestSr" };

            if (!allowedStats.Contains(statsType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Invalid statsType" });

            var url = $"https://www.cricbuzz.com/cricket-series/{seriesId}/womens-asia-cup-2026/stats?statsType={statsType}&matchFormat=3";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/139.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { success = false, message = "Cricbuzz request failed" });

            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tables = doc.DocumentNode.SelectNodes("//table");

            if (tables == null || tables.Count == 0)
                return NotFound(new { success = false, message = "Stats table not found" });

            var table = tables.FirstOrDefault();

            var headers = table.SelectNodes(".//thead//th")?.Select(x => HtmlEntity.DeEntitize(x.InnerText.Trim())).ToList() ?? new List<string>();

            var rows = new List<Dictionary<string, string>>();

            var trNodes = table.SelectNodes(".//tbody//tr");

            if (trNodes != null)
            {
                foreach (var tr in trNodes)
                {
                    var cells = tr.SelectNodes("./td");

                    if (cells == null)
                        continue;

                    var row = new Dictionary<string, string>();

                    for (int i = 0; i < cells.Count && i < headers.Count; i++)
                    {
                        row[headers[i]] = HtmlEntity.DeEntitize(cells[i].InnerText.Trim());
                    }

                    if (row.Count > 0)
                        rows.Add(row);
                }
            }

            return Ok(new
            {
                success = true,
                seriesId,
                statsType,
                headers,
                data = rows
            });
        }


        [HttpGet("series/{seriesId}/squads")]
        public async Task<IActionResult> GetSeriesSquads(int seriesId)
        {
            try
            {
                var url = $"https://www.cricbuzz.com/cricket-series/{seriesId}/womens-asia-cup-2026/squads";

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
                );

                request.Headers.Add(
                    "Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
                );

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(
                        (int)response.StatusCode,
                        new
                        {
                            success = false,
                            message = "Cricbuzz request failed",
                            statusCode = (int)response.StatusCode
                        }
                    );
                }

                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var teams = new List<object>();

                // Left side se team names
                var teamNodes = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class,'cursor-pointer')]//span[normalize-space()]"
                );

                var teamNames = new List<string>();

                if (teamNodes != null)
                {
                    foreach (var node in teamNodes)
                    {
                        var name = HtmlEntity.DeEntitize(node.InnerText).Trim();

                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (name.Equals("T20", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!teamNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                            teamNames.Add(name);
                    }
                }

                // Agar above selector se teams nahi mile
                if (teamNames.Count == 0)
                {
                    var knownTeams = new[]
                    {
                "India Women",
                "Pakistan Women",
                "Thailand Women",
                "Hong Kong, China Women",
                "Sri Lanka Women",
                "Bangladesh Women",
                "United Arab Emirates Women",
                "Indonesia Women"
            };

                    teamNames.AddRange(knownTeams);
                }

                // Current server-rendered squad section
                var squadContainer = doc.DocumentNode.SelectSingleNode(
                    "//div[contains(@class,'fullscreen-container')]"
                );

                if (squadContainer != null)
                {
                    string currentTeam = null;

                    // Mobile heading se current team identify karo
                    var currentTeamNode = squadContainer.SelectSingleNode(
                        ".//*[contains(@class,'tb:hidden')]"
                    );

                    if (currentTeamNode != null)
                    {
                        currentTeam = HtmlEntity.DeEntitize(
                            currentTeamNode.InnerText
                        ).Trim();
                    }

                    // Fallback: currently selected team
                    if (string.IsNullOrWhiteSpace(currentTeam))
                    {
                        var selectedTeamNode = doc.DocumentNode.SelectSingleNode(
                            "//*[contains(@class,'bg-cbGrnCyn')]//span[normalize-space()]"
                        );

                        if (selectedTeamNode != null)
                        {
                            currentTeam = HtmlEntity.DeEntitize(
                                selectedTeamNode.InnerText
                            ).Trim();
                        }
                    }

                    var players = new List<object>();

                    string currentCategory = null;

                    var children = squadContainer.SelectNodes(".//*");

                    if (children != null)
                    {
                        foreach (var node in children)
                        {
                            var text = HtmlEntity.DeEntitize(
                                node.InnerText
                            ).Trim();

                            if (string.IsNullOrWhiteSpace(text))
                                continue;

                            var upperText = text.ToUpperInvariant();

                            if (
                                upperText == "BATTERS" ||
                                upperText == "ALL ROUNDERS" ||
                                upperText == "WICKET KEEPERS" ||
                                upperText == "BOWLERS"
                            )
                            {
                                currentCategory = text;
                                continue;
                            }

                            if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                            {
                                var href = node.GetAttributeValue("href", "");

                                if (!href.Contains("/profiles/"))
                                    continue;

                                var profileNode = node.SelectSingleNode(
                                    ".//span[contains(@class,'hover:underline')]"
                                );

                                var roleNode = node.SelectSingleNode(
                                    ".//p"
                                );

                                var imageNode = node.SelectSingleNode(
                                    ".//img"
                                );

                                if (profileNode == null)
                                    continue;

                                var playerName = HtmlEntity.DeEntitize(
                                    profileNode.InnerText
                                ).Trim();

                                playerName = Regex.Replace(
                                    playerName,
                                    @"\s+",
                                    " "
                                );

                                if (string.IsNullOrWhiteSpace(playerName))
                                    continue;

                                var role = roleNode != null
                                    ? HtmlEntity.DeEntitize(roleNode.InnerText).Trim()
                                    : "";

                                var imageUrl = imageNode != null
                                    ? imageNode.GetAttributeValue("src", "")
                                    : "";

                                imageUrl = imageUrl.Replace("&amp;", "&");

                                var profileMatch = Regex.Match(
                                    href,
                                    @"/profiles/(\d+)/([^/?]+)",
                                    RegexOptions.IgnoreCase
                                );

                                int? playerId = null;
                                string profileSlug = null;

                                if (profileMatch.Success)
                                {
                                    if (int.TryParse(
                                        profileMatch.Groups[1].Value,
                                        out var id
                                    ))
                                    {
                                        playerId = id;
                                    }

                                    profileSlug = profileMatch.Groups[2].Value;
                                }

                                players.Add(new
                                {
                                    id = playerId,
                                    name = playerName,
                                    role = role,
                                    category = currentCategory,
                                    profileUrl = href,
                                    profileSlug = profileSlug,
                                    imageUrl = imageUrl,
                                    team = currentTeam
                                });
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(currentTeam))
                    {
                        var shortName = Regex.Replace(
                            currentTeam.ToLowerInvariant(),
                            @"[^a-z0-9]+",
                            "-"
                        ).Trim('-');

                        teams.Add(new
                        {
                            name = currentTeam,
                            shortName = shortName,
                            playerCount = players.Count,
                            players = players
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    seriesId = seriesId,
                    teamCount = teams.Count,
                    teams = teams
                });
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        success = false,
                        message = "Failed to fetch series squads",
                        error = ex.Message
                    }
                );
            }
        }

        private string GetValue(string text, string property)
        {
            var match = Regex.Match(
                text,
                $"\"{Regex.Escape(property)}\":\"([^\"]*)\"",
                RegexOptions.IgnoreCase
            );

            return match.Success
                ? match.Groups[1].Value
                : "";
        }

        //series squad
        [HttpGet("series/{seriesId}/players")]
        public async Task<IActionResult> GetSeriesPlayers(int seriesId)
        {
            try
            {
                string url = $"https://www.cricbuzz.com/cricket-series/{seriesId}/womens-asia-cup-2026/squads";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36"
                );

                request.Headers.TryAddWithoutValidation(
                    "Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8"
                );

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        seriesId,
                        message = "Cricbuzz request failed"
                    });
                }

                string html = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(html))
                {
                    return NotFound(new
                    {
                        success = false,
                        seriesId,
                        message = "Cricbuzz HTML empty"
                    });
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var players = new Dictionary<string, object>();

                var profileNodes = doc.DocumentNode.SelectNodes(
                    "//a[starts-with(@href,'/profiles/')]"
                );

                if (profileNodes != null)
                {
                    foreach (var node in profileNodes)
                    {
                        string href = node.GetAttributeValue("href", "");

                        if (string.IsNullOrWhiteSpace(href))
                            continue;

                        var profileMatch = Regex.Match(
                            href,
                            @"^/profiles/(\d+)/([^/?]+)",
                            RegexOptions.IgnoreCase
                        );

                        if (!profileMatch.Success)
                            continue;

                        string playerId = profileMatch.Groups[1].Value;
                        string slug = profileMatch.Groups[2].Value;

                        string name = node.GetAttributeValue("title", "").Trim();

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            var nameNode = node.SelectSingleNode(
                                ".//span[contains(@class,'hover:underline')]"
                            );

                            if (nameNode != null)
                            {
                                name = HtmlEntity.DeEntitize(
                                    nameNode.InnerText
                                ).Trim();
                            }
                        }

                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        var roleNode = node.SelectSingleNode(".//p");

                        string role = roleNode != null
                            ? HtmlEntity.DeEntitize(roleNode.InnerText).Trim()
                            : "";

                        var imageNode = node.SelectSingleNode(".//img");

                        string imageUrl = imageNode != null
                            ? imageNode.GetAttributeValue("src", "")
                            : "";

                        imageUrl = imageUrl.Replace("&amp;", "&");

                        players[playerId] = new
                        {
                            id = playerId,
                            name = name,
                            role = role,
                            slug = slug,
                            profileUrl = href,
                            imageUrl = imageUrl
                        };
                    }
                }

                return Ok(new
                {
                    success = true,
                    seriesId = seriesId,
                    playerCount = players.Count,
                    players = players.Values.ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    seriesId = seriesId,
                    playerCount = 0,
                    players = new List<object>(),
                    error = ex.Message
                });
            }
        }

        [HttpGet("match-facts/{matchId}")]
        public async Task<IActionResult> GetMatchFacts(int matchId)
        {
            try
            {
                // ==========================================
                // CRICBUZZ URL
                // ==========================================

                string url = matchId switch
                {
                    152731 => "https://www.cricbuzz.com/cricket-match-facts/152731/afg-vs-ind-2nd-t20i-afghanistan-vs-india-in-india-2026",
                    _ => $"https://www.cricbuzz.com/cricket-match-facts/{matchId}"
                };

                _httpClient.DefaultRequestHeaders.Clear();

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/139.0.0.0 Safari/537.36"
                );

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
                );

                string html = await _httpClient.GetStringAsync(url);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);


                // ==========================================
                // INFO
                // ==========================================

                var info = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                );

                string series = "";
                string venue = "";

                var infoRows = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class,'facts-row-grid')]"
                );

                if (infoRows != null)
                {
                    foreach (var row in infoRows)
                    {
                        var cells = row.SelectNodes("./div");

                        if (cells == null || cells.Count < 2)
                            continue;

                        string key = HtmlEntity.DeEntitize(
                            cells[0].InnerText
                        ).Trim();

                        if (string.IsNullOrWhiteSpace(key))
                            continue;

                        // Only actual INFO fields
                        bool isInfoField =
                            key.Equals("Match", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Series", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Time", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Toss", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Venue", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Umpires", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("3rd Umpire", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Referee", StringComparison.OrdinalIgnoreCase);

                        if (!isInfoField)
                            continue;

                        string value = HtmlEntity.DeEntitize(
                            cells[1].InnerText
                        ).Trim();

                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        info[key] = value;

                        if (key.Equals("Series", StringComparison.OrdinalIgnoreCase))
                            series = value;

                        if (key.Equals("Venue", StringComparison.OrdinalIgnoreCase))
                            venue = value;
                    }
                }


                // ==========================================
                // SERIES - DIRECT HTML FALLBACK
                // ==========================================

                if (string.IsNullOrWhiteSpace(series))
                {
                    var seriesNode = doc.DocumentNode.SelectSingleNode(
                        "//div[contains(@class,'facts-row-grid')][div[contains(@class,'font-bold') and normalize-space()='Series']]//a"
                    );

                    if (seriesNode != null)
                    {
                        series = HtmlEntity.DeEntitize(
                            seriesNode.InnerText
                        ).Trim();
                    }
                }


                // ==========================================
                // VENUE - DIRECT HTML FALLBACK
                // ==========================================

                if (string.IsNullOrWhiteSpace(venue))
                {
                    var venueNode = doc.DocumentNode.SelectSingleNode(
                        "//div[contains(@class,'facts-row-grid')][div[contains(@class,'font-bold') and normalize-space()='Venue']]//a"
                    );

                    if (venueNode != null)
                    {
                        venue = HtmlEntity.DeEntitize(
                            venueNode.InnerText
                        ).Trim();
                    }
                }


                // ==========================================
                // SQUADS
                // ==========================================

                var squads = new List<object>();

                var squadRows = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class,'facts-row-grid')][div[contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'squad')]]"
                );

                if (squadRows != null)
                {
                    foreach (var squadRow in squadRows)
                    {
                        // ==================================
                        // TEAM NAME
                        // ==================================

                        string teamName = "";

                        var teamNameNode = squadRow.SelectSingleNode(
                            "./div[contains(@class,'font-bold')][1]"
                        );

                        if (teamNameNode != null)
                        {
                            teamName = HtmlEntity.DeEntitize(
                                teamNameNode.InnerText
                            ).Trim();
                        }

                        teamName = Regex.Replace(
                            teamName,
                            @"\s+squad$",
                            "",
                            RegexOptions.IgnoreCase
                        ).Trim();

                        if (string.IsNullOrWhiteSpace(teamName))
                            continue;


                        // ==================================
                        // PLAYERS
                        // ==================================

                        var players = new List<object>();

                        var playersHeading = squadRow.SelectSingleNode(
                            ".//div[contains(@class,'font-bold') and normalize-space()='Players']"
                        );

                        if (playersHeading != null)
                        {
                            var playersContainer = playersHeading.ParentNode;

                            var playerLinks = playersContainer.SelectNodes(
                                ".//a[contains(@href,'/profiles/')]"
                            );

                            if (playerLinks != null)
                            {
                                var addedPlayers = new HashSet<string>(
                                    StringComparer.OrdinalIgnoreCase
                                );

                                foreach (var link in playerLinks)
                                {
                                    string href = link.GetAttributeValue(
                                        "href",
                                        ""
                                    );

                                    var playerMatch = Regex.Match(
                                        href,
                                        @"/profiles/(\d+)/([^/?]+)",
                                        RegexOptions.IgnoreCase
                                    );

                                    if (!playerMatch.Success)
                                        continue;

                                    string playerId = playerMatch.Groups[1].Value;
                                    string slug = playerMatch.Groups[2].Value;

                                    if (!addedPlayers.Add(playerId))
                                        continue;

                                    string name = link.GetAttributeValue(
                                        "title",
                                        ""
                                    );

                                    name = Regex.Replace(
                                        name,
                                        @"^View Profile Of\s+",
                                        "",
                                        RegexOptions.IgnoreCase
                                    ).Trim();

                                    if (string.IsNullOrWhiteSpace(name))
                                    {
                                        name = HtmlEntity.DeEntitize(
                                            link.InnerText
                                        ).Trim();
                                    }

                                    if (string.IsNullOrWhiteSpace(name))
                                        continue;

                                    string imageUrl =
                                        $"https://static.cricbuzz.com/a/img/v1/i1/c{playerId}/{slug}.jpg?d=low&p=gthumb";

                                    players.Add(new
                                    {
                                        id = playerId,
                                        name = name,
                                        slug = slug,
                                        profileUrl = href,
                                        imageUrl = imageUrl
                                    });
                                }
                            }
                        }


                        // ==================================
                        // BENCH
                        // ==================================

                        var bench = new List<object>();

                        var benchHeading = squadRow.SelectSingleNode(
                            ".//div[contains(@class,'font-bold') and normalize-space()='Bench']"
                        );

                        if (benchHeading != null)
                        {
                            var benchContainer = benchHeading.ParentNode;

                            var benchLinks = benchContainer.SelectNodes(
                                ".//a[contains(@href,'/profiles/')]"
                            );

                            if (benchLinks != null)
                            {
                                var addedBench = new HashSet<string>(
                                    StringComparer.OrdinalIgnoreCase
                                );

                                foreach (var link in benchLinks)
                                {
                                    string href = link.GetAttributeValue(
                                        "href",
                                        ""
                                    );

                                    var benchMatch = Regex.Match(
                                        href,
                                        @"/profiles/(\d+)/([^/?]+)",
                                        RegexOptions.IgnoreCase
                                    );

                                    if (!benchMatch.Success)
                                        continue;

                                    string playerId = benchMatch.Groups[1].Value;
                                    string slug = benchMatch.Groups[2].Value;

                                    if (!addedBench.Add(playerId))
                                        continue;

                                    string name = link.GetAttributeValue(
                                        "title",
                                        ""
                                    );

                                    name = Regex.Replace(
                                        name,
                                        @"^View Profile Of\s+",
                                        "",
                                        RegexOptions.IgnoreCase
                                    ).Trim();

                                    if (string.IsNullOrWhiteSpace(name))
                                    {
                                        name = HtmlEntity.DeEntitize(
                                            link.InnerText
                                        ).Trim();
                                    }

                                    if (string.IsNullOrWhiteSpace(name))
                                        continue;

                                    string imageUrl =
                                        $"https://static.cricbuzz.com/a/img/v1/i1/c{playerId}/{slug}.jpg?d=low&p=gthumb";

                                    bench.Add(new
                                    {
                                        id = playerId,
                                        name = name,
                                        slug = slug,
                                        profileUrl = href,
                                        imageUrl = imageUrl
                                    });
                                }
                            }
                        }


                        // ==================================
                        // SUPPORT STAFF
                        // ==================================

                        var supportStaff = new List<object>();

                        var staffHeading = squadRow.SelectSingleNode(
                            ".//div[normalize-space()='Support Staff']"
                        );

                        if (staffHeading != null)
                        {
                            var staffContainer = staffHeading.ParentNode;

                            var staffLinks = staffContainer.SelectNodes(
                                ".//a[contains(@href,'/profiles/')]"
                            );

                            if (staffLinks != null)
                            {
                                var addedStaff = new HashSet<string>(
                                    StringComparer.OrdinalIgnoreCase
                                );

                                foreach (var link in staffLinks)
                                {
                                    string href = link.GetAttributeValue(
                                        "href",
                                        ""
                                    );

                                    var staffMatch = Regex.Match(
                                        href,
                                        @"/profiles/(\d+)/([^/?]+)",
                                        RegexOptions.IgnoreCase
                                    );

                                    if (!staffMatch.Success)
                                        continue;

                                    string staffId = staffMatch.Groups[1].Value;
                                    string slug = staffMatch.Groups[2].Value;

                                    if (!addedStaff.Add(staffId))
                                        continue;

                                    string name = link.GetAttributeValue(
                                        "title",
                                        ""
                                    );

                                    name = Regex.Replace(
                                        name,
                                        @"^View Profile Of\s+",
                                        "",
                                        RegexOptions.IgnoreCase
                                    ).Trim();

                                    if (string.IsNullOrWhiteSpace(name))
                                    {
                                        name = HtmlEntity.DeEntitize(
                                            link.InnerText
                                        ).Trim();
                                    }

                                    if (string.IsNullOrWhiteSpace(name))
                                        continue;

                                    string imageUrl =
                                        $"https://static.cricbuzz.com/a/img/v1/i1/c{staffId}/{slug}.jpg?d=low&p=gthumb";

                                    supportStaff.Add(new
                                    {
                                        id = staffId,
                                        name = name,
                                        slug = slug,
                                        profileUrl = href,
                                        imageUrl = imageUrl
                                    });
                                }
                            }
                        }


                        // ==================================
                        // ADD TEAM
                        // ==================================

                        squads.Add(new
                        {
                            teamName = teamName,

                            players = players,
                            playerCount = players.Count,

                            bench = bench,
                            benchCount = bench.Count,

                            supportStaff = supportStaff,
                            staffCount = supportStaff.Count
                        });
                    }
                }


                // ==========================================
                // VENUE GUIDE
                // ==========================================

                var venueGuide = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                );

                var venueSection = doc.DocumentNode.SelectSingleNode(
                    "//div[a[@title='VENUE GUIDE']]"
                );

                if (venueSection != null)
                {
                    var rows = venueSection.SelectNodes(
                        ".//div[contains(@class,'facts-row-grid')]"
                    );

                    if (rows != null)
                    {
                        foreach (var row in rows)
                        {
                            var cells = row.SelectNodes("./div");

                            if (cells == null || cells.Count < 2)
                                continue;

                            string key = HtmlEntity.DeEntitize(
                                cells[0].InnerText
                            ).Trim();

                            string value = HtmlEntity.DeEntitize(
                                cells[1].InnerText
                            ).Trim();

                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                venueGuide[key] = value;
                            }
                        }
                    }
                }


                // ==========================================
                // BROADCAST GUIDE
                // ==========================================

                var broadcastGuide = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                );

                // Find "Broadcast Guide - IN"
                var broadcastTextNode = doc.DocumentNode
                    .Descendants()
                    .FirstOrDefault(x =>
                        HtmlEntity.DeEntitize(x.InnerText)
                            .Trim()
                            .Equals(
                                "Broadcast Guide - IN",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );

                if (broadcastTextNode != null)
                {
                    // Broadcast heading ke baad ke nodes
                    var current = broadcastTextNode;

                    while (current != null)
                    {
                        current = current.NextSibling;

                        if (current == null)
                            break;

                        string text = HtmlEntity.DeEntitize(
                            current.InnerText
                        ).Trim();

                        if (string.IsNullOrWhiteSpace(text))
                            continue;

                        text = Regex.Replace(text, @"\s+", " ").Trim();

                        // Streaming
                        if (text.Equals(
                            "Streaming",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            var valueNode = current.NextSibling;

                            while (valueNode != null)
                            {
                                string value = HtmlEntity.DeEntitize(
                                    valueNode.InnerText
                                ).Trim();

                                value = Regex.Replace(
                                    value,
                                    @"\s+",
                                    " "
                                ).Trim();

                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    if (!value.Equals(
                                        "TV",
                                        StringComparison.OrdinalIgnoreCase))
                                    {
                                        broadcastGuide["Streaming"] = value;
                                    }

                                    break;
                                }

                                valueNode = valueNode.NextSibling;
                            }
                        }

                        // TV
                        if (text.Equals(
                            "TV",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            var valueNode = current.NextSibling;

                            while (valueNode != null)
                            {
                                string value = HtmlEntity.DeEntitize(
                                    valueNode.InnerText
                                ).Trim();

                                value = Regex.Replace(
                                    value,
                                    @"\s+",
                                    " "
                                ).Trim();

                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    broadcastGuide["TV"] = value;
                                    break;
                                }

                                valueNode = valueNode.NextSibling;
                            }
                        }

                        // Dono mil gaye to stop
                        if (
                            broadcastGuide.ContainsKey("Streaming") &&
                            broadcastGuide.ContainsKey("TV")
                        )
                        {
                            break;
                        }
                    }
                }
                // ==========================================
                // FINAL RESPONSE
                // ==========================================

                return Ok(new
                {
                    success = true,
                    matchId = matchId,

                    series = series,
                    venue = venue,

                    info = info,

                    squads = squads,

                    venueGuide = venueGuide,

                    broadcastGuide = broadcastGuide
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    matchId = matchId,
                    error = ex.Message
                });
            }
        }
    }
}