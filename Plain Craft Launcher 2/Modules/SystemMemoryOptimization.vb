Imports System
Imports System.ComponentModel
Imports System.Runtime.InteropServices

'使用 Windows 公开 API 回收系统文件缓存，不强制修改其他应用的工作集。
Friend Module SystemMemoryOptimization
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
End Module
