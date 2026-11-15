using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 대기 장소(주차장)
/// </summary>
public partial class ParkingLot
{
    /// <summary>
    /// 대기 장소(주차장) ID
    /// </summary>
    public int ParkingLotId { get; set; }

    /// <summary>
    /// 추천 학원가 ID
    /// </summary>
    public int AreaId { get; set; }

    /// <summary>
    /// 주차장 이름
    /// </summary>
    public string LotName { get; set; } = null!;

    /// <summary>
    /// 주차장 주소
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 주차장 위도
    /// </summary>
    public decimal Lat { get; set; }

    /// <summary>
    /// 주차장 경도
    /// </summary>
    public decimal Lng { get; set; }

    /// <summary>
    /// 평일 운영 시작 시각
    /// </summary>
    public TimeSpan? WeekdayOpenTime { get; set; }

    /// <summary>
    /// 평일 운영 종료 시각
    /// </summary>
    public TimeSpan? WeekdayCloseTime { get; set; }

    /// <summary>
    /// 토요일 운영 시작 시각
    /// </summary>
    public TimeSpan? SatOpenTime { get; set; }

    /// <summary>
    /// 토요일 운영 종료 시각
    /// </summary>
    public TimeSpan? SatCloseTime { get; set; }

    /// <summary>
    /// 일요일·공휴일 운영 시작 시각
    /// </summary>
    public TimeSpan? HolidayOpenTime { get; set; }

    /// <summary>
    /// 일요일·공휴일 운영 종료 시각
    /// </summary>
    public TimeSpan? HolidayCloseTime { get; set; }

    /// <summary>
    /// 기본 시간(분)
    /// </summary>
    public short BaseMin { get; set; }

    /// <summary>
    /// 기본 요금(원)
    /// </summary>
    public int BaseFee { get; set; }

    /// <summary>
    /// 추가 단위 시간(분)
    /// </summary>
    public short? AddUnitMin { get; set; }

    /// <summary>
    /// 추가 요금(원)
    /// </summary>
    public int? AddFee { get; set; }

    /// <summary>
    /// 1일 주차권 요금(원). 1일 최대 요금은 DAY_MAX_FEE
    /// </summary>
    public int? DayPassFee { get; set; }

    /// <summary>
    /// 1일 최대 요금(원). 누적 요금의 상한
    /// </summary>
    public int? DayMaxFee { get; set; }

    /// <summary>
    /// 전체 주차 면수
    /// </summary>
    public short? TotalSpaces { get; set; }

    /// <summary>
    /// 실시간 주차대수 API 주차장 코드
    /// </summary>
    public string? RealtimeRef { get; set; }

    /// <summary>
    /// 마지막 조회 빈자리 수
    /// </summary>
    public short? AvailableSpaces { get; set; }

    /// <summary>
    /// 빈자리 마지막 조회 일시
    /// </summary>
    public DateTime? AvailableUpdateDt { get; set; }

    /// <summary>
    /// 표준데이터 주차장 관리번호
    /// </summary>
    public string? SourceRef { get; set; }

    /// <summary>
    /// 요금·운영 시간을 고른 출처 (STANDARD, SEOUL)
    /// </summary>
    public string? FeeSource { get; set; }

    /// <summary>
    /// 고른 출처의 기준일
    /// </summary>
    public DateTime? FeeBaseDate { get; set; }

    /// <summary>
    /// 서울시 공영주차장 코드(PKLT_CD). 연결 안 되면 NULL
    /// </summary>
    public string? SeoulRef { get; set; }

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

    /// <summary>
    /// 살아 있는 행 표시 (UNIQUE용)
    /// </summary>
    public sbyte? ActiveFlag { get; set; }

    public virtual Area Area { get; set; } = null!;

    public virtual ICollection<PickupPlan> PickupPlans { get; set; } = new List<PickupPlan>();
}
