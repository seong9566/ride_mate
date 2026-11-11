using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RideMateService.Comm.Logger;

namespace RideMateService.Comm.Map;

/// <summary>
/// 카카오맵 도보 경로 API(GET /v2/routing/walk)로 도보 경로를 조회하는 구현
/// </summary>
public class KakaoWalkRouteClient : IWalkRouteClient
{
    private readonly HttpClient _httpClient;
    private readonly KakaoOptions _options;
    private readonly ILogService _logService;

    public KakaoWalkRouteClient(HttpClient httpClient, IOptions<KakaoOptions> options, ILogService logService)
    {
        this._httpClient = httpClient;
        this._options = options.Value;
        this._logService = logService;
    }

    /// <summary>
    /// 큰길 우선(BROAD_FIRST)으로 도보 경로를 조회한다. 실패 사유는 로그에 남기되 좌표·키·URL은 남기지 않는다.
    /// </summary>
    public async Task<WalkRouteResult?> GetWalkRouteAsync(decimal startLat, decimal startLng, decimal endLat, decimal endLng, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.RestApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        try
        {
            // 카카오는 x=경도, y=위도 순서다
            var uri = FormattableString.Invariant(
                $"v2/routing/walk?start_x={startLng}&start_y={startLat}&end_x={endLng}&end_y={endLat}&route_mode=BROAD_FIRST");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Authorization", $"KakaoAK {apiKey}");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logService.LogMessage($"Kakao walk route failed: HTTP {(int)response.StatusCode}");
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
            _logService.LogMessage("Kakao walk route failed: timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logService.LogMessage($"Kakao walk route failed: {ex.HttpRequestError}");
            return null;
        }
        catch (Exception ex)
        {
            // 응답 해석 실패 등. 메시지에 응답 내용이 섞일 수 있어 예외 형식만 남긴다
            _logService.LogMessage($"Kakao walk route failed: {ex.GetType().Name}");
            return null;
        }
    }

    // status가 OK가 아니면 null. 응답 모양이 다르면 예외를 던지고, 호출한 쪽이 null로 바꾼다
    private WalkRouteResult? Parse(string body)
    {
        var root = JObject.Parse(body);

        var status = root["status"]?.Value<string>();
        if (status != "OK")
        {
            _logService.LogMessage($"Kakao walk route failed: status {status}");
            return null;
        }

        var route = root["route"] ?? throw new FormatException("route");
        var totalSeconds = route["properties"]?["totalTime"]?.Value<int>() ?? throw new FormatException("totalTime");

        var path = new List<(decimal Lat, decimal Lng)>();
        foreach (var leg in route["legs"] as JArray ?? new JArray())
        {
            foreach (var step in leg["steps"] as JArray ?? new JArray())
            {
                foreach (var point in step["path"]?["points"] as JArray ?? new JArray())
                {
                    // 점은 [x=경도, y=위도]
                    if (point is not JArray { Count: >= 2 } xy)
                        throw new FormatException("point");

                    var item = (Lat: xy[1].Value<decimal>(), Lng: xy[0].Value<decimal>());
                    if (path.Count > 0 && path[^1] == item)
                        continue;

                    path.Add(item);
                }
            }
        }

        return new WalkRouteResult { TotalSeconds = totalSeconds, Path = path };
    }
}
