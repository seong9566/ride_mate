using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RideMateService.Comm.Logger;

namespace RideMateService.Comm.Map;

/// <summary>
/// 카카오 로컬 좌표→주소 변환 API(GET /v2/local/geo/coord2address.json)로 장소 이름을 조회하는 구현
/// </summary>
public class KakaoAddressClient : IAddressClient
{
    private const int NameMaxLength = 100; // PICKUP_PLAN.CUSTOM_NAME VARCHAR(100)

    private readonly HttpClient _httpClient;
    private readonly KakaoOptions _options;
    private readonly ILogService _logService;

    public KakaoAddressClient(HttpClient httpClient, IOptions<KakaoOptions> options, ILogService logService)
    {
        this._httpClient = httpClient;
        this._options = options.Value;
        this._logService = logService;
    }

    /// <summary>
    /// 좌표의 장소 이름을 조회한다. 실패 사유는 로그에 남기되 좌표·주소·키·URL·응답 본문은 남기지 않는다.
    /// </summary>
    public async Task<string?> GetPlaceNameAsync(decimal lat, decimal lng, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.RestApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        try
        {
            // 카카오는 x=경도, y=위도 순서다. input_coord 기본값이 WGS84다
            var uri = FormattableString.Invariant($"v2/local/geo/coord2address.json?x={lng}&y={lat}");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Authorization", $"KakaoAK {apiKey}");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logService.LogMessage($"Kakao coord2address failed: HTTP {(int)response.StatusCode}");
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
            _logService.LogMessage("Kakao coord2address failed: TIMEOUT");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logService.LogMessage($"Kakao coord2address failed: {ex.HttpRequestError}");
            return null;
        }
        catch (Exception ex)
        {
            // 응답 해석 실패 등. 메시지에 응답 내용이 섞일 수 있어 예외 형식만 남긴다
            _logService.LogMessage($"Kakao coord2address failed: {ex.GetType().Name}");
            return null;
        }
    }

    // documents[0]에서 도로명 주소의 건물 이름 → 도로명 주소 → 지번 주소 순으로 고른다. 결과가 없으면 null
    private string? Parse(string body)
    {
        var root = JObject.Parse(body);

        var document = (root["documents"] as JArray)?.FirstOrDefault();
        var roadAddress = document?["road_address"] as JObject;
        var address = document?["address"] as JObject;

        var name = FirstText(roadAddress?["building_name"]?.Value<string>(),
            roadAddress?["address_name"]?.Value<string>(),
            address?["address_name"]?.Value<string>());
        if (name is null)
        {
            _logService.LogMessage("Kakao coord2address failed: NO_RESULT");
            return null;
        }

        return name.Length > NameMaxLength ? name[..NameMaxLength] : name;
    }

    private static string? FirstText(params string?[] values)
        => values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
