using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 픽업 완료 기록
/// </summary>
public partial class PickupRecord
{
    /// <summary>
    /// 픽업 완료 기록 ID
    /// </summary>
    public int PickupRecordId { get; set; }

    /// <summary>
    /// 계정 ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 시간표 요일 ID
    /// </summary>
    public int TimetableDayId { get; set; }

    /// <summary>
    /// 집 도착 시각
    /// </summary>
    public DateTime ArriveDt { get; set; }

    /// <summary>
    /// 총 소요 분
    /// </summary>
    public int TotalMin { get; set; }

    /// <summary>
    /// 대기 분
    /// </summary>
    public int WaitMin { get; set; }

    /// <summary>
    /// 출발 장소 이름. 지우면 빈 문자열
    /// </summary>
    public string DepartName { get; set; } = null!;

    /// <summary>
    /// 출발 장소 위도. 지우면 NULL
    /// </summary>
    public decimal? DepartLat { get; set; }

    /// <summary>
    /// 출발 장소 경도. 지우면 NULL
    /// </summary>
    public decimal? DepartLng { get; set; }

    /// <summary>
    /// 출발 시각
    /// </summary>
    public DateTime DepartDt { get; set; }

    /// <summary>
    /// 탑승 장소 이름. 지우면 빈 문자열
    /// </summary>
    public string BoardName { get; set; } = null!;

    /// <summary>
    /// 탑승 장소 위도. 지우면 NULL
    /// </summary>
    public decimal? BoardLat { get; set; }

    /// <summary>
    /// 탑승 장소 경도. 지우면 NULL
    /// </summary>
    public decimal? BoardLng { get; set; }

    /// <summary>
    /// 탑승 시각
    /// </summary>
    public DateTime BoardDt { get; set; }

    /// <summary>
    /// 다음부터 묻지 않고 저장 여부
    /// </summary>
    public bool AutoSaveYn { get; set; }

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

    public virtual TimetableDay TimetableDay { get; set; } = null!;

    public virtual UserInfo User { get; set; } = null!;
}
