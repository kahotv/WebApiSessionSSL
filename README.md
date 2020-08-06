# WebApi Session and SSL

​	windows下的selfhost方式实现的双向SSL认证。

​	此项目是基于`kahotv/WebApiSession`项目的改造。

## 核心功能

- 双向SSL证书认证
- 用户与证书绑定
- 强制使用HTTPS

## 预备条件

- 服务端：域名的pfx证书，或crt证书和私钥key。可以是自签发或者三方可信机构签发。
- 签发用户证书：域名的ca证书。
- openssl：用于启用单向或双向SSL。
- Session功能：用于校验登陆者和证书是否匹配，此功能在[WebApiSession项目](https://github.com/kahotv/WebApiSession)实现。

## 实现方式

- 签发机构校验：保证证书是合法的，防止A使用含有A名称的非法证书来访问接口。

- 用户名一致性校验：保证证书颁发对象是当前用户，防止A使用B账号+含有A名称的合法证书来访问接口。

### 强制HTTPS

​	禁用HTTP，只能使用HTTPS。使用`DelegatingHandler`实现。

```c#
public class HttpsGuardHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.RequestUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.CurrentCultureIgnoreCase))
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Https is required for security reason")
            };
            return Task.FromResult(response);
        }
        try
        {
            var res = base.SendAsync(request, cancellationToken);
            return res;
        }
        catch (System.Exception ex)
        {
            return null;
        }
    }
}
```

```c#
string url = "https://0.0.0.0:9000/";
var config = new HttpSelfHostConfiguration(url);
//...略
config.MessageHandlers.Add(new HttpsGuardHandler());    //强制HTTPS

```



### 启用双向SSL

1. 安装pfx证书到`个人`目录。

   安装完毕后可以看到左上角有小钥匙，没有的话就是导入的crt，要先删除后再安装。

2. 绑定端口

```bash
netsh http add sslcert ipport=0.0.0.0:9000  certhash=999ab7xxxxxxxxxxx appid={c2427611-7110-420a-abcf-f6298aa9e1a1} clientcertnegotiation=enable
```

| 参数                                         | 作用                                     |
| -------------------------------------------- | ---------------------------------------- |
| clientcertnegotiation=enable                 | 让浏览器传入客户端证书（启用客户端验证） |
| certhash=999ab7xxxxxxxxxxx                   | pfx证书的指纹                            |
| appid={c2427611-7110-420a-abcf-f6298aa9e1a1} | http服务端的guid，保证当前电脑唯一即可   |
| ipport=0.0.0.0:9000                          | 这里不能填域名，可以填外网IP             |

### 全局校验-签发机构

​	继承`X509CertificateValidator`类来做全局证书签发机构校验。

​	*`cert.IssuerName`里储存了签发机构信息。*

```c#
public class CertLimit : X509CertificateValidator
{
    string allowedIssuerName;

    public CertLimit(string allowedIssuerName)
    {
        if (allowedIssuerName == null)
            throw new ArgumentNullException("allowedIssuerName");

        this.allowedIssuerName = allowedIssuerName;
    }
    public override void Validate(X509Certificate2 cert)
    {
        //检查是否携带证书
        if (cert == null)
            throw new ArgumentNullException("certificate");

        //检查证书是否过期
        if (cert.NotAfter < DateTime.Now)
            throw new SecurityTokenValidationException("Certificate has expired");

        // 检查证书来源
        if (allowedIssuerName == cert.IssuerName.Name)
            throw new SecurityTokenValidationException("Certificate was not issued by a trusted issuer");
            
    }
}
```

```c#
string url = "https://0.0.0.0:9000/";
//TODO 签发结构，这里改成自己的
string issuer = "CN=XX.WWW, OU=FF, O=XX, L=CD, S=SC, C=CN";
var config = new HttpSelfHostConfiguration(url);
//设置客户端验证方式为`验证证书`
config.ClientCredentialType = System.ServiceModel.HttpClientCredentialType.Certificate;
//设置验证证书类
config.X509CertificateValidator = new CertLimit(issuer);
```

### 一致性校验-证书用户名

​	在`SessionAttribute`类里做登陆者和用户证书一致性校验。

​	*`cert.SubjectName`里储存了用户信息。*

```c#
public override void OnActionExecuting(HttpActionContext actionContext)
{
	//...略
	//检查当前证书与用户名是否一致
	var cert = actionContext.Request.GetClientCertificate();
	if (cert == null 
		|| !cert.SubjectName.Name.StartsWith($"CN={user.UserInfo.UserName},"))
	{
		//证书错误或不匹配
		actionContext.Response = actionContext.Request.CreateResponse<JObject>(ErrCodeManager.GetError(ErrCode.ClientCertError));
		return;
	}
	//...略
}
```

## 附加

### 生成pfx

​	若手里只有crt和key文件，用以下命令来合并为pfx：

```bash
openssl pkcs12 -export -inkey KEY_FILE_NAME -in CERT_FILE -out SOMETHING.pfx
```

​	为了方便，我把文件放在了`E:\`

​	实际命令是：

```bash
openssl pkcs12 -export -inkey E:\_.xxx.com.key -in E:\_.xxx.com.crt -out E:\_.xxx.com.pfx
```

​	执行时会让你输入两次密码，这个密码是导入pfx需要用的。

​	双击pfx，导入到`个人`目录，其他选项全部默认。

### 一些坑

- GetClientCertificate()为空

  现象：可以用GetClientCertificate()函数来获取证书，但函数返回为null。

  原因：客户端没传入证书。

  解决办法：
  
  - 对于浏览器，需要在绑定SSL时使用`clientcertnegotiation=enable`标记；
  - 对于程序，需要把证书放在指定的属性，例如C#：

```c#
var clientCertificate = ...;//获取本地证书对象
using (WebRequestHandler handler = new WebRequestHandler())
{
 handler.ClientCertificates.Add(clientCertificate);   //传入证书
 using (HttpClient httpClient = new HttpClient(handler))
 {
     var request = new HttpRequestMessage(method: httpMethod, requestUri: requestUri);
         
     if (requestContent != null)
     {
         request.Content = requestContent;
     }
     return await httpClient.SendAsync(request);
 }
}
```

- 跨域

  现象：访问接口时报跨域相关错误。

  原因：跨域配置不正确。

  解决办法：跨域里要配置好支持https，博主这不知道为什么不支持配置域名，只能把http协议一起配置

```c#
response.Headers.Add("Access-Control-Allow-Origin", "https://www.xxx.com");
...(其他跨域属性)
```

- 报403 SSL错误 

​	现象：一切流程正常，但是访问https接口就报403错误。

​	原因：可能被中间人或类似的软件干扰。

​	解决办法：检查是否开启了Fiddler或类似的软件，关掉再尝试。或者关掉后从新绑定SSL再尝试。

- 浏览器缓存

  现象：不显示选择证书的页面。

  原因：证书只需要选择一次，在首次调用接口时。

  解决办法：完全关掉浏览器全部页面，再不行还需要重启HTTP服务端。