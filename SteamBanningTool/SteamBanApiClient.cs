using System.Net.Http;
using System.Text.Json;

namespace SteamBanningTool.App
{
    public static class SteamBanApiClient
    {
        private static readonly HttpClient httpClient = new();

        private const string ReportUrl = "https://partner.steam-api.com/ICheatReportingService/ReportPlayerCheating/v1/";
        private const string BanUrl = "https://partner.steam-api.com/ICheatReportingService/RequestPlayerGameBan/v1/";
        private const string UnbanUrl = "https://partner.steam-api.com/ICheatReportingService/RemovePlayerGameBan/v1/";
        private const string GetCheatingReportsUrl = "https://partner.steam-api.com/ICheatReportingService/GetCheatingReports/v1/";

        public static Task<(bool Success, string ResponseText, string? ErrorMessage)> ReportPlayerAsync(string partnerKey, ulong appId, string steamId64)
        {
            return SendFormRequestAsync(ReportUrl, new Dictionary<string, string>
            {
                { "key", partnerKey },
                { "steamid", steamId64 },
                { "appid", appId.ToString() }
            });
        }

        public static Task<(bool Success, string ResponseText, string? ErrorMessage)> BanPlayerAsync(string partnerKey, ulong appId, string steamId64, string reportId, string cheatDescription, string duration)
        {
            return SendFormRequestAsync(BanUrl, new Dictionary<string, string>
            {
                { "key", partnerKey },
                { "steamid", steamId64 },
                { "appid", appId.ToString() },
                { "reportid", reportId },
                { "cheatdescription", cheatDescription },
                { "duration", duration },
                { "delayban", "false" }
            });
        }

        public static Task<(bool Success, string ResponseText, string? ErrorMessage)> UnbanPlayerAsync(string partnerKey, ulong appId, string steamId64)
        {
            return SendFormRequestAsync(UnbanUrl, new Dictionary<string, string>
            {
                { "key", partnerKey },
                { "steamid", steamId64 },
                { "appid", appId.ToString() }
            });
        }

        public static Task<(bool Success, string ResponseText, string? ErrorMessage)> GetCheatingReportsAsync(
            string partnerKey,
            ulong appId,
            uint timeBegin,
            uint timeEnd,
            ulong reportIdMin,
            bool includeReports,
            bool includeBans,
            string? steamId64)
        {
            var parameters = new Dictionary<string, string>
            {
                { "key", partnerKey },
                { "appid", appId.ToString() },
                { "timebegin", timeBegin.ToString() },
                { "timeend", timeEnd.ToString() },
                { "reportidmin", reportIdMin.ToString() },
                { "includereports", includeReports.ToString().ToLowerInvariant() },
                { "includebans", includeBans.ToString().ToLowerInvariant() }
            };

            if(!string.IsNullOrWhiteSpace(steamId64))
            {
                parameters.Add("steamid", steamId64);
            }

            return SendGetRequestAsync(GetCheatingReportsUrl, parameters);
        }

        public static string? TryGetJsonValue(string json, params string[] path)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement current = document.RootElement;

                foreach(string segment in path)
                {
                    if(!current.TryGetProperty(segment, out JsonElement next))
                    {
                        return null;
                    }

                    current = next;
                }

                return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string FormatJson(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json;
            }
        }

        public static string FormatCheatingReportsTable(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                List<JsonElement> items = ExtractReportItems(document.RootElement);

                if(items.Count == 0)
                {
                    return "No results found.";
                }

                var rows = new List<(string Index, string ReportId, string SteamId, string AppId, string Summary, string Extra)>();

                for(int i = 0; i < items.Count; i++)
                {
                    JsonElement item = items[i];
                    rows.Add((
                        (i + 1).ToString(),
                        GetStringValue(item, "reportid", "report_id", "id") ?? "-",
                        GetStringValue(item, "steamid") ?? "-",
                        GetStringValue(item, "appid") ?? "-",
                        BuildSummary(item),
                        BuildExtraDetails(item)
                    ));
                }

                int indexWidth = Math.Max(1, rows.Max(row => row.Index.Length));
                int reportIdWidth = Math.Max("ReportID".Length, rows.Max(row => row.ReportId.Length));
                int steamIdWidth = Math.Max("SteamID64".Length, rows.Max(row => row.SteamId.Length));
                int appIdWidth = Math.Max("AppID".Length, rows.Max(row => row.AppId.Length));
                int summaryWidth = Math.Max("Summary".Length, Math.Min(40, rows.Max(row => row.Summary.Length)));
                int extraWidth = Math.Max("Details".Length, Math.Min(60, rows.Max(row => row.Extra.Length)));

                var builder = new System.Text.StringBuilder();
                builder.AppendLine(BuildHeader(indexWidth, reportIdWidth, steamIdWidth, appIdWidth, summaryWidth, extraWidth));
                builder.AppendLine(BuildSeparator(indexWidth, reportIdWidth, steamIdWidth, appIdWidth, summaryWidth, extraWidth));

                foreach(var row in rows)
                {
                    builder.AppendLine(
                        $"{Pad(row.Index, indexWidth)} | {Pad(row.ReportId, reportIdWidth)} | {Pad(row.SteamId, steamIdWidth)} | {Pad(row.AppId, appIdWidth)} | {Pad(TrimTo(row.Summary, summaryWidth), summaryWidth)} | {Pad(TrimTo(row.Extra, extraWidth), extraWidth)}"
                    );
                }

                return builder.ToString();
            }
            catch
            {
                return json;
            }
        }

        private static async Task<(bool Success, string ResponseText, string? ErrorMessage)> SendFormRequestAsync(string url, Dictionary<string, string> parameters)
        {
            try
            {
                using var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(parameters));
                string responseText = await response.Content.ReadAsStringAsync();

                return (
                    response.IsSuccessStatusCode,
                    responseText,
                    response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})"
                );
            }
            catch(Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        private static async Task<(bool Success, string ResponseText, string? ErrorMessage)> SendGetRequestAsync(string url, Dictionary<string, string> parameters)
        {
            try
            {
                string query = await new FormUrlEncodedContent(parameters).ReadAsStringAsync();
                using var response = await httpClient.GetAsync(url + "?" + query);
                string responseText = await response.Content.ReadAsStringAsync();

                return (
                    response.IsSuccessStatusCode,
                    responseText,
                    response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})"
                );
            }
            catch(Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        private static List<JsonElement> ExtractReportItems(JsonElement root)
        {
            if(root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray().ToList();
            }

            if(root.ValueKind == JsonValueKind.Object)
            {
                if(root.TryGetProperty("response", out JsonElement response) && response.ValueKind == JsonValueKind.Object)
                {
                    var arrayCandidates = new[] { "reports", "bans", "items", "cheatingreports", "results" };
                    foreach(string candidate in arrayCandidates)
                    {
                        if(response.TryGetProperty(candidate, out JsonElement arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
                        {
                            return arrayElement.EnumerateArray().ToList();
                        }
                    }

                    return new List<JsonElement> { response };
                }
            }

            return new List<JsonElement> { root };
        }

        private static string? GetStringValue(JsonElement element, params string[] names)
        {
            foreach(string name in names)
            {
                if(element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
                {
                    return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                }
            }

            return null;
        }

        private static string BuildSummary(JsonElement element)
        {
            var summaryParts = new List<string>();

            string? cheatDescription = GetStringValue(element, "cheatdescription", "description");
            if(!string.IsNullOrWhiteSpace(cheatDescription))
            {
                summaryParts.Add(cheatDescription);
            }

            string? duration = GetStringValue(element, "duration");
            if(!string.IsNullOrWhiteSpace(duration))
            {
                summaryParts.Add($"Duration: {duration}");
            }

            string? delayedBan = GetStringValue(element, "delayban", "delayedban");
            if(!string.IsNullOrWhiteSpace(delayedBan))
            {
                summaryParts.Add($"DelayBan: {delayedBan}");
            }

            return summaryParts.Count == 0 ? "-" : string.Join(" | ", summaryParts);
        }

        private static string BuildExtraDetails(JsonElement element)
        {
            var extraParts = new List<string>();

            string? gm = GetStringValue(element, "gamemode");
            if(!string.IsNullOrWhiteSpace(gm)) extraParts.Add($"GM: {gm}");

            string? sev = GetStringValue(element, "severity");
            if(!string.IsNullOrWhiteSpace(sev)) extraParts.Add($"Sev: {sev}");

            string? heuristic = GetStringValue(element, "heuristic");
            if(!string.IsNullOrWhiteSpace(heuristic)) extraParts.Add($"Heur: {FormatBoolSpanish(heuristic)}");

            string? detection = GetStringValue(element, "detection");
            if(!string.IsNullOrWhiteSpace(detection)) extraParts.Add($"Detec: {FormatBoolSpanish(detection)}");

            string? playerreport = GetStringValue(element, "playerreport");
            if(!string.IsNullOrWhiteSpace(playerreport)) extraParts.Add($"PlayerReported: {FormatBoolSpanish(playerreport)}");

            string? noreportid = GetStringValue(element, "noreportid");
            if(!string.IsNullOrWhiteSpace(noreportid)) extraParts.Add($"NoReportId: {noreportid}");

            // Timestamps: try to convert unix seconds/milliseconds into readable date
            string? suspicion = GetStringValue(element, "suspicionstarttime");
            if(!string.IsNullOrWhiteSpace(suspicion)) extraParts.Add($"Since: {TryFormatUnixTimestamp(suspicion) ?? suspicion}");

            string? time = GetStringValue(element, "time");
            if(!string.IsNullOrWhiteSpace(time)) extraParts.Add($"Time: {TryFormatUnixTimestamp(time) ?? time}");

            // appdata can be JSON or a string - try to compact and trim
            string? appdata = GetStringValue(element, "appdata");
            if(!string.IsNullOrWhiteSpace(appdata))
            {
                string compact = appdata;
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(appdata);
                    compact = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
                }
                catch { }

                extraParts.Add($"appdata: {TrimTo(compact, 80)}");
            }

            return extraParts.Count == 0 ? "-" : string.Join(" | ", extraParts);
        }

        private static string FormatBoolSpanish(string value)
        {
            if(bool.TryParse(value, out bool b)) return b ? "Yes" : "No";
            // sometimes API returns 0/1
            if(int.TryParse(value, out int i)) return i != 0 ? "Yes" : "No";
            return value;
        }

        private static string? TryFormatUnixTimestamp(string raw)
        {
            if(long.TryParse(raw, out long v))
            {
                try
                {
                    // heurística: si el valor parece ser milisegundos (> 1e11), usar ms
                    DateTimeOffset dto = v > 100000000000 ? DateTimeOffset.FromUnixTimeMilliseconds(v) : DateTimeOffset.FromUnixTimeSeconds(v);
                    return dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch { }
            }

            return null;
        }

        private static string BuildHeader(int indexWidth, int reportIdWidth, int steamIdWidth, int appIdWidth, int summaryWidth, int extraWidth)
        {
            return $"{Pad("#", indexWidth)} | {Pad("ReportID", reportIdWidth)} | {Pad("SteamID64", steamIdWidth)} | {Pad("AppID", appIdWidth)} | {Pad("Summary", summaryWidth)} | {Pad("Details", extraWidth)}";
        }

        private static string BuildSeparator(int indexWidth, int reportIdWidth, int steamIdWidth, int appIdWidth, int summaryWidth, int extraWidth)
        {
            return $"{new string('-', indexWidth)}-+-{new string('-', reportIdWidth)}-+-{new string('-', steamIdWidth)}-+-{new string('-', appIdWidth)}-+-{new string('-', summaryWidth)}-+-{new string('-', extraWidth)}";
        }

        private static string Pad(string value, int width)
        {
            return value.PadRight(width);
        }

        private static string TrimTo(string value, int maxWidth)
        {
            if(value.Length <= maxWidth)
            {
                return value;
            }

            return maxWidth <= 1 ? value[..1] : value[..(maxWidth - 1)] + "…";
        }
    }
}