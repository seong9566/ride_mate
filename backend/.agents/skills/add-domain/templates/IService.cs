using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.{도메인}.Request;
using RideMateService.Comm.Dto.v1.{도메인}.Response;

namespace RideMateService.Service.v1.{도메인};

public interface I{도메인}Service
{
    /// <summary>
    /// {한글 설명}
    /// </summary>
    Task<ResponseModel<bool>> Add{도메인}Service(Add{도메인}Dto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// {한글 설명}
    /// </summary>
    Task<ResponseModel<CursorResult<Get{도메인}List>>> Get{도메인}ListService(
        int? last{도메인}Seq, int pageSize, string? searchKey,
        CancellationToken cancellationToken = default);
}
