-- T55 픽업 장소 고르기: 시간표별 "우리 픽업 장소"와 학원 기준 지점 한 줄 안내
-- 정본: backend/docs/architecture/schema.md

-- 부모가 고른 기본 픽업 지점. NULL이면 학원 후보의 PRIORITY 순으로 배정한다.
-- 그 학원의 살아 있는 후보(ACADEMY_PICKUP_SPOT)인지는 Service에서 확인한다(제약으로 걸 수 없다).
-- 시간표의 학원을 바꾸면 NULL로 되돌린다. 기존 행은 NULL이라 동작이 그대로다.
ALTER TABLE TIMETABLE
    ADD COLUMN PICKUP_SPOT_ID INT NULL COMMENT '우리 픽업 장소(부모가 고른 기본 픽업 지점 ID)' AFTER END_TIME,
    ADD INDEX IX_TIMETABLE_03 (PICKUP_SPOT_ID),
    ADD CONSTRAINT FK_TIMETABLE_03 FOREIGN KEY (PICKUP_SPOT_ID) REFERENCES PICKUP_SPOT (PICKUP_SPOT_ID);

-- 학원에서 본 지점 안내(예: 길 건너야 해요, 22시 전후 붐빔). 운영팀이 넣는다.
ALTER TABLE ACADEMY_PICKUP_SPOT
    ADD COLUMN NOTE VARCHAR(100) NULL COMMENT '학원 기준 지점 한 줄 안내' AFTER PRIORITY;
