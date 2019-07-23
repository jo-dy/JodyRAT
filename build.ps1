# Builds the agent and server using csc.exe

& $env:windir\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:pdf.ico /platform:x86 /main:Agent /out:agent.exe /doc:agent.xml Agent.cs
& $env:windir\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /target:exe /platform:x86 /main:Server /out:server.exe /doc:server.xml Server.cs
ls *.exe | ft