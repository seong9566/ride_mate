-- 아이 생년월일(BIRTH_DATE) 추가 — 이 칸을 넣기 전에 만든 아이 행이 있어 NULL을 허용한다
-- 정본: backend/docs/architecture/schema.md

ALTER TABLE CHILD
    ADD COLUMN BIRTH_DATE DATE NULL COMMENT '생년월일. NULL은 이 칸을 넣기 전에 만든 행이다' AFTER SEND_CHANNEL;
