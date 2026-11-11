FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RideMateService.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# 베이스 이미지에 tzdata가 있어 TZ 환경변수만 주면 DateTime.Now가 KST가 된다
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# LogService가 /app/SystemLog에 파일 로그를 쓴다. 앱 파일은 root 소유로 두고 이 폴더만 app 사용자에게 연다
RUN mkdir -p SystemLog && chown $APP_UID SystemLog
USER $APP_UID
ENTRYPOINT ["dotnet", "RideMateService.dll"]
