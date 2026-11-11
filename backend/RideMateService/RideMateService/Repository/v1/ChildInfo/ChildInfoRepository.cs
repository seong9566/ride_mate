using System.Data;
using RideMateService.Comm.Logger;
using RideMateService.Commons.Repository;

namespace RideMateService.Repository.v1.ChildInfo;

public partial class ChildInfoRepository : IChildInfoRepository
{
    private readonly RideMateContext _context; // EF Core
    private readonly IDbConnection _dapper;    // Dapper
    private readonly ILogService _logService;

    public ChildInfoRepository(RideMateContext context, IDbConnection dapper, ILogService logService)
    {
        this._context = context;
        this._dapper = dapper;
        this._logService = logService;
    }
}
