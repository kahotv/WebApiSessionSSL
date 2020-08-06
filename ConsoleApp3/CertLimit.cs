using System;
using System.Collections.Generic;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp3
{
public class CertLimit : X509CertificateValidator
{
    string allowedIssuerName;

    public CertLimit(string allowedIssuerName)
    {
        if (allowedIssuerName == null)
        {
            throw new ArgumentNullException("allowedIssuerName");
        }

        this.allowedIssuerName = allowedIssuerName;
    }
    public override void Validate(X509Certificate2 cert)
    {
        //检查是否携带证书
        if (cert == null)
        {
            throw new ArgumentNullException("cert");
        }

        //检查证书是否过期
        if (cert.NotAfter < DateTime.Now)
        {
            throw new SecurityTokenValidationException
                ("Certificate has expired");
        }

        // 检查证书来源
        if (allowedIssuerName == cert.IssuerName.Name)
        {
            throw new SecurityTokenValidationException
                ("Certificate was not issued by a trusted issuer");
        }


    }
}
}
