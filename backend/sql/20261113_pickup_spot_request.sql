-- T57 픽업 장소 추가 요청: 후보 지점에 원하는 곳이 없을 때 부모가 운영팀에 남기는 요청
-- 정본: backend/docs/architecture/schema.md

-- 계정·아이를 가리키는 칸을 두지 않는다(ADR-0008). 운영팀은 학원·위치·메모만 보고 현장 검증 후보로 쓴다.
CREATE TABLE PICKUP_SPOT_REQUEST (
    PICKUP_SPOT_REQUEST_ID INT           NOT NULL AUTO_INCREMENT COMMENT '픽업 장소 추가 요청 ID',
    ACADEMY_ID             INT           NOT NULL COMMENT '학원 ID',
    AREA_ID                INT           NULL COMMENT '학원가 ID. 학원에 좌표가 없으면 NULL',
    LAT                    DECIMAL(10,7) NULL COMMENT '원하는 위치 위도',
    LNG                    DECIMAL(10,7) NULL COMMENT '원하는 위치 경도',
    PLACE_NAME             VARCHAR(100)  NULL COMMENT '원하는 장소 이름',
    MEMO                   VARCHAR(200)  NULL COMMENT '요청 메모',
    STATUS                 VARCHAR(20)   NOT NULL COMMENT '처리 상태 (REQUESTED, ACCEPTED, REJECTED)',
    CREATE_DT              DATETIME      NOT NULL COMMENT '생성 일시',
    CREATE_USER            VARCHAR(50)   NOT NULL COMMENT '생성자',
    UPDATE_DT              DATETIME      NOT NULL COMMENT '수정 일시',
    UPDATE_USER            VARCHAR(50)   NOT NULL COMMENT '수정자',
    DELETE_YN              TINYINT(1)    NOT NULL DEFAULT 0 COMMENT '삭제 여부',
    PRIMARY KEY (PICKUP_SPOT_REQUEST_ID),
    INDEX IX_PICKUP_SPOT_REQUEST_01 (ACADEMY_ID),
    INDEX IX_PICKUP_SPOT_REQUEST_02 (AREA_ID),
    CONSTRAINT FK_PICKUP_SPOT_REQUEST_01 FOREIGN KEY (ACADEMY_ID) REFERENCES ACADEMY (ACADEMY_ID),
    CONSTRAINT FK_PICKUP_SPOT_REQUEST_02 FOREIGN KEY (AREA_ID) REFERENCES AREA (AREA_ID),
    CONSTRAINT CK_PICKUP_SPOT_REQUEST_01 CHECK ((LAT IS NULL) = (LNG IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='픽업 장소 추가 요청';
