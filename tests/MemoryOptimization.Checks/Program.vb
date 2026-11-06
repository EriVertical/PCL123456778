Imports System
Imports System.ComponentModel
Imports System.Runtime.InteropServices

Module Program
    Private Checks As Integer
    Sub Main(Args As String())
#If NET10_0 Then
        If Args.Length <> 1 Then Throw New ArgumentException("Pass the absolute path to the Linux native memory fixture library.")
        Dim Handle = NativeLibrary.Load(Args(0))
        NativeLibrary.SetDllImportResolver(GetType(SystemMemoryOptimization).Assembly,
            Function(Name, Assembly, SearchPath) If(Name = "kernel32.dll" OrElse Name = "advapi32.dll", Handle, IntPtr.Zero))
        Dim Configure = Marshal.GetDelegateForFunctionPointer(Of ConfigureDelegate)(NativeLibrary.GetExport(Handle, "ConfigureMemory"))
        Dim Counter = Marshal.GetDelegateForFunctionPointer(Of CounterDelegate)(NativeLibrary.GetExport(Handle, "MemoryCounter"))

        For Each Mode In {0, 7, 8}
            Configure(Mode)
            SystemMemoryOptimization.TrimFileCache()
            Check(Counter(0) = 1, "exactly one cache flush with SIZE_T -1 and unchanged cache policy")
            Check(Counter(1) = 1, "token closed on success")
            Check(Counter(2) = If(Mode = 7, 0, 1), "restore changed privileges, leave pre-enabled privilege unchanged")
            Check(Counter(4) = If(Mode = 8, 1, 0), "restore the original attributes rather than always disabling")
        Next

        For Mode = 1 To 6
            Configure(Mode)
            Dim Failed = False
            Try
                SystemMemoryOptimization.TrimFileCache()
            Catch ex As Win32Exception
                Failed = True
                Check(ex.NativeErrorCode = If(Mode = 2, 1313, If(Mode = 4, 1300, 5)), "preserve native failure code")
            End Try
            Check(Failed, "failed privilege or flush operation must not report success")
            Check(Counter(1) = If(Mode = 1, 0, 1), "close an opened token on every failure")
            Check(Counter(0) = If(Mode >= 5, 1, 0), "do not flush without the requested privilege")
            Check(Counter(2) = If(Mode >= 5, 1, 0), "restore permissions even after cache flush fails")
        Next
        Console.WriteLine($"Passed {Checks} native memory regression checks. Windows APIs were simulated; real UAC and memory reclamation were not exercised.")
#Else
        Throw New PlatformNotSupportedException("net48 is a compile compatibility check. Run the simulated native checks on Linux with net10.0.")
#End If
    End Sub
#If NET10_0 Then
    Private Delegate Sub ConfigureDelegate(Mode As Integer)
    Private Delegate Function CounterDelegate(Kind As Integer) As Integer
    Private Sub Check(Condition As Boolean, Name As String)
        If Not Condition Then Throw New Exception("Failed: " & Name)
        Checks += 1
    End Sub
#End If
End Module
