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

    /// <summary>
    /// 내 시간표 목록
    /// </summary>
    Task<ResponseModel<List<ScheduleItemDto>>> GetScheduleListService(CancellationToken cancellationToken = default);

    /// <summary>
    /// 요일별 픽업 스위치 변경
    /// </summary>
    Task<ResponseModel<ScheduleDayDto>> UpdateScheduleDayPickupService(UpdateScheduleDayPickupDto dto,
        CancellationToken cancellationToken = default);
}
