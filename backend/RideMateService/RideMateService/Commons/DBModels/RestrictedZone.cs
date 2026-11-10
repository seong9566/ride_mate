using System;
using System.Collections.Generic;
using MySql.Data.Types;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 제외·단속 구역
/// </summary>
public partial class RestrictedZone
{
    /// <summary>
    /// 제외·단속 구역 ID
    /// </summary>
    public int RestrictedZoneId { get; set; }

    /// <summary>
    /// 학원가 ID
    /// </summary>
    public int AreaId { get; set; }

    /// <summary>
    /// 구역 유형 (SCHOOL_ZONE, NO_PARKING, INTERSECTION, CROSSWALK, BUS_STOP, FIRE_HYDRANT, YELLOW_DOUBLE, YELLOW_SINGLE, CCTV, ACCIDENT_SPOT, RESIDENT_PARKING)
    /// </summary>
    public string ZoneType { get; set; } = null!;

    /// <summary>
    /// 제외·단속 구역 점·선·면
    /// </summary>
    public MySqlGeometry Geom { get; set; }

    /// <summary>
    /// 제외 거리(m)
    /// </summary>
    public short BufferM { get; set; }

    /// <summary>
    /// 적용 요일 유형 (WEEKDAY, SAT, HOLIDAY)
    /// </summary>
    public string? DayType { get; set; }

    /// <summary>
    /// 적용 시작 시각
    /// </summary>
    public TimeSpan? ActiveStartTime { get; set; }

    /// <summary>
    /// 적용 종료 시각
    /// </summary>
    public TimeSpan? ActiveEndTime { get; set; }

    /// <summary>
    /// 원천 데이터 이름
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// 원천 데이터 관리번호
    /// </summary>
    public string? SourceRef { get; set; }

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
}
