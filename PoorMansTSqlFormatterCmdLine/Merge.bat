"c:\Program Files\Microsoft\ILMerge\ILMerge.exe" /t:winexe /out:%1SqlFormatter.exe %1SqlFormatterExeAssembly.exe %1PoorMansTSqlFormatterLib.dll %1NDesk.Options.dll
del %1NDesk.Options.dll
del %1PoorMansTSqlFormatterLib.dll
del %1PoorMansTSqlFormatterLib.pdb
del %1SqlFormatterExeAssembly.exe
del %1SqlFormatterExeAssembly.pdb
