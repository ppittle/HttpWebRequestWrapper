::@echo off

set lowLevelDllFullPath=%1
set lowLevelDll=%~n1%.dll
set lowLevelDllXml=%~n1%.xml
set lowLevelDllPdb=%~n1%.pdb
set lowLevelAssembly=%~n1%.asm
set lowLevelResource=%~n1%.res
set solutionDirectory=%2

::ECHO.fullPath: %lowLevelDllFullPath%
::ECHO.dll: %lowLevelDll%
::ECHO.lowLevelAssembly %lowLevelAssembly%

:: dissassemble lowLevelDll
%solutionDirectory%..\build\ildasm.exe %lowLevelDllFullPath% /out=%lowLevelAssembly%

:: change HttpWebRequestWrapper base class to HttpWebRequestWrapper
%solutionDirectory%..\build\ReWriteTextInFile.exe .\%lowLevelAssembly% ^
    "extends [System]System.Net.WebRequest" ^
    "extends [System]System.Net.HttpWebRequest"

%solutionDirectory%..\build\ReWriteTextInFile.exe .\%lowLevelAssembly% ^
    "call       instance void [System]System.Net.WebRequest" ^
    "call       instance void [System]System.Net.HttpWebRequest"

:: reassembly lowLevelDll
%solutionDirectory%..\build\ilasm /dll %lowLevelAssembly%

:: copy updated lowLevelDll, xml and pdb to solution directory
copy /Y %lowLevelDll% %solutionDirectory%%lowLevelDll% /B
copy /Y %lowLevelDllXml% %solutionDirectory%%lowLevelDllXml% /B
copy /Y %lowLevelDllPdb% %solutionDirectory%%lowLevelDllPdb% /B

:: cleanup local files
del %lowLevelAssembly%
del %lowLevelResource%