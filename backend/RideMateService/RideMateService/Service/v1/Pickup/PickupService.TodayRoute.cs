using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Map;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string DriveRouteSourceRoute = "ROUTE";
    private const string DriveRouteSourceEstimate = "ESTIMATE";
    private const string LegCurrentName = "현재 위치";
    private const string LegHomeName = "집";

    /// <summary>
    /// 오늘의 픽업 경로 조회
    /// </summary>
    public async Task<ResponseModel<DetailTodayPickupRouteDto>> DetailTodayPickupRouteService(DetailTodayPickupRouteReqDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<DetailTodayPickupRouteDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — API 모델 검증을 거치지 않은 호출도 필수값·경유지 개수·경유지 좌표/주소 조합은 거절한다(범위·길이는 모델 검증)
            var requestWaypoints = dto?.waypoints ?? new List<TodayPickupWaypointReqDto>();
            if (dto is not { scheduleSeq: >= 1, dayOfWeek: >= 1 and <= 7, currentLat: not null, currentLng: not null }
                || requestWaypoints.Count > DetailTodayPickupRouteReqDto.MaxWaypoints
                || !requestWaypoints.All(DetailTodayPickupRouteReqDto.IsValidWaypoint))
                return new ResponseModel<DetailTodayPickupRouteDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var home = await _pickupRepository.GetUserHomeAsync(userId, cancellationToken).ConfigureAwait(false);
            if (home is null) // 계정이 없거나 지워졌다
                return new ResponseModel<DetailTodayPickupRouteDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 3) 오늘 그 시간표의 픽업 — 날짜·요일은 서버 현재 시각이다. 오늘 요일이 아니거나 오늘 픽업하지 않으면 101
            var today = DateOnly.FromDateTime(DateTime.Now);
            var todayDayOfWeek = DepartureGuideCalculator.DayOfWeek(today);
            if (dto.dayOfWeek != todayDayOfWeek)
                return new ResponseModel<DetailTodayPickupRouteDto> { message = "요청이 정상 처리되었습니다.", data = null, code = 101 };

            var row = await _pickupRepository.GetTodayPickupRouteAsync(userId, dto.scheduleSeq!.Value, today,
                todayDayOfWeek, cancellationToken).ConfigureAwait(false);
            if (row is null)
                return new ResponseModel<DetailTodayPickupRouteDto> { message = "요청이 정상 처리되었습니다.", data = null, code = 101 };

            // 4) 경유지 좌표 — 좌표를 보냈으면 그대로, 주소만 보냈으면 카카오 주소 검색으로 찾는다.
            //    주소 검색은 동시에 부른다(카카오 장애 때 타임아웃이 경유지 수만큼 쌓이지 않게).
            //    주소를 찾지 못하면(결과 없음) 순번이 가장 작은 것으로 400, 카카오 장애·키 없음이면 좌표 없이 두고 경로를 계산하지 않는다
            var searches = requestWaypoints
                .Select(w => w is { lat: not null, lng: not null }
                    ? Task.FromResult<AddressSearchResult?>(null)
                    : SearchWaypointAsync(w.address!.Trim(), cancellationToken))
                .ToList();
            var found = await Task.WhenAll(searches).ConfigureAwait(false);

            var waypoints = new List<TodayPickupWaypointDto>(requestWaypoints.Count);
            for (var i = 0; i < requestWaypoints.Count; i++)
            {
                var request = requestWaypoints[i];
                var address = string.IsNullOrWhiteSpace(request.address) ? null : request.address.Trim();
                var name = string.IsNullOrWhiteSpace(request.name) ? address ?? $"경유지 {i + 1}" : request.name.Trim();
                if (found[i] is null)
                {
                    waypoints.Add(new TodayPickupWaypointDto { name = name, address = address, lat = request.lat, lng = request.lng });
                    continue;
                }

                if (found[i]!.Status == AddressSearchStatus.NotFound)
                    return new ResponseModel<DetailTodayPickupRouteDto>
                    {
                        message = $"{i + 1}번째 경유지 주소를 찾을 수 없습니다.",
                        data = null,
                        code = 400
                    };

                waypoints.Add(new TodayPickupWaypointDto { name = name, address = address, lat = found[i]!.Lat, lng = found[i]!.Lng });
            }

            var result = new DetailTodayPickupRouteDto
            {
                childName = row.ChildName,
                displayName = row.DisplayName,
                endTime = row.EndTime.ToString(@"hh\:mm"),
                areaName = row.AreaName,
                academy = new TodayPickupRouteAcademyDto
                {
                    academyName = row.AcademyName,
                    address = row.AcademyAddress,
                    lat = row.AcademyLat,
                    lng = row.AcademyLng
                },
                home = string.IsNullOrEmpty(home.DestAddress)
                    ? null
                    : new TodayPickupRouteHomeDto { address = home.DestAddress, lat = home.DestLat, lng = home.DestLng },
                waypoints = waypoints
            };

            // 5) 현재 위치 → 학원 → 경유지 → 집 차량 경로 — 카카오모빌리티 자동차 길찾기(지금 출발) 한 번으로 구간별 시간을 받는다.
            //    학원은 카카오의 첫 경유지가 된다. 실패하면 구간마다 직선 거리로 추정한다. 학원·집·경유지 좌표 중 하나라도 없으면 계산하지 않는다
            if (row is { AcademyLat: { } academyLat, AcademyLng: { } academyLng }
                && result.home is { lat: { } homeLat, lng: { } homeLng }
                && waypoints.All(w => w.lat is not null && w.lng is not null))
            {
                // 점 순서: 현재 위치 → 학원 → 경유지들 → 집. 구간의 도착 경유지 순번은 사용자 경유지 기준(학원·집은 null)
                var points = new List<(decimal Lat, decimal Lng)> { (dto.currentLat!.Value, dto.currentLng!.Value), (academyLat, academyLng) };
                points.AddRange(waypoints.Select(w => (w.lat!.Value, w.lng!.Value)));
                points.Add((homeLat, homeLng));

                var pointNames = new List<string> { LegCurrentName, row.AcademyName };
                pointNames.AddRange(waypoints.Select(w => w.name));
                pointNames.Add(LegHomeName);

                var toWaypointIndexes = new List<int?> { null };
                toWaypointIndexes.AddRange(Enumerable.Range(0, waypoints.Count).Select(i => (int?)i));
                toWaypointIndexes.Add(null);

                result.driveRoute = await DriveRouteAsync(points, pointNames, toWaypointIndexes, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new ResponseModel<DetailTodayPickupRouteDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = result,
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<DetailTodayPickupRouteDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private async Task<AddressSearchResult?> SearchWaypointAsync(string address, CancellationToken cancellationToken)
        => await _addressClient.SearchAddressAsync(address, cancellationToken).ConfigureAwait(false);

    // 결과를 저장하지 않는다(카카오 길찾기 결과 캐싱 금지). points는 출발지·중간 지점들·도착지 순서이고 구간은 points 수 − 1개다.
    // 중간 지점은 카카오 경유지로 보낸다. 실패하면 구간마다 직선 거리 × 도로 보정 ÷ 차량 추정 속도를 올림한 분이다.
    // 전체 시간은 구간 시간의 합이다(화면의 구간 시간을 더한 값과 맞춘다). pointNames는 points와, toWaypointIndexes는 구간과 같은 순서다
    private async Task<TodayPickupDriveRouteDto> DriveRouteAsync(IReadOnlyList<(decimal Lat, decimal Lng)> points,
        IReadOnlyList<string> pointNames, IReadOnlyList<int?> toWaypointIndexes, CancellationToken cancellationToken)
    {
        var route = await _driveRouteClient.GetDriveRouteAsync(points[0], points.Skip(1).Take(points.Count - 2).ToList(),
            points[^1], cancellationToken).ConfigureAwait(false);

        var legs = Enumerable.Range(0, points.Count - 1)
            .Select(i => new TodayPickupDriveLegDto
            {
                from = pointNames[i],
                to = pointNames[i + 1],
                toWaypointIndex = toWaypointIndexes[i],
                driveMin = route is not null
                    ? DepartureGuideCalculator.SecondsToMinutesCeil(route.Legs[i].Seconds)
                    : DepartureGuideCalculator.EstimateMinutes(
                        GeoDistance.Meters(points[i].Lat, points[i].Lng, points[i + 1].Lat, points[i + 1].Lng),
                        _options.DriveKmh, _options.RoadFactor),
                distanceM = route?.Legs[i].DistanceM
            })
            .ToList();

        return new TodayPickupDriveRouteDto
        {
            driveMin = legs.Sum(l => l.driveMin),
            distanceM = route?.DistanceM,
            source = route is not null ? DriveRouteSourceRoute : DriveRouteSourceEstimate,
            legs = legs
        };
    }
}
