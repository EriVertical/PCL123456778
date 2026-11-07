Imports System
Imports System.ComponentModel
Imports System.Reflection
Imports System.Runtime.InteropServices

Module Program
    Private Checks As Integer
    Sub Main(Args As String())
#If NET10_0 Then
        If Args.Length <> 1 Then Throw New ArgumentException("Pass the absolute path to the Linux native memory fixture library.")
        Dim Handle = NativeLibrary.Load(Args(0))
        NativeLibrary.SetDllImportResolver(GetType(SystemMemoryOptimization).Assembly,
            Function(Name, Assembly, SearchPath) If(Name = "kernel32.dll" OrElse Name = "advapi32.dll" OrElse Name = "psapi.dll", Handle, IntPtr.Zero))
        Dim Configure = Marshal.GetDelegateForFunctionPointer(Of ConfigureDelegate)(NativeLibrary.GetExport(Handle, "ConfigureMemory"))
        Dim Counter = Marshal.GetDelegateForFunctionPointer(Of CounterDelegate)(NativeLibrary.GetExport(Handle, "MemoryCounter"))

        Check(SystemMemoryOptimization.DescribeResult(0).Contains("未观察到可用内存增加"), "zero delta must not imply memory was reclaimed")
        Check(SystemMemoryOptimization.DescribeResult(2048).Contains("2 MB"), "positive delta is described as approximate net change")
        Check(SystemMemoryOptimization.IsEligibleProcess(42, 1, "same-user", 99, 1, "same-user", 100), "same-user same-session application eligible")
        For Each Id In {0, 4, 99, 100}
            Check(Not SystemMemoryOptimization.IsEligibleProcess(Id, 1, "same-user", 99, 1, "same-user", 100), "system/helper/caller excluded")
        Next
        Check(Not SystemMemoryOptimization.IsEligibleProcess(42, 0, "same-user", 99, 0, "same-user", 100), "session zero excluded")
        Check(Not SystemMemoryOptimization.IsEligibleProcess(42, 2, "same-user", 99, 1, "same-user", 100), "other session excluded")
        Check(Not SystemMemoryOptimization.IsEligibleProcess(42, 1, "other-user", 99, 1, "same-user", 100), "other user excluded")
        Check(Not SystemMemoryOptimization.IsEligibleProcess(42, 1, Nothing, 99, 1, "same-user", 100), "unknown process owner excluded")
        Check(Not SystemMemoryOptimization.IsEligibleProcess(42, 1, "same-user", 99, 1, Nothing, 100), "unknown current owner excluded")
        Dim PartialStatus = SystemMemoryOptimization.CacheFailed Or SystemMemoryOptimization.WorkingSetsCompleted Or SystemMemoryOptimization.ProcessesSkipped
        Dim PartialText = SystemMemoryOptimization.DescribeResult(0, True, PartialStatus)
        Check(PartialText.Contains("缓存步骤未完成"), "cache failure cannot be reported as cache success")
        Check(PartialText.Contains("已回收可访问应用的工作集"), "working-set success reported independently")
        Check(PartialText.Contains("已跳过"), "skipped processes must be disclosed")

        Dim Trim = GetType(SystemMemoryOptimization).GetMethod("EmptyWorkingSet", BindingFlags.Static Or BindingFlags.NonPublic)
        Configure(0)
        Check(CBool(Trim.Invoke(Nothing, New Object() {New IntPtr(&H100000001L)})), "native working-set call preserves 64-bit process handle")
        Check(Counter(5) = 1, "native working-set call executed")
        Configure(9)
        Check(Not CBool(Trim.Invoke(Nothing, New Object() {New IntPtr(&H100000001L)})), "denied working-set request reports failure")

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
