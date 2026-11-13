-- 하원 시각 범위 CHECK(14:00~23:30) 제거. 범위는 Service 검증(ScheduleService)으로만 둔다.
-- 자정을 넘는 픽업 처리(T88)는 MVP 뒤로 미뤘다. 5분 단위 CHECK(CK_TIMETABLE_02)는 그대로 둔다.
-- 정본: backend/docs/architecture/schema.md
ALTER TABLE TIMETABLE
    DROP CHECK CK_TIMETABLE_01;
