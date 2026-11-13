using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RideMateService.Comm.Logger;

namespace RideMateService.Comm.Map;

/// <summary>
/// 카카오모빌리티 자동차 길찾기 API(GET /v1/directions)로 자동차 경로를 조회하는 구현
/// </summary>
public class KakaoDriveRouteClient : IDriveRouteClient
{
    private readonly HttpClient _httpClient;
    private readonly KakaoOptions _options;
    private readonly ILogService _logService;

    public KakaoDriveRouteClient(HttpClient httpClient, IOptions<KakaoOptions> options, ILogService logService)
    {
        this._httpClient = httpClient;
        this._options = options.Value;
        this._logService = logService;
    }

    /// <summary>
    /// 추천 경로(RECOMMEND)로 자동차 경로를 조회한다. 실패 사유는 로그에 남기되 좌표·키·URL은 남기지 않는다.
    /// </summary>
    public async Task<DriveRouteResult?> GetDriveRouteAsync((decimal Lat, decimal Lng) start,
        IReadOnlyList<(decimal Lat, decimal Lng)> waypoints, (decimal Lat, decimal Lng) end,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.RestApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        try
        {
            // 카카오는 x=경도, y=위도 순서다. 경유지는 '|'로 잇는다. 경로 선은 쓰지 않아 요약(summary=true)만 받는다
            var uri = FormattableString.Invariant(
                $"v1/directions?origin={start.Lng},{start.Lat}&destination={end.Lng},{end.Lat}&priority=RECOMMEND&summary=true");
            if (waypoints.Count > 0)
                uri += "&waypoints=" + Uri.EscapeDataString(string.Join("|",
                    waypoints.Select(w => FormattableString.Invariant($"{w.Lng},{w.Lat}"))));

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Authorization", $"KakaoAK {apiKey}");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logService.LogMessage($"Kakao drive route failed: HTTP {(int)response.StatusCode}");
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Parse(body, waypoints.Count + 1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // HttpClient.Timeout 초과
            _logService.LogMessage("Kakao drive route failed: timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logService.LogMessage($"Kakao drive route failed: {ex.HttpRequestError}");
            return null;
        }
        catch (Exception ex)
        {
            // 응답 해석 실패 등. 메시지에 응답 내용이 섞일 수 있어 예외 형식만 남긴다
            _logService.LogMessage($"Kakao drive route failed: {ex.GetType().Name}");
            return null;
        }
    }

    // 첫 경로의 result_code가 0이 아니면 null. 구간 수가 경유지 수 + 1과 다르거나 응답 모양이 다르면 예외를 던지고, 호출한 쪽이 null로 바꾼다
    private DriveRouteResult? Parse(string body, int expectedLegs)
    {
        var root = JObject.Parse(body);
        var route = (root["routes"] as JArray)?.FirstOrDefault() ?? throw new FormatException("routes");

        var resultCode = route["result_code"]?.Value<int>() ?? throw new FormatException("result_code");
        if (resultCode != 0)
        {
            _logService.LogMessage($"Kakao drive route failed: result_code {resultCode}");
            return null;
        }

        var summary = route["summary"] ?? throw new FormatException("summary");
        var distanceM = summary["distance"]?.Value<int>() ?? throw new FormatException("distance");

        var sections = route["sections"] as JArray ?? throw new FormatException("sections");
        if (sections.Count != expectedLegs)
            throw new FormatException("sections count");

        var legs = sections
            .Select(section => new DriveRouteLeg
            {
                Seconds = section["duration"]?.Value<int>() ?? throw new FormatException("section duration"),
                DistanceM = section["distance"]?.Value<int>() ?? throw new FormatException("section distance")
            })
            .ToList();

        return new DriveRouteResult { DistanceM = distanceM, Legs = legs };
    }
}
