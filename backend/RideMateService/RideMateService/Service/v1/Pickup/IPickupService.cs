using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public interface IPickupService
{
    /// <summary>
    /// 출발 안내 계산
    /// </summary>
    Task<ResponseModel<DetailDepartureGuideDto>> DetailDepartureGuideService(DepartureGuideQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 대기 장소 추천 목록
    /// </summary>
    Task<ResponseModel<WaitingPlaceListDto>> GetWaitingPlaceListService(WaitingPlaceQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가 단속 구역 목록
    /// </summary>
    Task<ResponseModel<GetRestrictedZoneList>> GetRestrictedZoneListService(RestrictedZoneListQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 링크 문자 발송(최초 발송과 실패 후 다시 보내기)
    /// </summary>
    Task<ResponseModel<PickupLinkSendDto>> AddPickupLinkService(AddPickupLinkDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 정차 후 "도착했어요" 다시 알림 문자 발송
    /// </summary>
    Task<ResponseModel<PickupLinkSendDto>> AddPickupRemindService(AddPickupRemindDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 링크 전달 상태 조회(링크·다시 알림 발송, 열람, 지금 나가요)
    /// </summary>
    Task<ResponseModel<DetailPickupLinkStatusDto>> DetailPickupLinkStatusService(PickupLinkStatusQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 오늘 픽업 계획 만들기(이미 있으면 그 계획). 기본 지점을 우선순위·금지 시간·정원으로 배정한다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> AddPickupPlanService(AddPickupPlanDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 장소 바꾸기(임의 좌표로 바꾸거나 기본 지점으로 되돌리기). 같은 clientEventId를 다시 보내면 한 번만 반영한다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanLocationService(UpdatePickupPlanLocationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 경로 시작. 늦게 출발했으면 같은 장소에서 배정 시각을 차 도착 예상 시각으로 미룬다. 이미 시작했으면 지금 계획을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStartService(UpdatePickupPlanStartDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 정차 기록. 배정 장소와의 거리와 정차 위치의 단속 구역을 판정해 돌려주고 계획을 STOPPED로 바꾼다.
    /// 같은 clientEventId를 다시 보내면(계획이 끝났어도) 한 번만 반영하고 200을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStopService(UpdatePickupPlanStopDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 만료 주기 작업 한 회차. EXPIRE_DT가 지난 진행 중 계획을 계획마다 따로 트랜잭션으로 EXPIRED로 끝내고 익명화한다.
    /// 끝낸 계획 수를 돌려준다. 한 계획의 오류는 로그만 남기고 다음 계획으로 간다. HTTP 요청 밖(주기 작업)에서 부른다
    /// </summary>
    Task<int> ExpirePickupPlansService(CancellationToken cancellationToken = default);
}
