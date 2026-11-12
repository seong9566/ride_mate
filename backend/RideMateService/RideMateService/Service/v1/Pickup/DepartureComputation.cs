using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 출발 안내 계산의 중간 결과
/// </summary>
internal sealed class DepartureComputation
{
    /// <summary>
    /// 응답 코드. 오늘 남은 픽업이 없으면 101, 그 밖은 200
    /// </summary>
    public int Code { get; init; }

    /// <summary>
    /// 출발 안내 결과. 오늘 남은 픽업이 없으면 null
    /// </summary>
    public DetailDepartureGuideDto? Result { get; init; }

    /// <summary>
    /// 선택한 오늘 픽업. 오늘 남은 픽업이 없으면 null
    /// </summary>
    public TodayPickupRow? Target { get; init; }

    /// <summary>
    /// 요일 유형. 오늘 남은 픽업이 없으면 빈 문자열
    /// </summary>
    public string DayType { get; init; } = string.Empty;

    /// <summary>
    /// 요청이 지정한 현재 시각
    /// </summary>
    public DateTime Now { get; init; }
}
