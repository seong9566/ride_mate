using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm.Dto.v1.Schedule.Request;

/// <summary>
/// 시간표 등록 요청
/// </summary>
public class AddScheduleDto
{
    /// <summary>
    /// 부모가 지은 학원 이름. 앞뒤 공백을 지운 뒤 1~20자, 한글·영문·숫자·공백과 특수문자 ( ) - · 만 쓴다(Service에서 검사)
    /// </summary>
    [Required]
    public string? displayName { get; set; }

    /// <summary>
    /// 학원 장소. 앱 SDK의 장소 검색 결과를 그대로 보낸다
    /// </summary>
    [Required]
    public SchedulePlaceDto? place { get; set; }

    /// <summary>
    /// 요일 목록. 1(월)~7(일), 1~7개, 중복 없음(Service에서 검사)
    /// </summary>
    [Required]
    public List<int>? days { get; set; }

    /// <summary>
    /// 하원 시각. HH:mm, 14:00~23:30, 5분 단위(Service에서 검사)
    /// </summary>
    [Required]
    [RegularExpression(@"^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    public string? endTime { get; set; }

    /// <summary>
    /// 같은 요일에 시간이 가까운 시간표가 있다는 경고(103)를 본 뒤 그래도 저장할 때 true. 빠지면 false
    /// </summary>
    public bool? confirmOverlap { get; set; }
}

/// <summary>
/// 시간표의 학원 장소
/// </summary>
public class SchedulePlaceDto
{
    /// <summary>
    /// 학원 이름. 앞뒤 공백을 지운 뒤 1~100자(Service에서 검사)
    /// </summary>
    [Required]
    public string? name { get; set; }

    /// <summary>
    /// 학원 주소. 앞뒤 공백을 지운 뒤 1~200자(Service에서 검사)
    /// </summary>
    [Required]
    public string? address { get; set; }

    /// <summary>
    /// 위도. 경도와 함께 보내거나 둘 다 뺀다
    /// </summary>
    [Range(-90.0, 90.0)]
    public decimal? lat { get; set; }

    /// <summary>
    /// 경도. 위도와 함께 보내거나 둘 다 뺀다
    /// </summary>
    [Range(-180.0, 180.0)]
    public decimal? lng { get; set; }
}
