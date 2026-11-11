using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.User.Request;
using RideMateService.Comm.Dto.v1.User.Response;

namespace RideMateService.Service.v1.User;

public interface IUserService
{
    /// <summary>
    /// 집 저장
    /// </summary>
    Task<ResponseModel<HomeDto>> UpdateHomeService(UpdateHomeDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 차 정보 저장
    /// </summary>
    Task<ResponseModel<VehicleDto>> UpdateVehicleService(UpdateVehicleDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 초기 설정 완료
    /// </summary>
    Task<ResponseModel<SetupCompleteDto>> UpdateSetupCompleteService(CancellationToken cancellationToken = default);
}
