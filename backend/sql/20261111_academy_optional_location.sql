-- T14 시간표 저장: 학원 좌표를 선택 입력으로 바꾸고 주소를 필수로
-- 정본: backend/docs/architecture/schema.md

-- 앱(Pleos SDK)이 주는 이름·주소는 항상 있고 좌표는 없을 수 있다. 좌표가 없으면 학원가(AREA_ID)도 정할 수 없다.
-- 이 칸을 바꾸기 전에 ADDRESS가 NULL인 행이 있으면 NOT NULL로 바꿀 수 없다. 로컬 ACADEMY는 0행이다.
ALTER TABLE ACADEMY
    MODIFY COLUMN AREA_ID INT NULL COMMENT '학원가 ID',
    MODIFY COLUMN ADDRESS VARCHAR(200) NOT NULL COMMENT '학원 주소',
    MODIFY COLUMN LAT DECIMAL(10,7) NULL COMMENT '학원 위도',
    MODIFY COLUMN LNG DECIMAL(10,7) NULL COMMENT '학원 경도',
    ADD CONSTRAINT CK_ACADEMY_01 CHECK ((LAT IS NULL) = (LNG IS NULL)),
    ADD CONSTRAINT CK_ACADEMY_02 CHECK (AREA_ID IS NULL OR LAT IS NOT NULL);
