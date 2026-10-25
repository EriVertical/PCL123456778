Imports Microsoft.Win32
Imports System
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text

'用户提供的服务配置不写入源码；API Key 由 Windows DPAPI 绑定当前用户加密。
Friend Module ServiceConfiguration
    Private Const RegistryPath As String = "Software\PCLDebug\Services"

    Public Function ReadClientId() As String
        Dim Value = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID")
        If Not String.IsNullOrWhiteSpace(Value) Then Return Value.Trim()
        Return ReadLocalClientId()
    End Function

    Public Function ReadLocalClientId() As String
        Try
            Using Key = Registry.CurrentUser.OpenSubKey(RegistryPath)
                Return If(TryCast(Key?.GetValue("MicrosoftClientId"), String), "")
            End Using
        Catch ex As Exception When TypeOf ex Is System.Security.SecurityException OrElse TypeOf ex Is UnauthorizedAccessException
            Return ""
        End Try
    End Function

    Public Function ReadApiKey() As String
        Dim Value = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY")
        If Not String.IsNullOrWhiteSpace(Value) Then Return If(IsValidApiKey(Value.Trim()), Value.Trim(), "")
        Return ReadLocalApiKey()
    End Function

    Public Function ReadLocalApiKey() As String
        Try
            Using Key = Registry.CurrentUser.OpenSubKey(RegistryPath)
                Dim Encrypted = TryCast(Key?.GetValue("CurseForgeApiKey"), Byte())
                If Encrypted Is Nothing Then Return ""
                Dim Decrypted = Encoding.UTF8.GetString(ProtectedData.Unprotect(Encrypted, Nothing, DataProtectionScope.CurrentUser))
                Return If(IsValidApiKey(Decrypted), Decrypted, "")
            End Using
        Catch ex As Exception When TypeOf ex Is CryptographicException OrElse TypeOf ex Is System.Security.SecurityException OrElse TypeOf ex Is UnauthorizedAccessException
            '复制到其他 Windows 用户的配置无法解密，允许用户从百宝箱重新配置。
            Return ""
        End Try
    End Function

    Public Function IsValidClientId(Value As String) As Boolean
        Dim Id As Guid
        Return Guid.TryParse(Value, Id) AndAlso Id <> Guid.Empty
    End Function

    Public Function IsValidApiKey(Value As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(Value) AndAlso Not Value.Any(Function(c) Char.IsWhiteSpace(c) OrElse Char.IsControl(c))
    End Function

    Public Sub Save(ClientId As String, ApiKey As String)
        ClientId = ClientId.Trim()
        ApiKey = ApiKey.Trim()
        If ClientId <> "" AndAlso Not IsValidClientId(ClientId) Then Throw New ArgumentException("Client ID 应为应用程序（客户端）ID，格式为 GUID。")
        If ApiKey <> "" AndAlso Not IsValidApiKey(ApiKey) Then Throw New ArgumentException("API Key 中不能包含空白或控制字符，请重新复制。")
        '先加密再写入，避免加密失败时改变现有配置。空 Key 保留原值。
        Dim Encrypted = If(ApiKey = "", Nothing, ProtectedData.Protect(Encoding.UTF8.GetBytes(ApiKey), Nothing, DataProtectionScope.CurrentUser))
        Using Key = Registry.CurrentUser.CreateSubKey(RegistryPath)
            If Encrypted IsNot Nothing Then Key.SetValue("CurseForgeApiKey", Encrypted, RegistryValueKind.Binary)
            Key.SetValue("MicrosoftClientId", ClientId, RegistryValueKind.String)
        End Using
    End Sub

    Public Sub ClearApiKey()
        Using Key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable:=True)
            Key?.DeleteValue("CurseForgeApiKey", throwOnMissingValue:=False)
        End Using
    End Sub

    Public Function IsCurseForgeApiUrl(Url As String) As Boolean
        Dim Address As Uri = Nothing
        Return Uri.TryCreate(Url, UriKind.Absolute, Address) AndAlso
            Address.Scheme = Uri.UriSchemeHttps AndAlso Address.IsDefaultPort AndAlso
            Address.UserInfo = "" AndAlso Address.Host.Equals("api.curseforge.com", StringComparison.OrdinalIgnoreCase)
    End Function
End Module
