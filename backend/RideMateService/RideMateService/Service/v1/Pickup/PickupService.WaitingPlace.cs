using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Map;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    /// <summary>
    /// 대기 장소 추천 목록
    /// </summary>
    public async Task<ResponseModel<WaitingPlaceListDto>> GetWaitingPlaceListService(WaitingPlaceQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<WaitingPlaceListDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 출발 안내와 같다. 필수값이 없거나 픽업 좌표가 한쪽만 오면 거절한다
            if (!IsValidDepartureQuery(dto))
                return new ResponseModel<WaitingPlaceListDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var computation = await ComputeDepartureAsync(userId, dto!, cancellationToken).ConfigureAwait(false);
            if (computation.Code == 401) // 계정이 없거나 지워졌다
                return new ResponseModel<WaitingPlaceListDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            if (computation.Code == 101)
            {
                return new ResponseModel<WaitingPlaceListDto>
                {
                    message = "요청이 정상 처리되었습니다.",
                    data = null,
                    code = 101
                };
            }

            // 학원가 진입 혼잡 보정은 출발 안내와 같다. 남은 여유(slackMin)·대기 시간·예상 요금이 출발 안내의 추천 출발과 맞는다
            await ApplyCongestionAsync(computation, cancellationToken).ConfigureAwait(false);

            var departure = computation.Result!;
            var target = computation.Target!;
            var result = new WaitingPlaceListDto
            {
                academyName = departure.academyName,
                endTime = departure.endTime,
                academyLat = target.AcademyLat,
                academyLng = target.AcademyLng,
                pickup = departure.pickup,
                slackMin = departure.slackMin,
                reasons = departure.reasons
            };

            if (departure.pickup is null || departure.slackMin is null || target.AreaId is null)
                return Success(result);

            var rows = await _pickupRepository.GetParkingLotsAsync(target.AreaId.Value, cancellationToken)
                .ConfigureAwait(false);
            var candidates = BuildCandidates(rows, dto, computation, departure);
            await ApplyRealtimeAsync(candidates, cancellationToken).ConfigureAwait(false);

            result.places = candidates
                .Select(ToWaitingPlace)
                .Where(x => x is not null)
                .OrderBy(x => SpaceOrder(x!.Place.space))
                .ThenBy(x => x!.SortFee)
                .ThenBy(x => x!.Place.driveMin)
                .ThenBy(x => x!.Place.parkingLotSeq)
                .Take(_waitingPlaceOptions.MaxPlaces)
                .Select(x => x!.Place)
                .ToList();

            return Success(result);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<WaitingPlaceListDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private List<WaitingPlaceCandidate> BuildCandidates(List<ParkingLotRow> rows, WaitingPlaceQueryDto dto,
        DepartureComputation computation, DetailDepartureGuideDto departure)
    {
        var pickup = departure.pickup!;
        var candidates = new List<WaitingPlaceCandidate>(rows.Count);

        foreach (var row in rows)
        {
            var toLotMeters = GeoDistance.Meters(dto.currentLat!.Value, dto.currentLng!.Value, row.Lat, row.Lng);
            var driveToLot = DepartureGuideCalculator.EstimateMinutes(toLotMeters, _options.DriveKmh, _options.RoadFactor);
            var lotToPickup = DepartureGuideCalculator.EstimateMinutes(
                GeoDistance.Meters(row.Lat, row.Lng, pickup.lat, pickup.lng),
                _options.DriveKmh, _options.RoadFactor);
            if (lotToPickup > _waitingPlaceOptions.MaxLotToPickupMin)
                continue;

            var waitMin = WaitingPlaceCalculator.WaitMinutes(departure.slackMin!.Value,
                computation.DriveMin!.Value, driveToLot, lotToPickup);
            if (waitMin <= 0)
                continue;

            // 대기 종료는 도보가 길면 하원 시각보다 늦을 수 있다(ADR-0028). 하원 시각과 대기 종료 중 늦은 쪽까지 운영해야 한다
            var (open, close) = OperatingHours(row, computation.DayType);
            var waitStart = computation.Now + TimeSpan.FromMinutes(driveToLot);
            var waitEnd = (waitStart + TimeSpan.FromMinutes(waitMin)).TimeOfDay;
            var endTime = computation.Target!.EndTime;
            if (!WaitingPlaceCalculator.IsOpen(open, close, waitStart.TimeOfDay, waitEnd > endTime ? waitEnd : endTime))
                continue;

            candidates.Add(new WaitingPlaceCandidate
            {
                Row = row,
                DriveToLot = driveToLot,
                DistanceM = (int)Math.Round(toLotMeters * _options.RoadFactor),
                WaitMin = waitMin
            });
        }

        return candidates;
    }

    private async Task ApplyRealtimeAsync(List<WaitingPlaceCandidate> candidates, CancellationToken cancellationToken)
    {
        var checkedDt = DateTime.Now;
        var cacheCutoff = checkedDt.AddSeconds(-_waitingPlaceOptions.RealtimeCacheSec);
        var refs = candidates
            .Where(x => !string.IsNullOrWhiteSpace(x.Row.RealtimeRef)
                        && (x.Row.AvailableSpaces is null || x.Row.AvailableUpdateDt is null
                            || x.Row.AvailableUpdateDt < cacheCutoff))
            .Select(x => x.Row.RealtimeRef!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (refs.Length == 0)
            return;

        var availableByRef = await _parkingRealtimeClient.GetAvailableAsync(refs, cancellationToken)
            .ConfigureAwait(false);
        if (availableByRef is null)
            return;

        foreach (var candidate in candidates)
        {
            if (candidate.Row.RealtimeRef is not { } realtimeRef
                || (candidate.Row.AvailableSpaces is not null && candidate.Row.AvailableUpdateDt >= cacheCutoff))
                continue;

            // 조회는 성공했는데 그 주차장이 목록에 없으면 지금은 실시간을 내지 않는 것이다. 오래된 저장값을 쓰지 않는다
            if (!availableByRef.TryGetValue(realtimeRef, out var available) || available is < 0 or > short.MaxValue)
            {
                candidate.RealtimeMissing = true;
                continue;
            }

            candidate.RealtimeAvailable = available;
            try
            {
                await _pickupRepository.UpdateAvailableSpacesAsync(candidate.Row.ParkingLotId, (short)available,
                    checkedDt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logService.LogMessage(ex.ToString());
#if DEBUG
                _logService.ConsoleWarning(ex.ToString());
#endif
            }
        }
    }

    private WaitingPlaceResult? ToWaitingPlace(WaitingPlaceCandidate candidate)
    {
        // 실시간 연결이 없거나 정상 응답에 빠진 주차장은 저장값이 남아 있어도 자리 정보 없음으로 둔다
        var available = candidate.Row.RealtimeRef is null || candidate.RealtimeMissing
            ? null
            : candidate.RealtimeAvailable ?? candidate.Row.AvailableSpaces;
        var space = WaitingPlaceCalculator.Space(available, _waitingPlaceOptions.FewThreshold);
        if (space is null)
            return null;

        var fee = WaitingPlaceCalculator.Fee(candidate.WaitMin, candidate.Row.BaseMin, candidate.Row.BaseFee,
            candidate.Row.AddUnitMin, candidate.Row.AddFee, candidate.Row.DayMaxFee);
        return new WaitingPlaceResult
        {
            Place = new WaitingPlaceDto
            {
                parkingLotSeq = candidate.Row.ParkingLotId,
                name = candidate.Row.LotName,
                lat = candidate.Row.Lat,
                lng = candidate.Row.Lng,
                driveMin = candidate.DriveToLot,
                distanceM = candidate.DistanceM,
                baseMin = candidate.Row.BaseMin,
                baseFee = candidate.Row.BaseFee,
                addUnitMin = candidate.Row.AddUnitMin,
                addFee = candidate.Row.AddFee,
                fee = fee,
                feeText = fee is null
                    ? WaitingPlaceCalculator.FeeText(candidate.Row.BaseMin, candidate.Row.BaseFee)
                    : null,
                dayPassFee = candidate.Row.DayPassFee,
                space = space,
                availableSpaces = available
            },
            SortFee = WaitingPlaceCalculator.SortFee(fee, candidate.WaitMin, candidate.Row.BaseMin,
                candidate.Row.BaseFee)
        };
    }

    private static (TimeSpan? Open, TimeSpan? Close) OperatingHours(ParkingLotRow row, string dayType)
        => dayType switch
        {
            "SAT" => (row.SatOpenTime, row.SatCloseTime),
            "HOLIDAY" => (row.HolidayOpenTime, row.HolidayCloseTime),
            _ => (row.WeekdayOpenTime, row.WeekdayCloseTime)
        };

    private static int SpaceOrder(string space)
        => space switch
        {
            "AVAILABLE" => 0,
            "FEW" => 1,
            _ => 2
        };

    private static ResponseModel<WaitingPlaceListDto> Success(WaitingPlaceListDto result)
        => new() { message = "요청이 정상 처리되었습니다.", data = result, code = 200 };

    private sealed class WaitingPlaceCandidate
    {
        public ParkingLotRow Row { get; init; } = null!;
        public int DriveToLot { get; init; }
        public int DistanceM { get; init; }
        public int WaitMin { get; init; }
        public int? RealtimeAvailable { get; set; }
        public bool RealtimeMissing { get; set; }
    }

    private sealed class WaitingPlaceResult
    {
        public WaitingPlaceDto Place { get; init; } = null!;
        public double SortFee { get; init; }
    }
}
