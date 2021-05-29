set ARCHS=win-x86 win-x64 win-arm linux-x64 linux-arm osx-x64
set APP_NAME=famicom-dumper
set PROJECT_PATH=FamicomDumper
set OUTPUT_DIR=release

set START_DIRECTORY=%CD%

del /F /S /Q %OUTPUT_DIR%

FOR %%a IN (%ARCHS%) do (
  dotnet publish %PROJECT_PATH% -c Release -r %%a -p:PublishSingleFile=true --no-self-contained -p:IncludeAllContentForSelfExtract=true -o %OUTPUT_DIR%\%%a\%APP_NAME%
  cd %OUTPUT_DIR%\%%a
  7z a -r -tzip -mx9 ..\%APP_NAME%-%%a.zip %APP_NAME%
  cd %START_DIRECTORY%
)

FOR %%a IN (%ARCHS%) do (
  dotnet publish %PROJECT_PATH% -c Release -r %%a -p:PublishSingleFile=true --self-contained true -p:IncludeAllContentForSelfExtract=true -o %OUTPUT_DIR%\%%a-self-contained\%APP_NAME%
  cd %OUTPUT_DIR%\%%a-self-contained
  7z a -r -tzip -mx9 ..\%APP_NAME%-%%a-self-contained.zip %APP_NAME%
  cd %START_DIRECTORY%
)
