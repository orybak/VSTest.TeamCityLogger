VSTest.TeamCityLogger
=====================

Enables TeamCity to display output when tests are run through VSTest.console.exe

**Note:** VSTest.TeamCityLogger needs at least VS2012.1 (Visual Studio 2012 Update 1) installed

#Usage

Put VSTest.TeamCityLogger into `C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\Extensions`

    vstest.console.exe tests.dll /logger:TeamCity

Initial project by [JakeGinnivan](https://github.com/JakeGinnivan/VSTest.TeamCityLogger)
    
