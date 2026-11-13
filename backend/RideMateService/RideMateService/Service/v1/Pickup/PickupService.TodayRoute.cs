using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Map;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string HomeDriveSourceRoute = "ROUTE";
    private const string HomeDriveSourceEstimate = "ESTIMATE";

    /// <summary>
    /// 오늘의 픽업 경로 조회
    /// </summary>
    public async Task<ResponseModel<DetailTodayPickupRouteDto>> DetailTodayPickupRouteService(TodayPickupRouteQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<DetailTodayPickupRouteDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — API 모델 검증을 거치지 않은 호출도 같은 규칙으로 거절한다
            if (dto is not { scheduleSeq: >= 1, dayOfWeek: >= 1 and <= 7 })
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
                    : new TodayPickupRouteHomeDto { address = home.DestAddress, lat = home.DestLat, lng = home.DestLng }
            };

            // 4) 학원 → 집 차량 경로 — 카카오모빌리티 자동차 길찾기(지금 출발). 실패하면 직선 거리로 추정한다.
            //    학원이나 집 좌표가 없으면 계산하지 않는다
            if (row is { AcademyLat: { } academyLat, AcademyLng: { } academyLng }
                && result.home is { lat: { } homeLat, lng: { } homeLng })
            {
                result.homeDrive = await HomeDriveAsync(academyLat, academyLng, homeLat, homeLng, cancellationToken)
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

    // 결과를 저장하지 않는다(카카오 길찾기 결과 캐싱 금지). 실패하면 직선 거리 × 도로 보정 ÷ 차량 추정 속도를 올림한 분이다
    private async Task<TodayPickupHomeDriveDto> HomeDriveAsync(decimal startLat, decimal startLng, decimal endLat,
        decimal endLng, CancellationToken cancellationToken)
    {
        var route = await _driveRouteClient.GetDriveRouteAsync(startLat, startLng, endLat, endLng, cancellationToken)
            .ConfigureAwait(false);
        if (route is not null)
        {
            return new TodayPickupHomeDriveDto
            {
                driveMin = DepartureGuideCalculator.SecondsToMinutesCeil(route.TotalSeconds),
                distanceM = route.DistanceM,
                source = HomeDriveSourceRoute,
                path = route.Path.Count == 0
                    ? null
                    : route.Path.Select(p => new GeoPointDto { lat = p.Lat, lng = p.Lng }).ToList()
            };
        }

        var meters = GeoDistance.Meters(startLat, startLng, endLat, endLng);
        return new TodayPickupHomeDriveDto
        {
            driveMin = DepartureGuideCalculator.EstimateMinutes(meters, _options.DriveKmh, _options.RoadFactor),
            distanceM = null,
            source = HomeDriveSourceEstimate,
            path = null
        };
    }
}
