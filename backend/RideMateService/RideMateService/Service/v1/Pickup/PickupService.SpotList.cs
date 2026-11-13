using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Map;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string ReasonNoSpotCandidate = "NO_SPOT_CANDIDATE";
    private const string UnavailableBlocked = "BLOCKED";
    private const string UnavailableFull = "FULL";

    /// <summary>
    /// 픽업 장소 후보 목록
    /// </summary>
    public async Task<ResponseModel<PickupSpotListDto>> GetPickupSpotListService(PickupSpotListQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<PickupSpotListDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값이 없거나 범위 밖이면 거절한다
            if (dto?.scheduleSeq is not >= 1)
                return new ResponseModel<PickupSpotListDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            // 3) 평가 날짜는 서버 현재 시각의 날짜. 소유한 살아 있는 시간표가 아니면 찾을 수 없다
            var now = DateTime.Now;
            var date = DateOnly.FromDateTime(now);
            var target = await _pickupRepository.GetPickupSpotTargetAsync(userId, dto.scheduleSeq.Value, date,
                DepartureGuideCalculator.DayOfWeek(date), cancellationToken).ConfigureAwait(false);
            if (target is null)
                return new ResponseModel<PickupSpotListDto> { message = "잘못된 요청입니다.", data = null, code = 404 };

            var result = new PickupSpotListDto
            {
                scheduleSeq = target.TimetableId,
                academyName = target.DisplayName,
                endTime = target.EndTime.ToString(@"hh\:mm"),
                areaName = target.AreaName
            };

            // 4) 학원 좌표나 학원가가 없으면 후보를 평가할 수 없다
            if (target.AcademyLat is not { } academyLat || target.AcademyLng is not { } academyLng
                || target.AreaId is not { } areaId)
            {
                result.reason = ReasonNoAcademyLocation;
                return Success(result);
            }

            // 5) 후보 — 우리 픽업 장소를 맨 앞에, 나머지는 PRIORITY 순 [ADR-0025]
            var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(target.AcademyId, target.PreferredSpotId,
                cancellationToken).ConfigureAwait(false);
            if (spots.Count == 0)
            {
                result.reason = ReasonNoSpotCandidate;
                return Success(result);
            }

            // 6) 요일 유형 — 그해 공휴일이 적재되지 않았으면 공휴일이 아닌 것으로 본다
            var holiday = await _pickupRepository.GetHolidayAsync(date, cancellationToken).ConfigureAwait(false);
            var dayType = DepartureGuideCalculator.DayType(date, holiday.IsHoliday);
            var blocks = await _pickupRepository.GetPickupSpotBlocksAsync(spots.Select(s => s.PickupSpotId).ToArray(),
                dayType, cancellationToken).ConfigureAwait(false);

            // 이 시간표의 오늘 계획은 정원 세기에서 뺀다(자기 자신 때문에 붐빔이 되지 않게). 트랜잭션·잠금 없는 일반 조회다
            int? excludePlanId = null;
            if (target.TimetableDayId is { } timetableDayId)
                excludePlanId = await _pickupRepository.GetLivePlanIdByTimetableDayAsync(timetableDayId, date, null,
                    cancellationToken).ConfigureAwait(false);

            var items = new List<PickupSpotItemDto>(spots.Count);
            foreach (var spot in spots)
            {
                var arrival = DepartureGuideCalculator.ChildArrival(date, target.EndTime, _options.TidyUpMin, spot.WalkMin);
                var item = new PickupSpotItemDto
                {
                    pickupSpotSeq = spot.PickupSpotId,
                    spotName = spot.SpotName,
                    lat = spot.Lat,
                    lng = spot.Lng,
                    note = spot.Note,
                    walkMin = spot.WalkMin,
                    available = true
                };

                var arrivalTime = arrival.TimeOfDay;
                var block = blocks.FirstOrDefault(b => b.PickupSpotId == spot.PickupSpotId
                                                       && b.StartTime <= arrivalTime && arrivalTime < b.EndTime);
                if (block is not null)
                {
                    item.available = false;
                    item.unavailableReason = UnavailableBlocked;
                    item.blockReason = block.Reason;
                    item.blockStartTime = block.StartTime.ToString(@"hh\:mm");
                    item.blockEndTime = block.EndTime.ToString(@"hh\:mm");
                }
                else
                {
                    // 도착 시각부터 SlotShiftMaxMin까지 1분씩 늦춘 모든 시각이 꽉 차면 FULL. 도착 시각 그대로 꽉 차면 붐빔
                    var full = true;
                    for (var shift = 0; shift <= _planOptions.SlotShiftMaxMin; shift++)
                    {
                        var count = await _pickupRepository.CountSlotPlansAsync(spot.PickupSpotId,
                            arrival.AddMinutes(shift), _planOptions.SlotWindowMin, now, excludePlanId, null,
                            cancellationToken).ConfigureAwait(false);
                        if (shift == 0)
                            item.busy = count >= spot.Capacity;
                        if (count < spot.Capacity)
                        {
                            full = false;
                            break;
                        }
                    }

                    if (full)
                    {
                        item.available = false;
                        item.unavailableReason = UnavailableFull;
                    }
                }

                items.Add(item);
            }

            // 7) 추천 — 보는 순서에서 쓸 수 있고 붐비지 않는 첫 지점, 없으면 쓸 수 있는 첫 지점. 추천 지점이 맨 앞이다
            var recommended = items.FirstOrDefault(i => i.available && !i.busy) ?? items.FirstOrDefault(i => i.available);
            if (recommended is not null)
            {
                recommended.recommended = true;
                items.Remove(recommended);
                items.Insert(0, recommended);
            }

            result.spots = items;

            // 8) 학원 앞 단속 구역 — 출발 안내와 같은 데이터·같은 시각(하원 + 정리 시간). 판정할 수 없으면 null
            var zones = await MatchZonesAsync(areaId, academyLat, academyLng, dayType,
                target.EndTime + TimeSpan.FromMinutes(_options.TidyUpMin), cancellationToken).ConfigureAwait(false);
            if (zones is { Count: > 0 })
                result.academyFrontRestricted = true;
            else if (zones is not null && holiday.YearLoaded)
                result.academyFrontRestricted = false;

            return Success(result);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<PickupSpotListDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private static ResponseModel<PickupSpotListDto> Success(PickupSpotListDto data)
        => new() { message = "요청이 정상 처리되었습니다.", data = data, code = 200 };
}
