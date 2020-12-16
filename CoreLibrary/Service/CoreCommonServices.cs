using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using CoreLibrary.Model;
using System.ComponentModel.DataAnnotations;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Mail;
using System.Linq;

namespace CoreLibrary.Service
{
    public static class CoreCommonServices
    {
        #region Common Methods
        public static CoreRouteModel GetRouteModel()
        {
            CoreRouteModel model = new CoreRouteModel
            {
                Controller = HttpContext.Current.Request.RequestContext.RouteData.Values["controller"].ToString(),
                Action     = HttpContext.Current.Request.RequestContext.RouteData.Values["action"].ToString(),
                Parameters = HttpContext.Current.Request.Params,
                Route      = HttpContext.Current.Request.RequestContext.RouteData.Values["controller"] + "/" + HttpContext.Current.Request.RequestContext.RouteData.Values["action"],
                RequestURI = HttpContext.Current.Request.Url.ToString(),
                MethodName = HttpContext.Current.Request.RequestType,
                Request    = HttpContext.Current.Request.ToString()

            };
            return model;
        }

        public static string GetRoute()
        {
            return HttpContext.Current.Request.RequestContext.RouteData.Values["controller"] + "/" + HttpContext.Current.Request.RequestContext.RouteData.Values["action"];
        }

        public static string Encrypt(string clearText)
        {
            string EncryptionKey = "MAKV2SPBNI99212";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                if (encryptor != null)
                {
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(),
                            CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                            cs.Close();
                        }
                        clearText = Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            return clearText;
        }

        public static string Decrypt(string cipherText)
        {
            string EncryptionKey = "MAKV2SPBNI99212";
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                if (encryptor != null)
                {
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(),
                            CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        cipherText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            return cipherText;
        }

        public static CoreResponseModel ValidateImage(HttpPostedFile imageFile)
        {

            CoreResponseModel response = new CoreResponseModel();

            string[] allowedFileExtensions = { ".jpg", ".jpeg", ".gif", ".png" };

            if (!((IList)allowedFileExtensions).Contains(imageFile.FileName.ToLower()
                .Substring(imageFile.FileName.LastIndexOf('.'))))
            {
                response.Status = false;
                response.Message = CoreMessageModel.USER_PROFILEPICTURE_EXTENSION_RESTRICTION;
                response.Data = EmptyJson;
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Api = GetRoute();
            }
            else
            {
                response.Status = true;
            }
            return response;
        }

        public static string GetImageUrl(string serverUrl, bool forceHttps)
        {
            if (serverUrl.IndexOf("://", StringComparison.Ordinal) > -1)
                return serverUrl;

            string newUrl = serverUrl;
            Uri originalUri = HttpContext.Current.Request.Url;
            newUrl = (forceHttps ? "https" : originalUri.Scheme) +
                     "://" + originalUri.Authority + newUrl;
            return newUrl;
        }

        public static CoreResponseModel ValidateModel(dynamic model)
        {
            var context = new ValidationContext(model, null, null);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(model, context, results, true))
            {
                return CoreResponseServices.ReturnBadRequest(results[0].ToString());
            }
            else
            {
                return CoreResponseServices.ReturnModel(model);
            }
        }


        public static int TryParseInt32(string value)
        {
            int i;
            Int32.TryParse(value, out i);
            return i;
        }

        public static void SendMail(string strMailTo, string strMailSubject, string strMailBody, bool blnAllowMultipleBCC, bool sendBCC, bool blnSMTPGMAIL,byte[] attachementBytes = null)
        {

            string emailFrom = CoreMessageModel.EMAILFROM;
            if (!string.IsNullOrWhiteSpace(emailFrom))
            {
                string sitename = CoreMessageModel.EMAIL_SITENAME;
                MailMessage MyMailMessage = new MailMessage();
                MyMailMessage.From = new MailAddress(emailFrom, sitename);
                MyMailMessage.To.Add(strMailTo);

                string strBCCEmail = CoreMessageModel.BCCEMAIL;
                string strMultipleBCCEmail = CoreMessageModel.MULTIPLEBCCEMAIL;

                string[] strBCCEmails = strMultipleBCCEmail.Split(',');
                if (sendBCC == true)
                {
                    if (strBCCEmails != null && strBCCEmails.Length > 0)
                    {
                        strBCCEmail = strBCCEmails[0].Trim();
                    }
                    if (blnAllowMultipleBCC == true)
                    {
                        if (!string.IsNullOrEmpty(strMultipleBCCEmail))
                        {
                            strBCCEmails = strMultipleBCCEmail.Split(',');
                            if (strBCCEmails != null && strBCCEmails.Length > 0)
                            {
                                foreach (string strItem in strBCCEmails)
                                {
                                    MyMailMessage.Bcc.Add(strItem.Trim());
                                }
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(strBCCEmail))
                            {
                                MyMailMessage.Bcc.Add(strBCCEmail);
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(strBCCEmail))
                        {
                            MyMailMessage.Bcc.Add(strBCCEmail);
                        }
                    }
                }

                if (attachementBytes != null)
                {
                        MyMailMessage.Attachments.Add(new Attachment(new MemoryStream(attachementBytes), "bidReceived.pdf"));
                  
                }

                MyMailMessage.Subject = strMailSubject;
                MyMailMessage.IsBodyHtml = true;
                MyMailMessage.Body = strMailBody;
                SmtpClient SMTPServer = new SmtpClient(CoreMessageModel.SMTP_SERVER);
                SMTPServer.UseDefaultCredentials = false;
                string smtpPort = CoreMessageModel.SMTP_PORT;
                SMTPServer.Port = Convert.ToInt32(smtpPort);
                SMTPServer.EnableSsl = true;
                SMTPServer.Credentials = new NetworkCredential(CoreMessageModel.SMTP_AUTH_EMAIL, CoreMessageModel.SMTP_AUTH_PASSWORD);
                SMTPServer.Send(MyMailMessage);
            }
        }

        

        #endregion

        #region Declaration
        public static string EmptyJson = "{}";
        #endregion

    }
}
