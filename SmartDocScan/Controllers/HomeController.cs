using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Mvc;
using CoreLibrary.Model;
using CoreLibrary.Service;
using SmartDocScan.Data;
using SmartDocScan.Models;
using SmartDocScan.Repository;
using SmartDocScan.Service;
using Spire.Barcode;

namespace SmartDocScan.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            //using (var client = new UserRepository())
            //{
            //    user modelUser = new user();
            //    modelUser = client.GetByUserName("micky");
            //    ViewBag.Data = modelUser;
            //}

            //Bitmap bitmap = new Bitmap(1240, 600);
            //Graphics graphic = Graphics.FromImage(bitmap);
            //MemoryStream memoryStream = new MemoryStream();
            ////Image image = new Image();
            //graphic.Clear(Color.White);
            //string item = base.Request.QueryString["patient_id"];
            //string str = base.Request.QueryString["cat_id"];
            //string str1 = string.Concat(item, "-", str);
            //if ((item == null ? false : str != null))
            //{
            //    BarcodeSettings barcodeSetting = new BarcodeSettings()
            //    {
            //        Data = str1,
            //        Type = BarCodeType.Code39,
            //        Unit = GraphicsUnit.Millimeter,
            //        X = 1.1f
            //    };
            //    Image image1 = (new BarCodeGenerator(barcodeSetting)).GenerateImage();
            //    int width = (1240 - image1.Width) / 2;
            //    graphic.DrawImage(image1, width, 100);
            //    string str2 = "No Patient Found";
            //    string str3 = "No Patient Found";
                
            //    Font font = new Font("Courier New", 12f);
            //    SolidBrush solidBrush = new SolidBrush(Color.Black);
            //    PointF pointF = new PointF(300f, 300f);
            //    graphic.DrawString(string.Concat("Patient ID : ", item), font, solidBrush, pointF);
            //    pointF = new PointF(300f, 320f);
            //    graphic.DrawString(string.Concat("Patient Name : ", str2), font, solidBrush, pointF);
            //    pointF = new PointF(300f, 340f);
            //    graphic.DrawString(string.Concat("Category : ", str3), font, solidBrush, pointF);
            //    bitmap.Save(memoryStream, ImageFormat.Jpeg);
            //    string base64String = Convert.ToBase64String(memoryStream.ToArray());
            //    image.ImageUrl = string.Concat("data:image/jpg;base64,", base64String);
            //    image.CssClass = "barcodeSep";
            //    this.pPage.Controls.Add(image);
            //    bitmap.Dispose();
            //    graphic.Dispose();
            //}



            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult Index(LoginViewModel model)
        {

            try
            {
                if (DateTime.Now >= Convert.ToDateTime("11/04/2020"))
                //{
                    //ModelState.AddModelError(string.Empty, "Please contact support.");
                    //return View(model);
                //}

                if (ModelState.IsValid)
                {
                    using (var client = new UserRepository())
                    {
                        var response = client.Login(model.UserName, model.Password);

                        if (response.username != null)
                        {
                            CommonService.CreateCookie(response, false);

                            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
                            ViewBag.UserFirstName = loginUserViewModel.name;

                            //if (loginUserViewModel.IsAdmin == true)
                            //{
                                return RedirectToAction("selectcompany", "admin");
                            //}
                            //else
                            //{
                            //    return RedirectToAction("index", "admin");
                            //}
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Invalid email or password");
                        }
                    }
                }
                return View(model);
            }
            catch (Exception ex)
            {
                CommonService.SendErrorToText(ex);
                return View(model);
            }
        }

        private CoreResponseModel Authenticate(user model)
        {
            using (var client = new UserRepository()) {
                usersinfo user = client.Login(model.username, model.password);
                if (user.username != null) {

                    string authenticationToken = JwtManager.GenerateToken(Convert.ToString(user.username));
                    return CoreResponseServices.ReturnLoginSuccess(authenticationToken, user);
                } else {
                    return CoreResponseServices.ReturnNotFoud(CoreMessageModel.COMMON_USER_NOT_FOUND);
                }
            }
        }
    }
}