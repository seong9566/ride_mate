using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Schedule.Request;
using RideMateService.Comm.Dto.v1.Schedule.Response;

namespace RideMateService.Service.v1.Schedule;

public interface IScheduleService
{
    /// <summary>
    /// 시간표 등록
    /// </summary>
    Task<ResponseModel<AddScheduleResultDto>> AddScheduleService(AddScheduleDto dto,
        CancellationToken cancellationToken = default);
}
