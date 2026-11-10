using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 공휴일
/// </summary>
public partial class Holiday
{
    /// <summary>
    /// 공휴일 ID
    /// </summary>
    public int HolidayId { get; set; }

    /// <summary>
    /// 공휴일 날짜
    /// </summary>
    public DateTime HolidayDate { get; set; }

    /// <summary>
    /// 공휴일 이름
    /// </summary>
    public string HolidayName { get; set; } = null!;

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
}
