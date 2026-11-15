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
    /// 학원별 픽업 지점 후보를 한 번에 등록한다.
    /// </summary>
    public async Task<bool> AddAcademyPickupSpotsAsync(IEnumerable<AcademyPickupSpot> models,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.AcademyPickupSpots.AddRangeAsync(models, cancellationToken).ConfigureAwait(false);

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
    /// 살아 있는 요일 행의 픽업 스위치와 수정 감사 컬럼만 바꾼다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateScheduleDayPickupAsync(int scheduleDaySeq, bool pickupYn, DateTime updateDt,
        string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await _context.TimetableDays
                .Where(x => x.TimetableDayId == scheduleDaySeq
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PickupYn, pickupYn)
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
    /// 시간표의 살아 있는 요일 행 픽업 스위치를 pickupOffDays에 든 요일은 끄고 나머지는 켠다. 값이 바뀌는 행만 쓴다.
    /// </summary>
    public async Task SetScheduleDaysPickupAsync(int timetableId, IReadOnlyCollection<int> pickupOffDays,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            var offDays = pickupOffDays.Select(x => (sbyte)x).ToList();
            // 지금 값이 원하는 값과 다른 행(켜진 끌 요일, 꺼진 켤 요일)만 고른다
            await _context.TimetableDays
                .Where(x => x.TimetableId == timetableId
                            && x.DeleteYn == false
                            && x.PickupYn == offDays.Contains(x.DayOfWeek))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PickupYn, x => !offDays.Contains(x.DayOfWeek))
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);
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
    /// 시간표의 우리 픽업 장소(PICKUP_SPOT_ID)와 수정 감사 컬럼만 바꾼다. null이면 지운다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateSchedulePickupSpotAsync(int timetableId, int? pickupSpotId, DateTime updateDt,
        string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            var affected = await _context.Timetables
                .Where(x => x.TimetableId == timetableId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PickupSpotId, pickupSpotId)
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
    /// 시간표를 지운다. 날짜별 예외 → 픽업 완료 기록 → 요일 → 시간표 순으로 표마다 UPDATE 한 번씩 DELETE_YN을 켜고, 시간표의 DISPLAY_NAME은 빈 문자열로 바꾼다.
    /// 시간표 행이 바뀌지 않았으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> DeleteScheduleAsync(int timetableId, IReadOnlyCollection<int> timetableDayIds,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            // schema.md "아이·계정 삭제" 2~4행 순서다. 삭제된 행은 ACTIVE_FLAG가 NULL이라 살아 있는 행 UNIQUE에 걸리지 않는다
            if (timetableDayIds.Count > 0)
            {
                await _context.TimetableExceptions
                    .Where(x => timetableDayIds.Contains(x.TimetableDayId)
                                && x.DeleteYn == false)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DeleteYn, true)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                    .ConfigureAwait(false);

                // 그 요일들의 픽업 완료 기록을 비운다: 삭제 표시하고 장소 이름은 빈 문자열, 좌표는 NULL로 [ADR-0027]
                await _context.PickupRecords
                    .Where(x => timetableDayIds.Contains(x.TimetableDayId)
                                && x.DeleteYn == false)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DeleteYn, true)
                        .SetProperty(x => x.DepartName, string.Empty)
                        .SetProperty(x => x.BoardName, string.Empty)
                        .SetProperty(x => x.DepartLat, (decimal?)null)
                        .SetProperty(x => x.DepartLng, (decimal?)null)
                        .SetProperty(x => x.BoardLat, (decimal?)null)
                        .SetProperty(x => x.BoardLng, (decimal?)null)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                    .ConfigureAwait(false);
            }

            await _context.TimetableDays
                .Where(x => x.TimetableId == timetableId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DeleteYn, true)
                    .SetProperty(x => x.UpdateDt, updateDt)
                    .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                .ConfigureAwait(false);

            var affected = await _context.Timetables
                .Where(x => x.TimetableId == timetableId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DeleteYn, true)
                    .SetProperty(x => x.DisplayName, string.Empty)
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
    /// 시간표를 편집한다. 뺀 요일의 날짜별 예외 → 뺀 요일의 픽업 완료 기록 → 뺀 요일 → 되살릴 요일 → 새 요일 → 시간표 순으로 쓴다.
    /// 되살린 요일은 픽업 스위치를 켜고 LAST_PICKUP_DATE는 그대로 둔다. 학원이 바뀌었으면(academyChanged) 같은 시간표 쓰기에서
    /// 우리 픽업 장소를 비운다. 시간표 행이 바뀌지 않았으면 false를 돌려준다.
    /// </summary>
    public async Task<bool> UpdateScheduleAsync(int timetableId, int academyId, bool academyChanged, string displayName, TimeSpan endTime,
        IReadOnlyCollection<int> deleteDayIds, IReadOnlyCollection<int> reviveDayIds, IReadOnlyCollection<TimetableDay> newDays,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default)
    {
        try
        {
            // 뺀 요일은 삭제와 같이 날짜별 예외 → 요일 순으로 지운다. 삭제된 행은 ACTIVE_FLAG가 NULL이라 살아 있는 행 UNIQUE에 걸리지 않는다
            if (deleteDayIds.Count > 0)
            {
                await _context.TimetableExceptions
                    .Where(x => deleteDayIds.Contains(x.TimetableDayId)
                                && x.DeleteYn == false)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DeleteYn, true)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                    .ConfigureAwait(false);

                // 그 요일들의 픽업 완료 기록을 비운다: 삭제 표시하고 장소 이름은 빈 문자열, 좌표는 NULL로 [ADR-0027]
                await _context.PickupRecords
                    .Where(x => deleteDayIds.Contains(x.TimetableDayId)
                                && x.DeleteYn == false)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DeleteYn, true)
                        .SetProperty(x => x.DepartName, string.Empty)
                        .SetProperty(x => x.BoardName, string.Empty)
                        .SetProperty(x => x.DepartLat, (decimal?)null)
                        .SetProperty(x => x.DepartLng, (decimal?)null)
                        .SetProperty(x => x.BoardLat, (decimal?)null)
                        .SetProperty(x => x.BoardLng, (decimal?)null)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                    .ConfigureAwait(false);

                await _context.TimetableDays
                    .Where(x => x.TimetableId == timetableId
                                && deleteDayIds.Contains(x.TimetableDayId)
                                && x.DeleteYn == false)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DeleteYn, true)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                    .ConfigureAwait(false);
            }

            // 다시 넣은 요일은 새 행을 만들지 않고 삭제 표시된 행을 되살린다(schema.md TIMETABLE 절)
            if (reviveDayIds.Count > 0)
            {
                await _context.TimetableDays
                    .Where(x => x.TimetableId == timetableId
                                && reviveDayIds.Contains(x.TimetableDayId)
                                && x.DeleteYn == true)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.DeleteYn, false)
                        .SetProperty(x => x.PickupYn, true)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (newDays.Count > 0)
            {
                await _context.TimetableDays.AddRangeAsync(newDays, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            var affected = await _context.Timetables
                .Where(x => x.TimetableId == timetableId
                            && x.DeleteYn == false)
                .ExecuteUpdateAsync(setters =>
                {
                    setters
                        .SetProperty(x => x.AcademyId, academyId)
                        .SetProperty(x => x.DisplayName, displayName)
                        .SetProperty(x => x.EndTime, endTime)
                        .SetProperty(x => x.UpdateDt, updateDt)
                        .SetProperty(x => x.UpdateUser, updateUser);
                    // 학원이 바뀌면 이전 학원의 우리 픽업 장소는 후보가 아니므로 비운다 [ADR-0025]
                    if (academyChanged)
                        setters.SetProperty(x => x.PickupSpotId, (int?)null);
                }, cancellationToken)
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
