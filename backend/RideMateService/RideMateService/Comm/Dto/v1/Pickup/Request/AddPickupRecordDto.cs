using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Pickup.Request;

/// <summary>
/// 픽업 완료 기록 저장 요청. 집에 도착해 "오늘 픽업 완료!" 화면에서 저장할 때 보낸다. 같은 일정·요일의 기록은 덮어쓴다
/// </summary>
public class AddPickupRecordDto
{
    /// <summary>
    /// 기록할 시간표 ID. 시간표 목록의 scheduleSeq
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? scheduleSeq { get; set; }

    /// <summary>
    /// 기록할 요일. 1(월)~7(일)
    /// </summary>
    [Required]
    [Range(1, 7)]
    public int? dayOfWeek { get; set; }

    /// <summary>
    /// 집 도착 시각. 앱의 현재 시각이고 오프셋 없는 KST 로컬 시각이다. 서버 현재 시각 + 1분보다 늦거나 24시간보다 이르면 400이다
    /// </summary>
    [Required]
    public DateTime? arriveDt { get; set; }

    /// <summary>
    /// 총 소요 분
    /// </summary>
    [Required]
    [Range(0, 1440)]
    public int? totalMin { get; set; }

    /// <summary>
    /// 대기 분. 총 소요 분보다 클 수 없다
    /// </summary>
    [Required]
    [Range(0, 1440)]
    public int? waitMin { get; set; }

    /// <summary>
    /// 출발 장소 이름. 앞뒤 공백을 지운 뒤 1~100자
    /// </summary>
    [Required]
    [TrimmedMaxLength(100)]
    public string? departName { get; set; }

    /// <summary>
    /// 출발 장소 위도
    /// </summary>
    [Required]
    [Range(-90.0, 90.0)]
    public decimal? departLat { get; set; }

    /// <summary>
    /// 출발 장소 경도
    /// </summary>
    [Required]
    [Range(-180.0, 180.0)]
    public decimal? departLng { get; set; }

    /// <summary>
    /// 출발 시각. 탑승 시각보다 늦을 수 없다
    /// </summary>
    [Required]
    public DateTime? departDt { get; set; }

    /// <summary>
    /// 탑승 장소 이름. 앞뒤 공백을 지운 뒤 1~100자
    /// </summary>
    [Required]
    [TrimmedMaxLength(100)]
    public string? boardName { get; set; }

    /// <summary>
    /// 탑승 장소 위도
    /// </summary>
    [Required]
    [Range(-90.0, 90.0)]
    public decimal? boardLat { get; set; }

    /// <summary>
    /// 탑승 장소 경도
    /// </summary>
    [Required]
    [Range(-180.0, 180.0)]
    public decimal? boardLng { get; set; }

    /// <summary>
    /// 탑승 시각. 출발 시각 이상, 집 도착 시각 이하
    /// </summary>
    [Required]
    public DateTime? boardDt { get; set; }

    /// <summary>
    /// 다음부터 묻지 않고 저장. 앱이 이 값으로 그 경로에 대해 다음부터 묻지 않을지 정한다
    /// </summary>
    [Required]
    public bool? autoSave { get; set; }
}
