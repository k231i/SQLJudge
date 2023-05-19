#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 727
EXPOSE 420

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["SQLJudge.ApiServer/SQLJudge.ApiServer.csproj", "SQLJudge.ApiServer/"]
RUN dotnet restore "SQLJudge.ApiServer/SQLJudge.ApiServer.csproj"
COPY . .
WORKDIR "/src/SQLJudge.ApiServer"
RUN dotnet build "SQLJudge.ApiServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SQLJudge.ApiServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SQLJudge.ApiServer.dll"]