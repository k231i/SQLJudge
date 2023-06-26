# SQL Judge - Server App

## Installation

1. ```
    git clone https://github.com/k231i/SQLJudge.git
    ```
2. ```
    cd SQLJudge
    ```
3. ```
    dotnet restore SQLJudge.ApiServer/SQLJudge.ApiServer.csproj
    ```
4. ```
    dotnet build --no-restore SQLJudge.ApiServer/SQLJudge.ApiServer.csproj
    ```
5. ```
    dotnet publish SQLJudge.ApiServer/SQLJudge.ApiServer.csproj -c Release -o sqljudgeapi
    ```
6. ```
    cd sqljudgeapi
    ```
7. Edit `appsettings.json` according to the connection strings of your databases.
   
## Running

```
dotnet SQLJudge.ApiServer.dll
```