# RideMate

현대자동차 차량용 앱 개발 공모전 출품을 위한 모노레포입니다.
모빌리티 앱과 백엔드 서버를 한 저장소에서 함께 관리합니다.

## 팀 구성

| 이름 | 역할 | 이메일 |
|------|------|--------|
| 배민주 (대표자) | UI/UX 디자인 | dbaeminju@gmail.com |
| 김승은 | UI/UX 디자인 | sseungg01@gmail.com |
| 이윤주 | 기획 / PM | yjlove912@gmail.com |
| 황예원 | 모빌리티 앱 개발 | yewon303@naver.com |
| 이현성 | 백엔드 개발 | dlgustjd9566@gmail.com |

## 기술 스택

| 영역 | 기술 |
|------|------|
| 모빌리티 앱 | Kotlin |
| 서버 | ASP.NET Core Web API (C#) |

## 레포 구조

```
ride_mate/
├── mobility/       # 모빌리티 앱 (Kotlin, Gradle 루트)
├── backend/        # 백엔드 API 서버 (ASP.NET Core Web API, .sln 루트)
├── docs/
│   └── api/        # API 명세 (OpenAPI) — 앱·서버 간 계약
├── .gitignore      # OS·IDE 파일 (모듈별 무시 규칙은 각 폴더의 .gitignore)
└── README.md
```

## 시작하기

각 모듈의 빌드·실행 방법은 프로젝트 생성 후 해당 디렉터리의 README에 정리합니다.

- 모빌리티 앱: [`mobility/`](mobility/)
- 서버: [`backend/`](backend/)
