using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web;
using System.Web.Security;
using CoreLibrary.Model;
using CoreLibrary.Service;
using SmartDocScan.Data;
using SmartDocScan.Models;
using context = System.Web.HttpContext;

namespace SmartDocScan.Service
{
    public static class CommonService
    {
        public static CoreResponseModel CheckAuthentication(HttpRequestMessage request)
        {
            try {
                var authorization = request.Headers.Authorization;

                if (authorization == null || authorization.Scheme != "Bearer")
                    return CoreResponseServices.ReturnAccessTokenStatus(CoreMessageModel.COMMON_JWT_MISSING);

                if (string.IsNullOrEmpty(authorization.Parameter)) {
                    return CoreResponseServices.ReturnAccessTokenStatus(CoreMessageModel.COMMON_JWT_MISSING);
                }

                var token = authorization.Parameter;

                var simplePrinciple = JwtManager.GetPrincipal(token);
                var identity = simplePrinciple.Identity as ClaimsIdentity;

                if (identity == null)
                    return CoreResponseServices.ReturnAccessTokenStatus(CoreMessageModel.COMMON_JWT_EXPIRED);

                if (!identity.IsAuthenticated)
                    return CoreResponseServices.ReturnAccessTokenStatus(CoreMessageModel.COMMON_JWT_EXPIRED);

                var usernameClaim = identity.FindFirst(ClaimTypes.Name);
                string username = usernameClaim.Value;


                if (string.IsNullOrEmpty(username))
                    return CoreResponseServices.ReturnAccessTokenStatus(CoreMessageModel.COMMON_JWT_INVALID);

                //Update Activity
                //UpdateActivity(new Guid(username));

                CoreResponseModel coreResponseModel = new CoreResponseModel { Status = true, UserId = Convert.ToInt32(username) };
                return coreResponseModel;
            } catch {
                return CoreResponseServices.ReturnAccessTokenStatus(CoreMessageModel.COMMON_JWT_INVALID);
            }
        }

        public static string CreateErrorLog(Exception ex, dynamic parameterModel, string customeMessage, int userId)
        {
            try {
                //ErrorLog model = new ErrorLog();

                //var routeModel = CoreCommonServices.GetRouteModel();

                //if (ex.InnerException != null) model.InnerException = ex.InnerException.ToString();
                //model.Message = ex.Message;
                //model.Source = ex.Source;
                //model.StackTrace = ex.StackTrace;
                //model.TargetSite = ex.TargetSite.ToString();
                //model.CustomMessage = customeMessage;
                //model.Controller = routeModel.Controller;
                //model.MethodName = routeModel.MethodName;
                //model.Route = CoreCommonServices.GetRoute();
                //model.Parameters = parameterModel;
                //model.CreatedDate = DateTime.Now;
                //model.InnerException = ex.Message;
                //model.UserId = userId;

                //using (ErrorLogClient client = new ErrorLogClient())
                //{
                //    return client.Post(model);
                //}

                return "0";
            } catch {

                return "0";
            }
        }

        public static CoreResponseModel ReturnErrorWithLog(Exception ex, dynamic parameterModel, string customMessage, int userId)
        {
            string errorLogId = CreateErrorLog(ex, parameterModel, customMessage, userId);

            CoreResponseModel response = new CoreResponseModel {
                Status = false,
                Message = "Internal Server Error",
                Data = ex.Message,
                StatusCode = HttpStatusCode.InternalServerError,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true,
                ErrorLogId = errorLogId
            };
            return response;
        }

        public static bool ResizeImageAndSave(double scaleFactor, Stream sourcePath, string targetPath)
        {
            using (var image = Image.FromStream(sourcePath)) {
                var newWidth = (int)(image.Width * scaleFactor);
                var newHeight = (int)(image.Height * scaleFactor);
                var thumbnailImg = new Bitmap(newWidth, newHeight);
                var thumbGraph = Graphics.FromImage(thumbnailImg);
                thumbGraph.CompositingQuality = CompositingQuality.HighQuality;
                thumbGraph.SmoothingMode = SmoothingMode.HighQuality;
                thumbGraph.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var imageRectangle = new Rectangle(0, 0, newWidth, newHeight);
                thumbGraph.DrawImage(image, imageRectangle);
                thumbnailImg.Save(targetPath, image.RawFormat);
                return true;
            }
        }

        public static void CreateCookie(usersinfo model, bool rememberMe)
        {
            FormsAuthentication.SetAuthCookie(model.username, rememberMe);
            FormsAuthenticationTicket ticket = null;
            string role = null;
            if (model.IsAdmin == true)
            {
                role = "admin";
            }
            else
            {
                role = "normal";
            }
            /*Add Company id for display users company*/
            ticket = new FormsAuthenticationTicket(1, model.username, DateTime.Now, DateTime.Now.AddMinutes(50), rememberMe, model.username + "," + model.IsAdmin + "," + model.name+","+model.comp_id);

            HttpCookie myHttpCookie = new HttpCookie("LoginUserHttpCookie", DateTime.Now.ToString())
            {
                HttpOnly = true,
                Value = FormsAuthentication.Encrypt(ticket)
            };

            HttpContext.Current.Response.Cookies.Add(myHttpCookie);
        }

        public static LoginUserViewModel GetLoginUserData()
        {
            var cookie = HttpContext.Current.Request.Cookies["LoginUserHttpCookie"];
            var ticketInfo = FormsAuthentication.Decrypt(cookie.Value);

            LoginUserViewModel model = new LoginUserViewModel();
            string[] data = ticketInfo.UserData.Split(",".ToCharArray());

            model.username = data[0];
            model.IsAdmin = Convert.ToBoolean(data[1]);
            model.name = Convert.ToString(data[2]);
            /*Add comp_id */
            model.comp_id = Convert.ToInt32(data[3]);
            return model;
        }
        private static String ErrorlineNo, Errormsg, extype, exurl, hostIp, ErrorLocation, HostAdd;

        public static void SendErrorToText(Exception ex)
        {

            var line = Environment.NewLine + Environment.NewLine;

            ErrorlineNo = ex.StackTrace.Substring(ex.StackTrace.Length - 7, 7);
            Errormsg = ex.GetType().Name.ToString();
            extype = ex.GetType().ToString();
            exurl = context.Current.Request.Url.ToString();
            ErrorLocation = ex.Message.ToString();

            try
            {
                string filepath = context.Current.Server.MapPath("~/ExceptionDetailsFile/");  //Text File Path

                if (!Directory.Exists(filepath))
                {
                    Directory.CreateDirectory(filepath);
                }
                filepath = filepath + DateTime.Today.ToString("dd-MM-yy") + ".txt";   //Text File Name
                if (!File.Exists(filepath))
                {
                    File.Create(filepath).Dispose();
                }
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    string error = "Log Written Date:" + " " + DateTime.Now.ToString() + line + "Error Line No :" + " " + ErrorlineNo + line + "Error Message:" + " " + Errormsg + line + "Exception Type:" + " " + extype + line + "Error Location :" + " " + ErrorLocation + line + " Error Page Url:" + " " + exurl + line + "User Host IP:" + " " + hostIp + line;
                    sw.WriteLine("-----------Exception Details on " + " " + DateTime.Now.ToString() + "-----------------");
                    sw.WriteLine("-------------------------------------------------------------------------------------");
                    sw.WriteLine(line);
                    sw.WriteLine(error);
                    sw.WriteLine("--------------------------------*End*------------------------------------------");
                    sw.WriteLine(line);
                    sw.Flush();
                    sw.Close();

                }

            }
            catch (Exception e)
            {
                e.ToString();

            }
        }

        public static void SendErrorToText2(string str)
        {

            var line = Environment.NewLine + Environment.NewLine;
            exurl = context.Current.Request.Url.ToString();
            try
            {
                string filepath = context.Current.Server.MapPath("~/ExceptionDetailsFile/");  //Text File Path

                if (!Directory.Exists(filepath))
                {
                    Directory.CreateDirectory(filepath);
                }
                filepath = filepath + DateTime.Today.ToString("dd-MM-yy") + ".txt";   //Text File Name
                if (!File.Exists(filepath))
                {
                    File.Create(filepath).Dispose();
                }
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    string error = str;
                    sw.WriteLine("-----------Exception Details on " + " " + DateTime.Now.ToString() + "-----------------");
                    sw.WriteLine(error);
                    sw.WriteLine("--------------------------------*End*------------------------------------------");
                    sw.WriteLine(line);
                    sw.Flush();
                    sw.Close();

                }

            }
            catch (Exception e)
            {
                e.ToString();

            }
        }
    }
}