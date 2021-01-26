net use a: \\testhost\c$ /user:gabor /PERSISTENT:NO

robocopy ..\..\..\Server\WatchDog\bin\Debug "a:\Program Files (x86)\Watchdog" /MIR /NDL
pause


