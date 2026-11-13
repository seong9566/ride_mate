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
    /// 홈 화면 조회
    /// </summary>
    Task<ResponseModel<DetailHomeDto>> DetailHomeService(CancellationToken cancellationToken = default);

    /// <summary>
    /// 요일별 픽업 스위치 변경
    /// </summary>
    Task<ResponseModel<ScheduleDayDto>> UpdateScheduleDayPickupService(UpdateScheduleDayPickupDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 우리 픽업 장소 저장
    /// </summary>
    Task<ResponseModel<UpdateSchedulePickupSpotResultDto>> UpdateSchedulePickupSpotService(
        UpdateSchedulePickupSpotDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 시간표 삭제
    /// </summary>
    Task<ResponseModel<DeleteScheduleResultDto>> DeleteScheduleService(DeleteScheduleDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 시간표 편집
    /// </summary>
    Task<ResponseModel<AddScheduleResultDto>> UpdateScheduleService(UpdateScheduleDto dto,
        CancellationToken cancellationToken = default);
}
