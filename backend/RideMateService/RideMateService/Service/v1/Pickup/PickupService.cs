using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Comm.Logger;
using RideMateService.Comm.Map;
using RideMateService.Comm.Parking;
using RideMateService.Comm.Security;
using RideMateService.Comm.Sms;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService : IPickupService
{
    private const string WalkSourceStored = "STORED";
    private const string WalkSourceRoute = "ROUTE";
    private const string WalkSourceEstimate = "ESTIMATE";
    private const string DriveSourceNavi = "NAVI";
    private const string DriveSourceEstimate = "ESTIMATE";
    private const string EnforcementRestricted = "RESTRICTED";
    private const string EnforcementClear = "CLEAR";
    private const string EnforcementUnknown = "UNKNOWN";
    private const string ReasonAcademyFrontRestricted = "ACADEMY_FRONT_RESTRICTED";
    private const string ReasonHigherSpotBlocked = "HIGHER_SPOT_BLOCKED";
    private const string ReasonNoSpotAvailable = "NO_SPOT_AVAILABLE";
    private const string ReasonCustomLocation = "CUSTOM_LOCATION";
    private const string ReasonNoAcademyLocation = "NO_ACADEMY_LOCATION";
    private const string ReasonSlotShifted = "SLOT_SHIFTED";

    private readonly IPickupRepository _pickupRepository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWalkRouteClient _walkRouteClient;
    private readonly IAddressClient _addressClient;
    private readonly IParkingRealtimeClient _parkingRealtimeClient;
    private readonly DepartureGuideOptions _options;
    private readonly WaitingPlaceOptions _waitingPlaceOptions;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다
    private readonly IHashService _hashService;
    private readonly IPhoneCryptoService _phoneCryptoService;
    private readonly ISmsSender _smsSender;
    private readonly PickupLinkOptions _linkOptions;
    private readonly PickupPlanOptions _planOptions;

    public PickupService(IPickupRepository pickupRepository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        IWalkRouteClient walkRouteClient,
        IAddressClient addressClient,
        IParkingRealtimeClient parkingRealtimeClient,
        IOptions<DepartureGuideOptions> options,
        IOptions<WaitingPlaceOptions> waitingPlaceOptions,
        RideMateContext context,
        IHashService hashService,
        IPhoneCryptoService phoneCryptoService,
        ISmsSender smsSender,
        IOptions<PickupLinkOptions> linkOptions,
        IOptions<PickupPlanOptions> planOptions)
    {
        this._pickupRepository = pickupRepository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._walkRouteClient = walkRouteClient;
        this._addressClient = addressClient;
        this._parkingRealtimeClient = parkingRealtimeClient;
        this._options = options.Value;
        this._waitingPlaceOptions = waitingPlaceOptions.Value;
        this._context = context;
        this._hashService = hashService;
        this._phoneCryptoService = phoneCryptoService;
        this._smsSender = smsSender;
        this._linkOptions = linkOptions.Value;
        this._planOptions = planOptions.Value;
    }

    /// <summary>
    /// 출발 안내 계산
    /// </summary>
    public async Task<ResponseModel<DetailDepartureGuideDto>> DetailDepartureGuideService(DepartureGuideQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<DetailDepartureGuideDto> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — 필수값이 없거나 픽업 좌표가 한쪽만 오면 거절한다
            if (dto?.currentDt is null || dto.currentLat is null || dto.currentLng is null
                || (dto.pickupLat is null) != (dto.pickupLng is null))
                return new ResponseModel<DetailDepartureGuideDto> { message = "잘못된 요청입니다.", data = null, code = 400 };

            var computation = await ComputeDepartureAsync(userId, dto, cancellationToken).ConfigureAwait(false);
            return new ResponseModel<DetailDepartureGuideDto>
            {
                message = "요청이 정상 처리되었습니다.",
                data = computation.Result,
                code = computation.Code
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<DetailDepartureGuideDto> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    /// <summary>
    /// 학원가 단속 구역 목록
    /// </summary>
    public async Task<ResponseModel<GetRestrictedZoneList>> GetRestrictedZoneListService(RestrictedZoneListQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return new ResponseModel<GetRestrictedZoneList> { message = "잘못된 요청입니다.", data = null, code = 401 };

            var userAreas = await _pickupRepository.GetUserAreasAsync(userId, cancellationToken).ConfigureAwait(false);
            var areaIds = userAreas.Select(a => a.AreaId).ToArray();
            string version;
            if (userAreas.Count == 0)
            {
                version = ComputeVersion(areaIds, null, null, 0);
            }
            else
            {
                var versionRow = await _pickupRepository.GetRestrictedZoneVersionAsync(areaIds, cancellationToken)
                    .ConfigureAwait(false);
                version = ComputeVersion(areaIds, userAreas.Max(a => a.AreaUpdatedDt), versionRow.MaxDt,
                    versionRow.LiveCount);
            }

            if (dto.version == version)
            {
                return new ResponseModel<GetRestrictedZoneList>
                {
                    message = "요청이 정상 처리되었습니다.",
                    data = new GetRestrictedZoneList { version = version, areas = null },
                    code = 101
                };
            }

            if (userAreas.Count == 0)
            {
                return new ResponseModel<GetRestrictedZoneList>
                {
                    message = "요청이 정상 처리되었습니다.",
                    data = new GetRestrictedZoneList { version = version, areas = new List<RestrictedZoneAreaDto>() },
                    code = 200
                };
            }

            var zoneRows = await _pickupRepository.GetRestrictedZonesAsync(areaIds, cancellationToken)
                .ConfigureAwait(false);
            var resultAreas = new List<RestrictedZoneAreaDto>(userAreas.Count);
            var vertexCount = 0;

            foreach (var area in userAreas)
            {
                var areaZoneRows = zoneRows.Where(z => z.AreaId == area.AreaId).ToList();
                var coordinates = areaZoneRows
                    .Select(z => ZoneShapeBuilder.FirstCoordinate(z.GeomWkt))
                    .Where(c => c.HasValue)
                    .Select(c => c!.Value)
                    .ToList();
                var hasOrigin = coordinates.Count > 0;
                var originLat = hasOrigin ? coordinates.Average(c => c.Lat) : 0;
                var originLng = hasOrigin ? coordinates.Average(c => c.Lng) : 0;
                var zones = new List<RestrictedZoneDto>(areaZoneRows.Count);

                foreach (var row in areaZoneRows)
                {
                    var zone = new RestrictedZoneDto
                    {
                        restrictedZoneSeq = row.RestrictedZoneId,
                        zoneType = row.ZoneType,
                        dayType = row.DayType,
                        activeStartTime = row.ActiveStartTime?.ToString(@"hh\:mm"),
                        activeEndTime = row.ActiveEndTime?.ToString(@"hh\:mm")
                    };

                    if (hasOrigin)
                    {
                        var shape = ZoneShapeBuilder.Build(row.GeomWkt, row.BufferM, originLat, originLng);
                        if (shape.FailureReason is not null)
                        {
                            _logService.LogMessage($"restricted zone shape failed: id={row.RestrictedZoneId}, reason={shape.FailureReason}");
                        }
                        else
                        {
                            zone.polygons = shape.Polygons.Select(p => new ZonePolygonDto
                            {
                                outer = p.Outer.Select(point => new GeoPointDto
                                {
                                    lat = (decimal)point.Lat,
                                    lng = (decimal)point.Lng
                                }).ToList(),
                                holes = p.Holes.Select(hole => hole.Select(point => new GeoPointDto
                                {
                                    lat = (decimal)point.Lat,
                                    lng = (decimal)point.Lng
                                }).ToList()).ToList()
                            }).ToList();
                        }
                    }
                    else
                    {
                        _logService.LogMessage($"restricted zone shape failed: id={row.RestrictedZoneId}, reason=no-origin");
                    }

                    vertexCount += zone.polygons.Sum(p => p.outer.Count + p.holes.Sum(h => h.Count));
                    zones.Add(zone);
                }

                resultAreas.Add(new RestrictedZoneAreaDto
                {
                    areaSeq = area.AreaId,
                    areaName = area.AreaName,
                    zones = zones
                });
            }

            if (vertexCount > 50000)
                _logService.LogMessage($"restricted zone list large: zones={zoneRows.Count}, vertices={vertexCount}");

            return new ResponseModel<GetRestrictedZoneList>
            {
                message = "요청이 정상 처리되었습니다.",
                data = new GetRestrictedZoneList { version = version, areas = resultAreas },
                code = 200
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<GetRestrictedZoneList> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }

    private async Task<DepartureComputation> ComputeDepartureAsync(int userId, DepartureGuideQueryDto dto,
        CancellationToken ct)
    {
        // 필수값(currentDt·currentLat·currentLng) 검증은 호출자가 끝냈다
        var now = dto.currentDt!.Value;

        // 3) 대상 픽업(설계 3절, 계획 API와 같은 규칙). 진행 중 계획은 서버 현재 시각으로 보고,
        //    계획이 없으면 currentDt의 날짜로 하원 뒤 창(PickupWindowHours)이 지나지 않은 것 중 하원이 가장 이른 것
        var selected = await SelectPickupTargetAsync(userId, DateTime.Now, now, ct).ConfigureAwait(false);
        if (selected is null)
            return new DepartureComputation { Code = 101, Result = null, Target = null, DayType = string.Empty, Now = now };

        var plan = selected.Plan;
        // 계획이 있으면 계획의 날짜, 없으면 currentDt의 날짜다
        var date = plan?.PickupDate ?? DateOnly.FromDateTime(now);

        // 4) 요일 유형 — 그해 공휴일이 적재되지 않았으면 공휴일이 아닌 것으로 본다
        var holiday = await _pickupRepository.GetHolidayAsync(date, ct).ConfigureAwait(false);
        var dayType = DepartureGuideCalculator.DayType(date, holiday.IsHoliday);

        TodayPickupRow target;
        DetailDepartureGuideDto result;
        DepartureGuidePickupDto? pickup;
        DateTime? slotDt = null;

        if (plan is not null)
        {
            // 5) 계획이 있으면 계획이 정답이다. 앱이 보낸 pickupLat·pickupLng는 무시하고 카카오를 부르지 않는다(설계 6.5)
            target = PlanTarget(plan);
            result = new DetailDepartureGuideDto
            {
                academyName = target.DisplayName,
                endTime = target.EndTime.ToString(@"hh\:mm"),
                pickupPlanSeq = plan.PickupPlanId,
                planStatus = plan.Status
            };

            pickup = await PlanPickupAsync(plan, date, target.EndTime, dayType, result.reasons, ct).ConfigureAwait(false);
            if (pickup is null)
            {
                result.reasons.Add(new DepartureGuideReasonDto { code = ReasonNoSpotAvailable });
                return new DepartureComputation { Code = 200, Result = result, Target = target, DayType = dayType, Now = now };
            }

            if (pickup.pickupSpotSeq is null)
            {
                result.reasons.Add(new DepartureGuideReasonDto { code = ReasonCustomLocation });
            }
            else if (target is { AcademyLat: { } planAcademyLat, AcademyLng: { } planAcademyLng })
            {
                // 학원 앞이 단속 구역이면 근거 맨 앞에 둔다. 학원 좌표는 계획 학원의 현재 값, 판정 시각은 하원 + 정리 시간
                var academyZones = await MatchZonesAsync(target.AreaId, planAcademyLat, planAcademyLng, dayType,
                    target.EndTime + TimeSpan.FromMinutes(_options.TidyUpMin), ct).ConfigureAwait(false);
                if (academyZones is { Count: > 0 })
                    result.reasons.Insert(0, new DepartureGuideReasonDto { code = ReasonAcademyFrontRestricted, zoneTypes = academyZones });
            }

            slotDt = plan.SlotDt;
            result.slotDt = slotDt;
        }
        else
        {
            target = selected.Pickup!;
            result = new DetailDepartureGuideDto
            {
                academyName = target.DisplayName,
                endTime = target.EndTime.ToString(@"hh\:mm")
            };

            // 학원 좌표가 없으면 학원에서 걷는 시간·픽업 지점·단속 구역을 알 수 없다. 앱이 픽업 좌표를 보냈어도 같다
            if (target.AcademyLat is not { } academyLat || target.AcademyLng is not { } academyLng)
            {
                result.reasons.Add(new DepartureGuideReasonDto { code = ReasonNoAcademyLocation });
                return new DepartureComputation { Code = 200, Result = result, Target = target, DayType = dayType, Now = now };
            }

            // 5) 픽업 장소와 도보 시간
            if (dto.pickupLat is null || dto.pickupLng is null)
            {
                pickup = await SelectDefaultSpotAsync(target, date, dayType, result.reasons, ct).ConfigureAwait(false);
                if (pickup is null)
                {
                    result.reasons.Add(new DepartureGuideReasonDto { code = ReasonNoSpotAvailable });
                    return new DepartureComputation { Code = 200, Result = result, Target = target, DayType = dayType, Now = now };
                }

                // 학원 앞이 단속 구역이면 근거 맨 앞에 둔다. 판정 시각은 하원 + 정리 시간
                var academyZones = await MatchZonesAsync(target.AreaId, academyLat, academyLng, dayType,
                    target.EndTime + TimeSpan.FromMinutes(_options.TidyUpMin), ct).ConfigureAwait(false);
                if (academyZones is { Count: > 0 })
                    result.reasons.Insert(0, new DepartureGuideReasonDto { code = ReasonAcademyFrontRestricted, zoneTypes = academyZones });
            }
            else
            {
                pickup = await CustomPickupAsync(academyLat, academyLng, dto.pickupLat.Value, dto.pickupLng.Value, ct).ConfigureAwait(false);
                result.reasons.Add(new DepartureGuideReasonDto { code = ReasonCustomLocation });
            }
        }

        result.pickup = pickup;

        // 6) 아이 도착 시각
        var childArrival = DepartureGuideCalculator.ChildArrival(date, target.EndTime, _options.TidyUpMin, pickup.walkMin);
        result.childArrivalDt = childArrival;

        // 계획의 배정 시각이 아이 도착보다 늦으면(정원·늦은 출발로 미룸) 미룬 분을 근거로 준다
        if (slotDt is { } shiftedSlot && shiftedSlot > childArrival)
            result.reasons.Add(new DepartureGuideReasonDto
            {
                code = ReasonSlotShifted,
                shiftMin = (int)Math.Ceiling((shiftedSlot - childArrival).TotalMinutes)
            });

        // 7) 차량 소요 시간 — 앱이 보낸 값이 없으면 직선 거리로 추정한다
        if (dto.driveMin is not null)
        {
            result.driveMin = dto.driveMin.Value;
            result.driveSource = DriveSourceNavi;
        }
        else
        {
            var meters = GeoDistance.Meters(dto.currentLat!.Value, dto.currentLng!.Value, pickup.lat, pickup.lng);
            result.driveMin = DepartureGuideCalculator.EstimateMinutes(meters, _options.DriveKmh, _options.RoadFactor);
            result.driveSource = DriveSourceEstimate;
        }

        // 8) 추천 출발 시각과 남은 여유. 계획이 있으면 배정 시각 기준이다(명세서 P-01 "배정 시각 − ETA − 여유")
        var departure = DepartureGuideCalculator.Departure(slotDt ?? childArrival, result.driveMin.Value, _options.BufferMin);
        result.departureDt = departure;
        result.slackMin = DepartureGuideCalculator.SlackMinutes(departure, now);

        // 9) 단속 구역 판정 — 판정 시각은 아이 도착 시각. 판정할 수 없으면 UNKNOWN
        var zones = await MatchZonesAsync(target.AreaId, pickup.lat, pickup.lng, dayType, childArrival.TimeOfDay,
            ct).ConfigureAwait(false);
        if (zones is null)
            result.enforcement = new DepartureGuideEnforcementDto { status = EnforcementUnknown };
        else if (zones.Count > 0)
            result.enforcement = new DepartureGuideEnforcementDto { status = EnforcementRestricted, zoneTypes = zones };
        else if (!holiday.YearLoaded)
            result.enforcement = new DepartureGuideEnforcementDto { status = EnforcementUnknown };
        else
            result.enforcement = new DepartureGuideEnforcementDto { status = EnforcementClear };

        return new DepartureComputation { Code = 200, Result = result, Target = target, DayType = dayType, Now = now };
    }

    // 진행 중 계획을 대상 픽업 행으로 옮긴다. 학원 이름·좌표·학원가는 계획 학원의 현재 값, 하원 시각은 계획의 값이다
    private static TodayPickupRow PlanTarget(LivePlanRow plan)
        => new()
        {
            TimetableDayId = plan.TimetableDayId
                             ?? throw new InvalidOperationException($"live pickup plan without timetable day: plan={plan.PickupPlanId}"),
            AcademyId = plan.AcademyId
                        ?? throw new InvalidOperationException($"live pickup plan without academy: plan={plan.PickupPlanId}"),
            AreaId = plan.AreaId,
            DisplayName = plan.AcademyName ?? string.Empty,
            EndTime = plan.EndTime
                      ?? throw new InvalidOperationException($"live pickup plan without end time: plan={plan.PickupPlanId}"),
            AcademyLat = plan.AcademyLat,
            AcademyLng = plan.AcademyLng
        };

    // 기본 지점: 우선순위 순으로 아이 도착 시각이 금지 시간 [시작, 종료)에 들지 않는 첫 지점.
    // 금지 시간에 걸려 건너뛴 지점마다 HIGHER_SPOT_BLOCKED를 reasons에 더한다. 쓸 지점이 없으면 null
    private async Task<DepartureGuidePickupDto?> SelectDefaultSpotAsync(TodayPickupRow target, DateOnly date, string dayType,
        List<DepartureGuideReasonDto> reasons, CancellationToken cancellationToken)
    {
        var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(target.AcademyId, cancellationToken).ConfigureAwait(false);
        if (spots.Count == 0)
            return null;

        var blocks = await _pickupRepository.GetPickupSpotBlocksAsync(spots.Select(s => s.PickupSpotId).ToArray(), dayType,
            cancellationToken).ConfigureAwait(false);

        foreach (var spot in spots)
        {
            var arrival = DepartureGuideCalculator.ChildArrival(date, target.EndTime, _options.TidyUpMin, spot.WalkMin).TimeOfDay;
            var block = blocks.FirstOrDefault(b => b.PickupSpotId == spot.PickupSpotId
                                                   && b.StartTime <= arrival && arrival < b.EndTime);
            if (block is not null)
            {
                reasons.Add(HigherSpotBlockedReason(spot, block));
                continue;
            }

            return new DepartureGuidePickupDto
            {
                pickupSpotSeq = spot.PickupSpotId,
                spotName = spot.SpotName,
                lat = spot.Lat,
                lng = spot.Lng,
                walkMin = spot.WalkMin,
                walkSource = WalkSourceStored,
                walkPath = ParseLineString(spot.WalkPathWkt)
            };
        }

        return null;
    }

    // 계획의 픽업 장소(설계 6.5). 장소 판정은 BuildPlanDtoAsync와 같다: 임의 좌표면 계획에 저장된 좌표·이름·도보 시간·경로,
    // 기본 지점이면 지점 좌표·계획의 도보 시간·학원 → 지점 도보 경로(ACADEMY_PICKUP_SPOT.WALK_PATH). 둘 다 STORED이고 장소가 없으면 null.
    // 기본 지점이면 계획 지점보다 우선순위가 높은 후보 중 아이 도착 시각이 금지 시간에 걸린 것마다 HIGHER_SPOT_BLOCKED를 더한다
    private async Task<DepartureGuidePickupDto?> PlanPickupAsync(LivePlanRow plan, DateOnly date, TimeSpan endTime,
        string dayType, List<DepartureGuideReasonDto> reasons, CancellationToken cancellationToken)
    {
        if (plan is { CustomLat: { } customLat, CustomLng: { } customLng, CustomName: { } customName })
        {
            return new DepartureGuidePickupDto
            {
                pickupSpotSeq = null,
                spotName = customName,
                lat = customLat,
                lng = customLng,
                walkMin = plan.WalkMin ?? 0,
                walkSource = WalkSourceStored,
                walkPath = ParseLineString(plan.CustomWalkPathWkt)
            };
        }

        if (plan is not { PickupSpotId: { } spotId, SpotName: { } spotName, SpotLat: { } spotLat, SpotLng: { } spotLng })
            return null;

        // 후보 목록에서 계획 지점의 도보 경로와 그보다 앞선 후보를 찾는다.
        // 배정 뒤 지점이 사용 중지되거나 학원 연결이 풀려 목록에 없으면 경로와 근거 없이 계획 지점을 준다
        List<GeoPointDto>? walkPath = null;
        if (plan.AcademyId is { } academyId)
        {
            var spots = await _pickupRepository.GetAcademyPickupSpotsAsync(academyId, cancellationToken).ConfigureAwait(false);
            var index = spots.FindIndex(s => s.PickupSpotId == spotId);
            if (index >= 0)
            {
                walkPath = ParseLineString(spots[index].WalkPathWkt);

                var higherSpots = spots.Take(index).ToList();
                if (higherSpots.Count > 0)
                {
                    var blocks = await _pickupRepository.GetPickupSpotBlocksAsync(
                        higherSpots.Select(s => s.PickupSpotId).ToArray(), dayType, cancellationToken).ConfigureAwait(false);
                    foreach (var spot in higherSpots)
                    {
                        var arrival = DepartureGuideCalculator.ChildArrival(date, endTime, _options.TidyUpMin, spot.WalkMin).TimeOfDay;
                        var block = blocks.FirstOrDefault(b => b.PickupSpotId == spot.PickupSpotId
                                                               && b.StartTime <= arrival && arrival < b.EndTime);
                        if (block is not null)
                            reasons.Add(HigherSpotBlockedReason(spot, block));
                    }
                }
            }
        }

        return new DepartureGuidePickupDto
        {
            pickupSpotSeq = spotId,
            spotName = spotName,
            lat = spotLat,
            lng = spotLng,
            walkMin = plan.WalkMin ?? 0,
            walkSource = WalkSourceStored,
            walkPath = walkPath
        };
    }

    // HIGHER_SPOT_BLOCKED 근거 한 건(금지 시간에 걸려 건너뛴 지점)
    private static DepartureGuideReasonDto HigherSpotBlockedReason(PickupSpotRow spot, SpotBlockRow block)
        => new()
        {
            code = ReasonHigherSpotBlocked,
            pickupSpotSeq = spot.PickupSpotId,
            spotName = spot.SpotName,
            blockReason = block.Reason,
            blockStartTime = block.StartTime.ToString(@"hh\:mm"),
            blockEndTime = block.EndTime.ToString(@"hh\:mm")
        };

    // 임의 좌표: 카카오 도보 경로(학원 → 픽업)를 매번 부른다(결과 캐싱 금지).
    // 실패하면 직선 거리 ÷ 아이 도보 속도로 추정하고 경로는 주지 않는다
    private async Task<DepartureGuidePickupDto> CustomPickupAsync(decimal academyLat, decimal academyLng, decimal lat, decimal lng,
        CancellationToken cancellationToken)
    {
        var pickup = new DepartureGuidePickupDto { lat = lat, lng = lng };

        var route = await _walkRouteClient.GetWalkRouteAsync(academyLat, academyLng, lat, lng,
            cancellationToken).ConfigureAwait(false);
        if (route is not null)
        {
            pickup.walkMin = DepartureGuideCalculator.SecondsToMinutesCeil(route.TotalSeconds);
            pickup.walkSource = WalkSourceRoute;
            pickup.walkPath = route.Path.Count > 0
                ? route.Path.Select(p => new GeoPointDto { lat = p.Lat, lng = p.Lng }).ToList()
                : null;
            return pickup;
        }

        var meters = GeoDistance.Meters(academyLat, academyLng, lat, lng);
        pickup.walkMin = DepartureGuideCalculator.EstimateMinutes(meters, _options.ChildWalkKmh);
        pickup.walkSource = WalkSourceEstimate;
        return pickup;
    }

    // 걸린 구역 유형. 학원가가 없거나 학원가 밖이거나 학원가에 구역 데이터가 없어 판정할 수 없으면 null
    private async Task<List<string>?> MatchZonesAsync(int? areaId, decimal lat, decimal lng, string dayType, TimeSpan at,
        CancellationToken cancellationToken)
    {
        if (areaId is not { } area)
            return null;

        if (!await _pickupRepository.IsInsideAreaAsync(area, lat, lng, cancellationToken).ConfigureAwait(false))
            return null;

        if (await _pickupRepository.CountRestrictedZonesAsync(area, cancellationToken).ConfigureAwait(false) == 0)
            return null;

        return await _pickupRepository.GetMatchedZoneTypesAsync(area, lat, lng, dayType, at, cancellationToken)
            .ConfigureAwait(false);
    }

    // SRID 4326 LINESTRING WKT("LINESTRING(위도 경도,위도 경도,…)")를 점 목록으로 바꾼다. 없으면 null
    private static List<GeoPointDto>? ParseLineString(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            return null;

        var open = wkt.IndexOf('(');
        var close = wkt.LastIndexOf(')');
        if (open < 0 || close <= open)
            return null;

        var points = wkt[(open + 1)..close]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(xy => new GeoPointDto
            {
                lat = decimal.Parse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture),
                lng = decimal.Parse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture)
            })
            .ToList();

        return points.Count > 0 ? points : null;
    }

    private static string ComputeVersion(int[] areaIds, DateTime? areaMaxDt, DateTime? zoneMaxDt, int liveCount)
    {
        var input = string.Join('|',
            ZoneShapeBuilder.FormatVersion,
            string.Join(',', areaIds.OrderBy(x => x)),
            areaMaxDt?.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) ?? string.Empty,
            zoneMaxDt?.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) ?? string.Empty,
            liveCount.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
