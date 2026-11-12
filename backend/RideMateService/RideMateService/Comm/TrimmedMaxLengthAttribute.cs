using System.ComponentModel.DataAnnotations;

namespace RideMateService.Comm;

/// <summary>
/// 앞뒤 공백을 지운 문자열의 길이가 최대 길이 이하인지 검사한다. null은 통과한다
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TrimmedMaxLengthAttribute : ValidationAttribute
{
    private readonly int _maxLength;

    public TrimmedMaxLengthAttribute(int maxLength)
    {
        _maxLength = maxLength;
    }

    public override bool IsValid(object? value)
    {
        return value is not string text || text.Trim().Length <= _maxLength;
    }
}
