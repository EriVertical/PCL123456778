Public Class PageOtherTest

    Private Sub PageLoaded() Handles Me.Loaded
        TextClientId.Text = ServiceConfiguration.ReadLocalClientId()
        TextApiKey.Clear()
        RefreshServiceStatus()
    End Sub

    Private Sub RefreshServiceStatus()
        LabApiKeyStorage.Text = If(String.IsNullOrWhiteSpace(ServiceConfiguration.ReadLocalApiKey()),
            "本地 API Key 未保存或无法解密；若使用环境变量，请以下方本次运行状态为准。",
            "本地 API Key 已保存并可解密（不回显）；留空保存会保留，重启后仍会读取。输入新 Key 可替换。")
        LabServiceStatus.Text = "本次运行：Microsoft Client ID " & If(ServiceConfiguration.IsValidClientId(OAuthClientId), "已配置", "未配置或格式无效") &
            "；CurseForge API Key " & If(String.IsNullOrWhiteSpace(CurseForgeAPIKey), "未配置或无法解密", "已配置") & "。配置状态不代表已获服务授权。"
    End Sub

    Private Sub SaveServices() Handles BtnSaveServices.Click
        Try
            ServiceConfiguration.Save(TextClientId.Text, TextApiKey.Password)
            TextApiKey.Clear()
            RefreshServiceStatus()
            Hint("配置已保存，请完全退出并重启启动器后使用。环境变量配置仍优先。", HintType.Green)
        Catch ex As ArgumentException
            Hint(ex.Message, HintType.Red)
        Catch
            '不要记录异常中的输入或凭据。
            Hint("无法保存服务配置，请检查当前 Windows 用户的注册表访问权限。", HintType.Red)
        End Try
    End Sub

    Private Sub ClearServiceApiKey() Handles BtnClearApiKey.Click
        If MyMsgBox("仅清除本机保存的 CurseForge API Key，环境变量不受影响。重启后生效。", "清除 API Key", "清除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            ServiceConfiguration.ClearApiKey()
            TextApiKey.Clear()
            RefreshServiceStatus()
            Hint("本地 API Key 已清除，请重启启动器。", HintType.Green)
        Catch
            Hint("无法清除配置，请检查当前 Windows 用户的注册表访问权限。", HintType.Red)
        End Try
    End Sub

    Private Sub SearchJava() Handles BtnSearchJava.Click
        ManuallySearchJava()
    End Sub

    Private Sub ImportModpack() Handles BtnImportModpack.Click
        ModpackInstall()
    End Sub

    Private Sub OpenGame() Handles BtnOpenGame.Click
        If String.IsNullOrWhiteSpace(McFolderSelected) OrElse Not Directory.Exists(McFolderSelected) Then
            Hint("请先在版本选择中添加或选择一个存在的 Minecraft 文件夹。", HintType.Red)
            Return
        End If
        OpenExplorer(McFolderSelected)
    End Sub

    Private Sub OpenLogs() Handles BtnOpenLogs.Click
        OpenExplorer(Paths.Base & "PCL\")
    End Sub

    Private Sub DownloadFile() Handles BtnCustomDownload.Click
        Dim Address As Uri = Nothing
        If Not Uri.TryCreate(TextDownloadUrl.Text.Trim(), UriKind.Absolute, Address) Then
            Hint("请输入有效的 HTTP 或 HTTPS 下载地址。", HintType.Red)
            Return
        End If
        StartCustomDownload(Address.AbsoluteUri, Uri.UnescapeDataString(Address.Segments.LastOrDefault()).Trim("/"c))
    End Sub

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Dim Address As Uri = Nothing
        If Not Uri.TryCreate(Url, UriKind.Absolute, Address) OrElse
            (Address.Scheme <> Uri.UriSchemeHttp AndAlso Address.Scheme <> Uri.UriSchemeHttps) OrElse Address.UserInfo <> "" Then
            Hint("仅支持不含用户名和密码的 HTTP / HTTPS 下载地址。", HintType.Red)
            Return
        End If
        Try
            '既用于百宝箱按钮，也恢复主页自定义事件的“下载文件”。始终让用户确认保存位置。
            Dim SafeName = If(FileName, "")
            For Each InvalidChar In Path.GetInvalidFileNameChars()
                SafeName = SafeName.Replace(InvalidChar, "_"c)
            Next
            If String.IsNullOrWhiteSpace(SafeName) OrElse SafeName.Trim("."c) = "" Then SafeName = "下载文件"
            Dim Target = Dialogs.SaveFile("选择下载保存位置", SafeName, Folder)
            If String.IsNullOrEmpty(Target) Then Return
            Dim LoaderName = "自定义下载：" & Path.GetFileName(Target) & " "
            If LoaderTaskbar.Any(Function(task) task.Name = LoaderName AndAlso task.State = LoadState.Loading) Then
                Hint("同名文件正在下载，请等待完成或取消已有任务。", HintType.Red)
                Return
            End If
            Dim Steps As New List(Of LoaderBase) From {
                New LoaderDownload("下载文件", {New NetFile({Address.AbsoluteUri}, Target)})
            }
            Dim Loader As New LoaderCombo(Of Integer)(LoaderName, Steps) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(1)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch
            'URL 可能携带签名或凭据，不输出原始 URL 或异常文本。
            Hint("无法创建下载任务，请检查下载地址、文件名和保存位置。", HintType.Red)
        End Try
    End Sub
    Public Shared Sub Jrrp()
        Hint("此分支未提供今日人品工具。")
    End Sub
    Public Shared Sub RubbishClear()
        Hint("此分支未提供自动垃圾清理，请自行确认需要清理的文件。")
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        Try
            Dim FreedKb As Long
            If WindowsUtils.HasAdminRole() Then
                Dim Before = My.Computer.Info.AvailablePhysicalMemory
                MemoryOptimizeInternal(False)
                Dim After = My.Computer.Info.AvailablePhysicalMemory
                FreedKb = If(After > Before, CLng((After - Before) \ 1024UL), 0L)
            Else
                McLaunchLog("系统文件缓存回收需要管理员权限，正在请求 UAC 授权")
                FreedKb = RunAsAdmin("--memory")
                If FreedKb < 0 Then Throw New InvalidOperationException("内存优化子进程失败，缓存未完成回收。")
            End If
            Dim Message = $"系统文件缓存回收完成，可用内存增加约 {Math.Round(FreedKb / 1024.0)} MB（受其他程序内存变化影响）。"
            McLaunchLog(Message)
            If ShowHint Then Hint(Message, HintType.Green)
        Catch ex As ComponentModel.Win32Exception When ex.NativeErrorCode = 1223
            McLaunchLog("用户取消管理员授权，已跳过本次内存优化")
            Hint("已取消管理员授权，本次不进行内存优化。")
        Catch ex As Exception
            Logger.Warn(ex, "系统文件缓存回收失败，已跳过本次内存优化")
            Hint("内存优化未完成，已跳过。请检查管理员授权或查看日志。", HintType.Red)
        End Try
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        If Not WindowsUtils.HasAdminRole() Then Throw New UnauthorizedAccessException("系统文件缓存回收需要管理员权限。")
        SystemMemoryOptimization.TrimFileCache()
        If ShowHint Then Hint("系统文件缓存回收完成。", HintType.Green)
    End Sub
    Public Shared Function GetRandomCave() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function

End Class
