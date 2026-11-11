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
}
