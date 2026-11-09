namespace RideMateService.Comm;

/// <summary>
/// 커서 페이지네이션 DTO
/// </summary>
public class CursorResult<T>
{
    public List<T> items { get; set; } = new();

    public int? lastCursor { get; set; }

    public bool hasMore { get; set; } = false;
}
