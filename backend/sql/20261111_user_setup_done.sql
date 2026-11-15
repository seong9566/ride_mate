-- T15 초기 설정 4/4: 초기 설정 완료 시각
-- 정본: backend/docs/architecture/schema.md

-- 4/4 완료 API가 채운다. NULL이면 로그인 응답의 setup_required가 true다.
-- 이 칸을 넣기 전에 만든 계정은 NULL이라 다음 로그인에 초기 설정으로 간다.
ALTER TABLE USER_INFO
    ADD COLUMN SETUP_DONE_DT DATETIME NULL COMMENT '초기 설정 완료 시각' AFTER WAKE_BEFORE_MIN;
