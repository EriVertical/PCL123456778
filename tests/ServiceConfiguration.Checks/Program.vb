Imports System

Module Program
    Private Checks As Integer

    Sub Main()
        Check(ServiceConfiguration.IsValidClientId("12345678-1234-1234-1234-123456789abc"), "valid client ID")
        For Each Invalid In {Nothing, "", "not-a-client-id", Guid.Empty.ToString()}
            Check(Not ServiceConfiguration.IsValidClientId(Invalid), "invalid client ID rejected")
        Next

        For Each Url In {"https://api.curseforge.com/v1/mods", "https://API.CURSEFORGE.COM:443/v1/games", "https://api.curseforge.com/v1/mods?q=test"}
            Check(ServiceConfiguration.IsCurseForgeApiUrl(Url), "official HTTPS API accepted")
        Next
        For Each Url In {Nothing, "", "not a url", "http://api.curseforge.com/v1/mods", "https://api.curseforge.com.evil.example/v1/mods",
                        "https://evil.example/api.curseforge.com", "https://evil.example/?host=api.curseforge.com", "https://api.curseforge.com@evil.example/",
                        "https://user@api.curseforge.com/", "https://api.curseforge.com:444/", "https://edge.forgecdn.net/test.jar", "ftp://api.curseforge.com/"}
            Check(Not ServiceConfiguration.IsCurseForgeApiUrl(Url), "key must not be sent to this destination")
        Next

        '只改变本测试进程的变量，使用虚构输入，不访问真实凭据或注册表。
        Dim OldId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID")
        Dim OldKey = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY")
        Try
            Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", " 12345678-1234-1234-1234-123456789abc ")
            Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", " fake-key-for-tests ")
            Check(ServiceConfiguration.ReadClientId() = "12345678-1234-1234-1234-123456789abc", "environment client ID precedence and trim")
            Check(ServiceConfiguration.ReadApiKey() = "fake-key-for-tests", "environment key precedence and trim")
            Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", "fake" & vbCrLf & "injected")
            Check(ServiceConfiguration.ReadApiKey() = "", "malformed environment key rejected before adding HTTP headers")
        Finally
            Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", OldId)
            Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY", OldKey)
        End Try

        ExpectInvalid(Sub() ServiceConfiguration.Save("not-a-guid", ""))
        ExpectInvalid(Sub() ServiceConfiguration.Save("", "key" & vbCrLf & "injection"))
        ExpectInvalid(Sub() ServiceConfiguration.Save("", "key with spaces"))
        Console.WriteLine($"Passed {Checks} checks. Windows registry/DPAPI, WPF, and live service authorization are not exercised.")
    End Sub

    Private Sub Check(Condition As Boolean, Name As String)
        If Not Condition Then Throw New Exception("Failed: " & Name)
        Checks += 1
    End Sub

    Private Sub ExpectInvalid(Action As Action)
        Try
            Action()
        Catch ex As ArgumentException
            Checks += 1
            Return
        End Try
        Throw New Exception("Invalid input was accepted before accessing Windows storage.")
    End Sub
End Module
