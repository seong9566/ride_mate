using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Schedule;

public partial class ScheduleRepository
{
    /// <summary>
    /// 학원 등록. 저장 뒤 model의 학원 ID가 채워진다.
    /// </summary>
    public async Task<bool> AddAcademyAsync(Academy model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Academies.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 좌표가 없는 살아 있는 학원에 학원가·좌표를 채운다. 이미 좌표가 있으면 바꾸지 않고 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateAcademyLocationAsync(int academyId, int areaId, decimal lat, decimal lng,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            // 좌표가 없는 행만 채운다 — 다른 요청이 먼저 채웠으면 덮어쓰지 않는다
            var affected = await _context.Academies
                .Where(x => x.AcademyId == academyId
                            && x.DeleteYn == false
                            && x.Lat == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AreaId, areaId)
                    .SetProperty(x => x.Lat, lat)
                    .SetProperty(x => x.Lng, lng)
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }

    /// <summary>
    /// 시간표와 그 요일 행을 한 번에 등록한다. 저장 뒤 model의 시간표 ID가 채워진다.
    /// </summary>
    public async Task<bool> AddTimetableAsync(Timetable model, IEnumerable<TimetableDay> days,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var day in days)
                model.TimetableDays.Add(day); // 시간표를 먼저 넣고 요일 행의 시간표 ID는 EF가 채운다

            await _context.Timetables.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }
}
