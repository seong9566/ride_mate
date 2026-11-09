using System.Data;
using Dapper;

namespace RideMateService.Comm;

public class DateOnlyHandler : SqlMapper.ITypeHandler
{
    public object? Parse(Type destinationType, object value)
        => DateOnly.FromDateTime((DateTime)value);

    public void SetValue(IDbDataParameter parameter, object value)
        => parameter.Value = ((DateOnly)value).ToDateTime(TimeOnly.MinValue);
}
