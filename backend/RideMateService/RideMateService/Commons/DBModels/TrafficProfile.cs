using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 학원가 요일·시간대 혼잡 집계
/// </summary>
public partial class TrafficProfile
{
    /// <summary>
    /// 혼잡 집계 ID
    /// </summary>
    public int TrafficProfileId { get; set; }

    /// <summary>
    /// 학원가 코드 (DAECHI, PYEONGCHON)
    /// </summary>
    public string AreaCode { get; set; } = null!;

    /// <summary>
    /// 요일 번호 (월 1~일 7)
    /// </summary>
    public sbyte DayOfWeek { get; set; }

    /// <summary>
    /// 자정부터 지난 분 (0~1435, 5분 단위)
    /// </summary>
    public short SlotMin { get; set; }

    /// <summary>
    /// 집계 소스 (COLLECTED, MONTHLY)
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// 집계 속도(km/h)
    /// </summary>
    public decimal SpeedKmh { get; set; }

    /// <summary>
    /// 집계 표본 일수. 근거가 없으면 0
    /// </summary>
    public short SampleDays { get; set; }

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
}
