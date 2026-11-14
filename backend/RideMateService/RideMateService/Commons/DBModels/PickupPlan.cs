using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 오늘 계획
/// </summary>
public partial class PickupPlan
{
    /// <summary>
    /// 오늘 계획 ID
    /// </summary>
    public int PickupPlanId { get; set; }

    /// <summary>
    /// 운전자 계정 ID
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// 아이 ID
    /// </summary>
    public int? ChildId { get; set; }

    /// <summary>
    /// 시간표 요일 ID
    /// </summary>
    public int? TimetableDayId { get; set; }

    /// <summary>
    /// 학원 ID
    /// </summary>
    public int? AcademyId { get; set; }

    /// <summary>
    /// 학원가 ID
    /// </summary>
    public int AreaId { get; set; }

    /// <summary>
    /// 픽업 날짜
    /// </summary>
    public DateTime PickupDate { get; set; }

    /// <summary>
    /// 적용된 하원 시각
    /// </summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// 배정 픽업 지점 ID
    /// </summary>
    public int? PickupSpotId { get; set; }

    /// <summary>
    /// 배정 도착 일시
    /// </summary>
    public DateTime? SlotDt { get; set; }

    /// <summary>
    /// 아이 도보 시간(분)
    /// </summary>
    public sbyte? WalkMin { get; set; }

    /// <summary>
    /// 부모가 고른 픽업 위도
    /// </summary>
    public decimal? CustomLat { get; set; }

    /// <summary>
    /// 부모가 고른 픽업 경도
    /// </summary>
    public decimal? CustomLng { get; set; }

    /// <summary>
    /// 부모가 고른 픽업 장소 이름
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// 장소를 바꾸기 직전 장소가 기본 지점이면 그 지점
    /// </summary>
    public int? PrevPickupSpotId { get; set; }

    /// <summary>
    /// 직전 장소가 부모가 고른 임의 좌표면 그 위도
    /// </summary>
    public decimal? PrevCustomLat { get; set; }

    /// <summary>
    /// 직전 장소가 부모가 고른 임의 좌표면 그 경도
    /// </summary>
    public decimal? PrevCustomLng { get; set; }

    /// <summary>
    /// 직전 임의 좌표의 장소 이름
    /// </summary>
    public string? PrevCustomName { get; set; }

    /// <summary>
    /// 계획 만료 일시
    /// </summary>
    public DateTime ExpireDt { get; set; }

    /// <summary>
    /// 계획 상태 (PLANNED, STARTED, WAITING, STOPPED, LOOPING, BOARDED, EXPIRED, CANCELED)
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// 대기 장소 ID
    /// </summary>
    public int? ParkingLotId { get; set; }

    /// <summary>
    /// 경로 시작 일시
    /// </summary>
    public DateTime? StartDt { get; set; }

    /// <summary>
    /// 대기 장소 도착 일시
    /// </summary>
    public DateTime? WaitStartDt { get; set; }

    /// <summary>
    /// 픽업 지점 최초 정차 일시
    /// </summary>
    public DateTime? StopDt { get; set; }

    /// <summary>
    /// 탑승 감지 일시
    /// </summary>
    public DateTime? BoardDt { get; set; }

    /// <summary>
    /// 계획 종료 일시
    /// </summary>
    public DateTime? CloseDt { get; set; }

    /// <summary>
    /// 생성 일시
    /// </summary>
    public DateTime CreateDt { get; set; }

    /// <summary>
    /// 생성자
    /// </summary>
    public string CreateUser { get; set; } = null!;

    /// <summary>
    /// 수정 일시
    /// </summary>
    public DateTime UpdateDt { get; set; }

    /// <summary>
    /// 수정자
    /// </summary>
    public string UpdateUser { get; set; } = null!;

    /// <summary>
    /// 삭제 여부
    /// </summary>
    public bool DeleteYn { get; set; }

    public virtual Academy? Academy { get; set; }

    public virtual Area Area { get; set; } = null!;

    public virtual Child? Child { get; set; }

    public virtual ParkingLot? ParkingLot { get; set; }

    public virtual ICollection<PickupEvent> PickupEvents { get; set; } = new List<PickupEvent>();

    public virtual PickupSession? PickupSession { get; set; }

    public virtual PickupSpot? PickupSpot { get; set; }

    public virtual PickupSpot? PrevPickupSpot { get; set; }

    public virtual TimetableDay? TimetableDay { get; set; }

    public virtual UserInfo? User { get; set; }
}
