-- 운전자 이름: 실명이 아닌 형식 없는 문자열로 바꿈(설명만 변경, 타입·NULL 그대로)
-- 정본: backend/docs/architecture/schema.md · ADR-0014

ALTER TABLE USER_INFO
    MODIFY COLUMN USER_NAME VARCHAR(50) NOT NULL COMMENT '운전자 이름';
