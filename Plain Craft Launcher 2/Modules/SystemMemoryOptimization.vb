Imports System
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Security.Principal

'使用 Windows 公开 API：普通模式回收文件缓存，深度模式额外回收当前用户会话内的应用工作集。
Friend Module SystemMemoryOptimization
    Public Const CacheCompleted As Integer = 1
    Public Const WorkingSetsCompleted As Integer = 2
    Public Const ProcessesSkipped As Integer = 4
    Public Const ProcessesFailed As Integer = 8
    Public Const CacheFailed As Integer = 16

    Public Function DescribeResult(FreedKb As Long, Optional Deep As Boolean = False, Optional Status As Integer = CacheCompleted) As String
        Dim Prefix = If(Deep, "深度内存回收：", "系统文件缓存回收：")
        Prefix &= If((Status And CacheCompleted) <> 0, "缓存步骤已执行。", "缓存步骤未完成。")
        If Deep Then
            Prefix &= If((Status And WorkingSetsCompleted) <> 0, "已回收可访问应用的工作集。", "没有应用工作集被回收。")
            If (Status And (ProcessesSkipped Or ProcessesFailed)) <> 0 Then Prefix &= "部分进程不可访问、已退出或不符合范围，已跳过。"
        End If
        If FreedKb <= 0 Then
            Return Prefix & "未观察到可用内存增加，不保证降低任务管理器中的总占用。"
        End If
        Return Prefix & $"可用内存较执行前增加约 {Math.Round(FreedKb / 1024.0, 1)} MB（系统净变化，非精确释放量）。"
    End Function

    Public Function IsEligibleProcess(ProcessId As Integer, SessionId As Integer, OwnerSid As String,
            CurrentProcessId As Integer, CurrentSessionId As Integer, CurrentSid As String, ExcludedProcessId As Integer) As Boolean
        Return ProcessId > 4 AndAlso ProcessId <> CurrentProcessId AndAlso ProcessId <> ExcludedProcessId AndAlso
            SessionId > 0 AndAlso SessionId = CurrentSessionId AndAlso Not String.IsNullOrEmpty(OwnerSid) AndAlso
            Not String.IsNullOrEmpty(CurrentSid) AndAlso String.Equals(OwnerSid, CurrentSid, StringComparison.Ordinal)
    End Function

    '各步骤独立执行，缓存回收失败时仍可尝试应用工作集；返回可通过子进程退出码传递的状态位。
    Public Function Execute(Deep As Boolean, ExcludedProcessId As Integer) As Integer
        If Not Deep Then
            TrimFileCache()
            Return CacheCompleted
        End If
        Dim Status As Integer
        Try
            TrimFileCache()
            Status = CacheCompleted
        Catch ex As Win32Exception
            Status = CacheFailed
        End Try
        Using Identity = WindowsIdentity.GetCurrent(), Current = Process.GetCurrentProcess()
            Dim CurrentSid = Identity.User.Value
            For Each Candidate In Process.GetProcesses()
                Using Candidate
                    Dim Handle As IntPtr = IntPtr.Zero, Token As IntPtr = IntPtr.Zero
                    Try
                        Dim Id = Candidate.Id
                        If Id <= 4 OrElse Id = Current.Id OrElse Id = ExcludedProcessId OrElse
                            Candidate.SessionId = 0 OrElse Candidate.SessionId <> Current.SessionId Then Continue For
                        '仅处理当前用户当前会话的应用，系统服务和其他用户的进程不在回收范围。
                        Handle = OpenProcess(&H1100UI, False, Id) 'QUERY_LIMITED_INFORMATION | SET_QUOTA
                        If Handle = IntPtr.Zero OrElse Not OpenProcessToken(Handle, &H8UI, Token) Then
                            Status = Status Or ProcessesSkipped
                            Continue For
                        End If
                        Using Owner As New WindowsIdentity(Token)
                            If Not IsEligibleProcess(Id, Candidate.SessionId, Owner.User?.Value,
                                    Current.Id, Current.SessionId, CurrentSid, ExcludedProcessId) Then Continue For
                        End Using
                        If EmptyWorkingSet(Handle) Then
                            Status = Status Or WorkingSetsCompleted
                        Else
                            Status = Status Or ProcessesFailed
                        End If
                    Catch ex As Exception When TypeOf ex Is Win32Exception OrElse TypeOf ex Is InvalidOperationException OrElse
                            TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is System.Security.SecurityException
                        Status = Status Or ProcessesSkipped
                    Finally
                        If Token <> IntPtr.Zero Then CloseHandle(Token)
                        If Handle <> IntPtr.Zero Then CloseHandle(Handle)
                    End Try
                End Using
            Next
        End Using
        Return Status
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure Luid
        Public LowPart As UInteger
        Public HighPart As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure TokenPrivileges
        Public Count As UInteger
        Public Id As Luid
        Public Attributes As UInteger
    End Structure

    Public Sub TrimFileCache()
        Dim Token As IntPtr
        If Not OpenProcessToken(GetCurrentProcess(), &H20UI Or &H8UI, Token) Then
            Throw New Win32Exception(Marshal.GetLastWin32Error(), "无法打开内存优化进程的权限令牌。")
        End If
        Dim Previous As TokenPrivileges
        Dim MustRestore As Boolean = False
        Try
            Dim Id As Luid
            If Not LookupPrivilegeValue(Nothing, "SeIncreaseQuotaPrivilege", Id) Then
                Throw New Win32Exception(Marshal.GetLastWin32Error(), "无法查询系统文件缓存回收所需权限。")
            End If
            Dim Requested As New TokenPrivileges With {.Count = 1, .Id = Id, .Attributes = 2}
            Dim ReturnLength As UInteger
            Dim Adjusted = AdjustTokenPrivileges(Token, False, Requested, CUInt(Marshal.SizeOf(GetType(TokenPrivileges))), Previous, ReturnLength)
            Dim ErrorCode = Marshal.GetLastWin32Error()
            'AdjustTokenPrivileges 返回 True 仍可能报 ERROR_NOT_ALL_ASSIGNED (1300)。
            If Not Adjusted OrElse ErrorCode <> 0 Then
                Throw New Win32Exception(ErrorCode, "无法启用缓存回收权限，请授予管理员权限。")
            End If
            MustRestore = Previous.Count <> 0
            '文档规定两个 SIZE_T 均为 -1 时回收缓存；Flags=0 保持现有缓存限制策略。
            Dim Flush = If(IntPtr.Size = 8, New UIntPtr(ULong.MaxValue), New UIntPtr(UInteger.MaxValue))
            If Not SetSystemFileCacheSize(Flush, Flush, 0) Then
                Throw New Win32Exception(Marshal.GetLastWin32Error(), "Windows 拒绝回收系统文件缓存。")
            End If
        Finally
            Try
                If MustRestore Then
                    Dim Restored = RestoreTokenPrivileges(Token, False, Previous, 0, IntPtr.Zero, IntPtr.Zero)
                    Dim ErrorCode = Marshal.GetLastWin32Error()
                    If Not Restored OrElse ErrorCode <> 0 Then
                        Throw New Win32Exception(ErrorCode, "无法恢复内存优化前的进程权限。")
                    End If
                End If
            Finally
                CloseHandle(Token)
            End Try
        End Try
    End Sub

    <DllImport("kernel32.dll")>
    Private Function GetCurrentProcess() As IntPtr
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function CloseHandle(Handle As IntPtr) As Boolean
    End Function
    <DllImport("advapi32.dll", SetLastError:=True)>
    Private Function OpenProcessToken(Process As IntPtr, Access As UInteger, ByRef Token As IntPtr) As Boolean
    End Function
    <DllImport("advapi32.dll", EntryPoint:="LookupPrivilegeValueW", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Function LookupPrivilegeValue(SystemName As String, Name As String, ByRef Id As Luid) As Boolean
    End Function
    <DllImport("advapi32.dll", SetLastError:=True)>
    Private Function AdjustTokenPrivileges(Token As IntPtr, DisableAll As Boolean, ByRef Requested As TokenPrivileges,
            BufferLength As UInteger, ByRef Previous As TokenPrivileges, ByRef ReturnLength As UInteger) As Boolean
    End Function
    <DllImport("advapi32.dll", EntryPoint:="AdjustTokenPrivileges", SetLastError:=True)>
    Private Function RestoreTokenPrivileges(Token As IntPtr, DisableAll As Boolean, ByRef Previous As TokenPrivileges,
            BufferLength As UInteger, PreviousState As IntPtr, ReturnLength As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function SetSystemFileCacheSize(Minimum As UIntPtr, Maximum As UIntPtr, Flags As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function OpenProcess(Access As UInteger, InheritHandle As Boolean, ProcessId As Integer) As IntPtr
    End Function
    <DllImport("psapi.dll", SetLastError:=True)>
    Private Function EmptyWorkingSet(Process As IntPtr) As Boolean
    End Function
End Module
