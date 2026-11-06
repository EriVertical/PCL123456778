'仅替代监视器不涉及本次检查的启动器 UI、日志和崩溃分析依赖。
'窗口枚举和状态转换仍执行链接进来的实际 ModWatcher.vb。
Imports System.Runtime.CompilerServices

Module AppStubs
    Public FrmMain As New TestMain
    Public Settings As New TestSettings
    Public Logger As New TestLogger
    Public Sub McLaunchLog(Text As String, Optional Level As LogLevel = LogLevel.Info)
    End Sub
    Public Sub RunInUi(Action As Action)
        Action()
    End Sub
    Public Sub RunInNewThread(Action As Action, Name As String)
        '测试逐次调用计时方法，不启动后台计时器或崩溃分析。
    End Sub
    Public Function MusicResume() As Boolean
        Return False
    End Function
    Public Function MusicPause() As Boolean
        Return False
    End Function
    Public Sub Hint(Text As String, Optional Type As HintType = HintType.Blue)
    End Sub
    Public Sub FeedbackInfo()
    End Sub
    <Extension>
    Public Function StartsWithF(Text As String, Value As String) As Boolean
        Return Text.StartsWith(Value, StringComparison.Ordinal)
    End Function
    <Extension>
    Public Function EndsWithF(Text As String, Value As String) As Boolean
        Return Text.EndsWith(Value, StringComparison.Ordinal)
    End Function
    <Extension>
    Public Function ContainsIgnoreCase(Text As String, Value As String) As Boolean
        Return Text.IndexOf(Value, StringComparison.OrdinalIgnoreCase) >= 0
    End Function
#If NET48 Then
    <Extension>
    Public Function ReplaceLineEndings(Text As String, Value As String) As String
        Return Text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Replace(vbLf, Value)
    End Function
#End If
End Module

Public Enum LogLevel
    Info
    [Error]
End Enum
Public Enum LogBehavior
    Toast
End Enum
Public Enum HintType
    Blue
    Red
End Enum
Public Enum LoadState
    Loading
    Canceled
End Enum
Public Class LoaderTask(Of TInput, TOutput)
    Public Input As TInput
    Public Progress As Double
    Public State As LoadState
End Class
Public Class McInstance
    Public ReleaseTime As New Date(2020, 1, 1)
    Public PathIndie As String = ""
    Public PathVersion As String = ""
    Public Name As String = ""
End Class
Public Class TestMain
    Public BtnExtraShutdown As New TestButton
    Public Hidden As Boolean
    Public Sub EndProgram(Value As Boolean)
    End Sub
End Class
Public Class TestButton
    Public Sub ShowRefresh()
    End Sub
End Class
Public Class TestSettings
    Public Function [Get](Of T)(Name As String) As T
        Return Nothing
    End Function
End Class
Public Class TestLogger
    Public Sub Info(Text As String)
    End Sub
    Public Sub Trace(Text As String)
    End Sub
    Public Sub Warn(Ex As Exception, Text As String)
        Throw New Exception(Text, Ex)
    End Sub
    Public Sub [Error](Ex As Exception, Text As String, Optional Behavior As LogBehavior = LogBehavior.Toast)
        Throw New Exception(Text, Ex)
    End Sub
End Class
Public Class CrashAnalyzer
    Public Sub New(Instance As McInstance)
    End Sub
    Public Sub Collect(Folder As String, Logs As List(Of String))
    End Sub
    Public Sub Prepare()
    End Sub
    Public Sub Analyze()
    End Sub
    Public Sub Output(Value As Boolean, Files As List(Of String))
    End Sub
End Class
Public Module Paths
    Public Base As String = ""
End Module
