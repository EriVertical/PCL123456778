Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices

Module Program
    Private Checks As Integer
    Private Const PrivateInstance As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic

    Sub Main(Args As String())
#If NET10_0 Then
        If Args.Length <> 1 Then Throw New ArgumentException("Pass the absolute path to the Linux native-window-fixture shared library.")
        Dim NativeHandle = NativeLibrary.Load(Args(0))
        NativeLibrary.SetDllImportResolver(GetType(Watcher).Assembly,
            Function(Name, Assembly, SearchPath) If(Name = "user32", NativeHandle, IntPtr.Zero))
        Dim Configure = Marshal.GetDelegateForFunctionPointer(Of ConfigureDelegate)(NativeLibrary.GetExport(NativeHandle, "ConfigureWindows"))
        Dim Visits = Marshal.GetDelegateForFunctionPointer(Of VisitsDelegate)(NativeLibrary.GetExport(NativeHandle, "WindowVisits"))

        'Regression: a visible window must release the launch wait even without new log lines.
        Dim Found = NewWatcher()
        SetField(Found, "IsWindowAppeared", True)
        CallPrivate(Found, "TimerLog")
        Check(Found.State = Watcher.MinecraftState.Running, "window recognition must not wait for another log")
        Check(Found.Loader.Progress = 0.95, "window-only launch progress")

        Dim NoWindow = NewWatcher()
        CallPrivate(NoWindow, "TimerLog")
        Check(NoWindow.State = Watcher.MinecraftState.Loading, "empty logs alone must not mark success")
        SetField(NoWindow, "LogProgress", 5)
        CallPrivate(NoWindow, "TimerLog")
        Check(NoWindow.State = Watcher.MinecraftState.Running, "existing log-based fallback still works")

        Dim Crashed = NewWatcher()
        Crashed.State = Watcher.MinecraftState.Crashed
        SetField(Crashed, "IsWindowAppeared", True)
        CallPrivate(Crashed, "TimerLog")
        Check(Crashed.State = Watcher.MinecraftState.Crashed, "window must not overwrite a crash")

        Using Exited = Process.Start(New ProcessStartInfo("/bin/sh", "-c ""exit 0""") With {.UseShellExecute = False})
            Exited.WaitForExit()
            Dim DuringLaunch = NewWatcher()
            DuringLaunch.GameProcess = Exited
            CallPrivate(DuringLaunch, "TimerLog")
            Check(DuringLaunch.State = Watcher.MinecraftState.Crashed, "silent exit before a window must be detected")
            Dim AfterLaunch = NewWatcher()
            AfterLaunch.GameProcess = Exited
            AfterLaunch.State = Watcher.MinecraftState.Running
            CallPrivate(AfterLaunch, "TimerLog")
            Check(AfterLaunch.State = Watcher.MinecraftState.Ended, "silent normal exit after launch must be detected")
        End Using

        'Exercise actual P/Invoke declarations and callback against controlled native windows.
        Configure(0, CUInt(Environment.ProcessId))
        Dim Window = FindWindow(NewWatcher())
        Check(Window.HasValue, "find game after unrelated, hidden and utility windows")
        Check(Window.Value.Key.ToInt64() = &H100000007L, "64-bit handle must remain intact")
        Check(Window.Value.Value.EndsWith("世界"), "Unicode window title must remain intact")
        Check(Visits() = 7, "continue past rejected windows and stop on the match")

        Configure(1, CUInt(Environment.ProcessId))
        Check(Not FindWindow(NewWatcher()).HasValue, "do not accept unrelated, hidden, blank or utility windows")
        Check(Visits() = 6, "finish enumerating all rejected windows")

        Configure(2, CUInt(Environment.ProcessId))
        Dim Forge = NewWatcher()
        CallPrivate(Forge, "TimerWindow")
        Check(CBool(GetField(Forge, "IsWindowAppeared")), "recognize the early Forge window")
        Check(Not CBool(GetField(Forge, "IsWindowFinished")), "keep looking for the final game window after FML")
        CallPrivate(Forge, "TimerLog")
        Check(Forge.State = Watcher.MinecraftState.Running, "early Forge window releases launch wait without new logs")
        Configure(0, CUInt(Environment.ProcessId))
        CallPrivate(Forge, "TimerWindow")
        Check(CBool(GetField(Forge, "IsWindowFinished")), "later final game window is still recognized")

        For Each Scenario In {3, 4, 5}
            Configure(Scenario, CUInt(Environment.ProcessId))
            Check(FindWindow(NewWatcher()).HasValue, "visible SDL/LWJGL/AWT window")
        Next
        Configure(6, CUInt(Environment.ProcessId))
        Check(Not FindWindow(NewWatcher()).HasValue, "SDL placeholder title is not a game window")
        Console.WriteLine($"Passed {Checks} watcher regression checks using simulated native windows; no real Windows UI or Minecraft was run.")
#Else
        Throw New PlatformNotSupportedException("net48 is a compile compatibility check. Run the native fixture tests on Linux with net10.0.")
#End If
    End Sub

#If NET10_0 Then
    Private Delegate Sub ConfigureDelegate(Scenario As Integer, Pid As UInteger)
    Private Delegate Function VisitsDelegate() As Integer

    Private Function NewWatcher() As Watcher
        'Avoid starting the real background thread or redirected process streams.
        Dim Value = CType(RuntimeHelpers.GetUninitializedObject(GetType(Watcher)), Watcher)
        Value.GameProcess = Process.GetCurrentProcess()
        Value.Instance = New McInstance
        Value.Loader = New LoaderTask(Of Process, Integer)
        Value.WaitingLog = New List(Of String)
        Value.LatestLog = New ConcurrentQueue(Of String)
        SetField(Value, "WaitingLogLock", New Object)
        SetField(Value, "PID", Environment.ProcessId)
        SetField(Value, "WindowTitle", "")
        Return Value
    End Function

    Private Function FindWindow(Value As Watcher) As KeyValuePair(Of IntPtr, String)?
        Return CType(CallPrivate(Value, "TryGetMinecraftWindow"), KeyValuePair(Of IntPtr, String)?)
    End Function

    Private Function CallPrivate(Value As Watcher, Name As String) As Object
        Return GetType(Watcher).GetMethod(Name, PrivateInstance).Invoke(Value, Nothing)
    End Function

    Private Sub SetField(Value As Watcher, Name As String, Data As Object)
        GetType(Watcher).GetField(Name, PrivateInstance).SetValue(Value, Data)
    End Sub

    Private Function GetField(Value As Watcher, Name As String) As Object
        Return GetType(Watcher).GetField(Name, PrivateInstance).GetValue(Value)
    End Function

    Private Sub Check(Condition As Boolean, Name As String)
        If Not Condition Then Throw New Exception("Failed: " & Name)
        Checks += 1
    End Sub
#End If
End Module
