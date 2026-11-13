using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

/// <summary>
/// 출발 안내 계산의 중간 결과
/// </summary>
internal sealed class DepartureComputation
{
    /// <summary>
    /// 응답 코드. 일정 지정이 맞지 않거나 그 일정의 오늘 픽업이 없으면 101, 계정이 없으면 401, 그 밖은 200
    /// </summary>
    public int Code { get; init; }

    /// <summary>
    /// 출발 안내 결과. 그 일정의 오늘 픽업이 없으면 null
    /// </summary>
    public DetailDepartureGuideDto? Result { get; init; }

    /// <summary>
    /// 선택한 오늘 픽업. 진행 중 계획이 있으면 계획 값(학원 이름·좌표는 계획 학원의 현재 값, 학원가와 하원 시각은 계획에 복사해 둔 값)으로 채운다.
    /// 그 일정의 오늘 픽업이 없으면 null
    /// </summary>
    public TodayPickupRow? Target { get; init; }

    /// <summary>
    /// 요일 유형. 그 일정의 오늘 픽업이 없으면 빈 문자열
    /// </summary>
    public string DayType { get; init; } = string.Empty;

    /// <summary>
    /// 계산 날짜의 공휴일 여부
    /// </summary>
    public bool IsHoliday { get; init; }

    /// <summary>
    /// 서버 현재 시각
    /// </summary>
    public DateTime Now { get; init; }

    /// <summary>
    /// 픽업 장소 도착 목표 시각(하원 시각 − 하원 전 도착 시간). 픽업 장소가 없으면 null
    /// </summary>
    public DateTime? TargetArrival { get; init; }

    /// <summary>
    /// 현재 위치 → 픽업 장소 차량 소요 시간(분, 첫 구간). 픽업 장소가 없으면 null
    /// </summary>
    public int? DriveMin { get; init; }
}
