using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.ChildInfo.Request;
using RideMateService.Comm.Dto.v1.ChildInfo.Response;

namespace RideMateService.Service.v1.ChildInfo;

public interface IChildInfoService
{
    /// <summary>
    /// 아이 정보 저장
    /// </summary>
    Task<ResponseModel<AddChildResultDto>> AddChildService(AddChildDto dto, CancellationToken cancellationToken = default);
}
