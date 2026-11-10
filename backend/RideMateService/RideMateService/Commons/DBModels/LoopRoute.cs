using System;
using System.Collections.Generic;

namespace RideMateService.Commons.DBModels;

/// <summary>
/// 한 바퀴 경로
/// </summary>
public partial class LoopRoute
{
    /// <summary>
    /// 한 바퀴 경로 ID
    /// </summary>
    public int LoopRouteId { get; set; }

    /// <summary>
    /// 픽업 지점 ID
    /// </summary>
    public int PickupSpotId { get; set; }

    /// <summary>
    /// 한 바퀴 소요 시간(초)
    /// </summary>
    public short DurationSec { get; set; }

    /// <summary>
    /// 경로 거리(m)
    /// </summary>
    public short DistanceM { get; set; }

    /// <summary>
    /// 우회전 수
    /// </summary>
    public sbyte RightTurnCnt { get; set; }

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

    public virtual PickupSpot PickupSpot { get; set; } = null!;
}
