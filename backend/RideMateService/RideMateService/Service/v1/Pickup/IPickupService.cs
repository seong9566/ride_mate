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
}
