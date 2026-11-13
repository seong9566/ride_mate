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
    public async Task<DriveRouteResult?> GetDriveRouteAsync(decimal startLat, decimal startLng, decimal endLat,
        decimal endLng, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.RestApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        try
        {
            // 카카오는 x=경도, y=위도 순서다
            var uri = FormattableString.Invariant(
                $"v1/directions?origin={startLng},{startLat}&destination={endLng},{endLat}&priority=RECOMMEND");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Authorization", $"KakaoAK {apiKey}");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logService.LogMessage($"Kakao drive route failed: HTTP {(int)response.StatusCode}");
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return Parse(body);
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

    // 첫 경로의 result_code가 0이 아니면 null. 응답 모양이 다르면 예외를 던지고, 호출한 쪽이 null로 바꾼다
    private DriveRouteResult? Parse(string body)
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
        var totalSeconds = summary["duration"]?.Value<int>() ?? throw new FormatException("duration");
        var distanceM = summary["distance"]?.Value<int>() ?? throw new FormatException("distance");

        var path = new List<(decimal Lat, decimal Lng)>();
        foreach (var section in route["sections"] as JArray ?? new JArray())
        {
            foreach (var road in section["roads"] as JArray ?? new JArray())
            {
                // vertexes는 [x1, y1, x2, y2, ...] 한 줄 배열이다(x=경도, y=위도)
                if (road["vertexes"] is not JArray vertexes || vertexes.Count % 2 != 0)
                    throw new FormatException("vertexes");

                for (var i = 0; i < vertexes.Count; i += 2)
                {
                    var item = (Lat: vertexes[i + 1].Value<decimal>(), Lng: vertexes[i].Value<decimal>());
                    if (path.Count > 0 && path[^1] == item)
                        continue;

                    path.Add(item);
                }
            }
        }

        return new DriveRouteResult { TotalSeconds = totalSeconds, DistanceM = distanceM, Path = path };
    }
}
