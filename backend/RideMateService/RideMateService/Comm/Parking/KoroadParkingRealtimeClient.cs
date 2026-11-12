using System.Globalization;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RideMateService.Comm.Logger;
using RideMateService.Service.v1.Pickup;

namespace RideMateService.Comm.Parking;

/// <summary>
/// 도로교통공단 주차장 실시간 API로 빈자리 수를 조회하는 구현
/// </summary>
public class KoroadParkingRealtimeClient : IParkingRealtimeClient
{
    private readonly HttpClient _httpClient;
    private readonly PublicDataOptions _publicDataOptions;
    private readonly WaitingPlaceOptions _waitingPlaceOptions;
    private readonly ILogService _logService;

    public KoroadParkingRealtimeClient(HttpClient httpClient,
        IOptions<PublicDataOptions> publicDataOptions,
        IOptions<WaitingPlaceOptions> waitingPlaceOptions,
        ILogService logService)
    {
        this._httpClient = httpClient;
        this._publicDataOptions = publicDataOptions.Value;
        this._waitingPlaceOptions = waitingPlaceOptions.Value;
        this._logService = logService;
    }

    /// <summary>
    /// 설정된 시군구별 빈자리 수를 받아 요청한 실시간 주차장 코드만 돌려준다
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>?> GetAvailableAsync(
        IReadOnlyCollection<string> realtimeRefs, CancellationToken cancellationToken = default)
    {
        var serviceKey = _publicDataOptions.ServiceKey;
        if (string.IsNullOrWhiteSpace(serviceKey))
            return null;

        if (realtimeRefs.Count == 0)
            return new Dictionary<string, int>();

        try
        {
            var encodedKey = Uri.EscapeDataString(Uri.UnescapeDataString(serviceKey.Trim()));
            var requested = realtimeRefs.ToHashSet(StringComparer.Ordinal);
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<ParkingRealtimeRegionOptions> regions = _waitingPlaceOptions.RealtimeRegions.Count > 0
                ? _waitingPlaceOptions.RealtimeRegions
                : [new ParkingRealtimeRegionOptions { SidoCd = "11", SigunguCd = "11680" }];

            foreach (var region in regions)
            {
                var uri = $"PrkRealtimeInfo_v1?serviceKey={encodedKey}&numOfRows=1000&pageNo=1&format=2&sidoCd={Uri.EscapeDataString(region.SidoCd)}&sigunguCd={Uri.EscapeDataString(region.SigunguCd)}";
                using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logService.LogMessage($"Parking realtime failed: HTTP {(int)response.StatusCode}");
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!TryMerge(body, requested, result))
                    return null;
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logService.LogMessage("Parking realtime failed: timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logService.LogMessage($"Parking realtime failed: {ex.HttpRequestError}");
            return null;
        }
        catch (Exception ex)
        {
            _logService.LogMessage($"Parking realtime failed: {ex.GetType().Name}");
            return null;
        }
    }

    private bool TryMerge(string body, HashSet<string> requested, Dictionary<string, int> result)
    {
        var root = JObject.Parse(body);
        var resultCode = root["resultCode"]?.Value<string>();
        if (resultCode != "0")
        {
            _logService.LogMessage($"Parking realtime failed: result {resultCode}");
            return false;
        }

        foreach (var item in root["PrkRealtimeInfo"] as JArray ?? new JArray())
        {
            var realtimeRef = item["prk_center_id"]?.Value<string>();
            var availableText = item["pkfc_Available_ParkingLots_total"]?.Value<string>();
            if (realtimeRef is null || !requested.Contains(realtimeRef)
                || !int.TryParse(availableText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var available))
                continue;

            result[realtimeRef] = available;
        }

        return true;
    }
}
