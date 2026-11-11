using System.Data;
using RideMateService.Comm.Logger;
using RideMateService.Commons.Repository;

namespace RideMateService.Repository.v1.Schedule;

public partial class ScheduleRepository : IScheduleRepository
{
    private readonly RideMateContext _context; // EF Core
    private readonly IDbConnection _dapper;    // Dapper
    private readonly ILogService _logService;

    public ScheduleRepository(RideMateContext context, IDbConnection dapper, ILogService logService)
    {
        this._context = context;
        this._dapper = dapper;
        this._logService = logService;
    }
}
