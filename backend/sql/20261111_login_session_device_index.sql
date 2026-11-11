-- T10 차 계정 목록: 기기 번호로 살아 있는 차량 세션을 찾는 인덱스
-- 정본: backend/docs/architecture/schema.md

ALTER TABLE LOGIN_SESSION
    ADD INDEX IX_LOGIN_SESSION_03 (DEVICE_ID, SESSION_TYPE);
