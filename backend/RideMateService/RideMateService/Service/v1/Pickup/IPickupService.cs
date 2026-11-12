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
}
