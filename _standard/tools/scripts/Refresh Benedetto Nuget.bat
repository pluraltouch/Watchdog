cd ..\..\..\JazzTuner\tools\scripts
call "Pack-Debug.bat"
cd ..\..\..\Levin\tools\scripts
call "clean-all.bat"
call "restore nuget.bat"