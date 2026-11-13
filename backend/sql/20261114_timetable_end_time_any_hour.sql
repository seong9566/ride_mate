-- 하원 시각 DB 범위를 14:00~23:30에서 하루 전체(00:00~23:55)로 넓힌다. 업무 범위(14:00~23:30)는 Service 검증(ScheduleService)으로만 둔다.
-- TIME 타입은 24:00:00·음수도 받으므로 범위 CHECK는 지우지 않고 하루 안으로만 남긴다. 5분 단위 CHECK(CK_TIMETABLE_02)는 그대로 둔다.
-- 지우기와 다시 걸기를 한 문장으로 해 중간에 실패해도 기존 제약이 남게 한다.
-- 자정을 넘는 픽업 처리(T88)는 MVP 뒤로 미뤘다.
-- 정본: backend/docs/architecture/schema.md
ALTER TABLE TIMETABLE
    DROP CHECK CK_TIMETABLE_01,
    ADD CONSTRAINT CK_TIMETABLE_01 CHECK (END_TIME BETWEEN '00:00:00' AND '23:55:00');
