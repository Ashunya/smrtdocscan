using CoreLibrary.Model;
using CoreLibrary.Service;
using iTextSharp.text.pdf;
using SmartDocScan.Data;
using SmartDocScan.Models;
using SmartDocScan.Repository;
using SmartDocScan.Service;
using Spire.Barcode;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace SmartDocScan.Controllers
{
    public class AdminController : Controller
    {
        // GET: Admin
        public ActionResult Index()
        {
            return View();
        }


        public ActionResult SelectCompany()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            using (var client = new UserRepository())
            {
                usersinfo modelUser = new usersinfo();
                modelUser = client.GetByUserName(loginUserViewModel.username);
                ViewBag.Permission = modelUser;
            }


            List<company> list = new List<company>();
            /*Get User wise company*/
            var checkAdmin = Convert.ToBoolean(loginUserViewModel.IsAdmin);
            if (!checkAdmin)
            {
                string url = string.Format("/admin/findpatient?id={0}", Convert.ToString(loginUserViewModel.comp_id));
                return Redirect(url);

                //using (var client = new CompanyRepository())
                //{
                //    list = client.GetAll(loginUserViewModel.comp_id);
                //}

            }
            /*END*/
            else
            {

                using (var client = new CompanyRepository())
                {
                    list = client.GetAll();
                }
            }

            return View(list);
        }


        public ActionResult AddCompany()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;
            using (var client = new UserRepository())
            {
                usersinfo modelUser = new usersinfo();
                modelUser = client.GetByUserName(loginUserViewModel.username);
                ViewBag.Permission = modelUser;
            }


            CompanyViewModel model = new CompanyViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult CompanyPost(company model)
        {
            CoreResponseModel response = new CoreResponseModel();
            try
            {
                if (ModelState.IsValid)
                {
                    using (var client = new CompanyRepository())
                    {
                        client.Create(model);
                        return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_INSERTED, model));
                    }
                }
                return Json(CoreResponseServices.ReturnBadRequest(CoreMessageModel.COMMON_BADREQUEST));
            }
            catch (Exception ex)
            {
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult AddUser()
        {

            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;
            ViewBag.comp_id = loginUserViewModel.comp_id;
            UserViewModel model = new UserViewModel();
            /*Edit Existing User on 8 Nov 2019*/
            if (Request.QueryString["username"] != null && Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));
                ViewBag.Permission = GetLoggedInUserPermission();

                usersinfo modelUser = new UserRepository().GetUserDetails(Request.QueryString["username"]);
                model.username = modelUser.username;
                model.name = modelUser.name;
                model.password = modelUser.password;
                model.comp_id = modelUser.comp_id;
                model.upload_doc = Convert.ToBoolean(modelUser.upload_doc);
                model.scan_doc = Convert.ToBoolean(modelUser.scan_doc);
                model.delete_doc = Convert.ToBoolean(modelUser.delete_doc);
                model.delete_manage = Convert.ToBoolean(modelUser.delete_manage);

                model.print_doc = Convert.ToBoolean(modelUser.print_doc);
                model.download_doc = Convert.ToBoolean(modelUser.download_doc);
                model.add_cat = Convert.ToBoolean(modelUser.add_cat);
                model.add_users = Convert.ToBoolean(modelUser.add_users);
                model.add_patients = Convert.ToBoolean(modelUser.add_patients);

                model.box = Convert.ToBoolean(modelUser.box);
                model.report = Convert.ToBoolean(modelUser.report);
                model.su = Convert.ToBoolean(modelUser.su);
                model.disabled = Convert.ToBoolean(modelUser.disabled);
                model.su = Convert.ToBoolean(modelUser.su);
                model.IsAdmin = false;
                ViewBag.username = Request.QueryString["username"];

                return View(model);
            }
            /*END*/
            //else
            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));
                ViewBag.Permission = GetLoggedInUserPermission();
                return View(model);
            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult UserPost(UserViewModel model)
        {
            CoreResponseModel response = new CoreResponseModel();
            try
            {
                if (ModelState.IsValid)
                {
                    if (Request.QueryString["username"] != "null")
                    {
                        using (var client = new UserRepository())
                        {
                            var userdata = client.IsUserExists(model);
                            if (userdata)
                            {
                                usersinfo editUser = new usersinfo
                                {

                                    username = model.username,
                                    name = model.name,
                                    /*capture User password from page*/
                                    // password = "1234",
                                    password = model.password,
                                    comp_id = model.comp_id,
                                    upload_doc = 0,
                                    scan_doc = 0,
                                    delete_doc = 0,
                                    delete_manage = 0,
                                    print_doc = 0,
                                    download_doc = 0,
                                    add_cat = 0,
                                    add_users = 0,
                                    add_patients = 0,
                                    box = 0,
                                    report = 0,
                                    su = 0,
                                    disabled = 0,
                                    IsAdmin = false

                                };
                                if (model.scan_doc) { editUser.scan_doc = 1; }
                                if (model.upload_doc) { editUser.upload_doc = 1; }
                                if (model.delete_doc) { editUser.delete_doc = 1; }
                                if (model.add_cat) { editUser.add_cat = 1; }
                                if (model.add_patients) { editUser.add_patients = 1; }
                                if (model.add_users) { editUser.add_users = 1; }
                                if (model.box) { editUser.box = 1; }
                                if (model.report) { editUser.report = 1; }
                                if (model.su) { editUser.su = 1; }
                                client.UpdateUser(editUser);
                                return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_UPDATED, model));
                            }
                        }
                    }
                    else
                    {
                        using (var client = new UserRepository())
                        {
                            if (IsUsernameAlreadyExists(model))
                            {
                                return Json(CoreResponseServices.ReturnExist(false, CoreMessageModel.USER_EMAIL_EXIST, CoreCommonServices.EmptyJson));
                            }

                            usersinfo user = new usersinfo
                            {
                                username = model.username,
                                name = model.name,
                                /*capture User password from page*/
                                // password = "1234",
                                password = model.password,
                                comp_id = model.comp_id,
                                upload_doc = 0,
                                scan_doc = 0,
                                delete_doc = 0,
                                delete_manage = 0,
                                print_doc = 0,
                                download_doc = 0,
                                add_cat = 0,
                                add_users = 0,
                                add_patients = 0,
                                box = 0,
                                report = 0,
                                su = 0,
                                disabled = 0,
                                IsAdmin = false
                            };


                            if (model.scan_doc) { user.scan_doc = 1; }
                            if (model.upload_doc) { user.upload_doc = 1; }
                            if (model.delete_doc) { user.delete_doc = 1; }
                            if (model.add_cat) { user.add_cat = 1; }
                            if (model.add_patients) { user.add_patients = 1; }
                            if (model.add_users) { user.add_users = 1; }
                            if (model.box) { user.box = 1; }
                            if (model.report) { user.report = 1; }
                            if (model.su) { user.su = 1; }


                            client.Create(user);
                            return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_INSERTED, model));
                        }
                    }
                }
                return Json(CoreResponseServices.ReturnBadRequest(CoreMessageModel.COMMON_BADREQUEST));
            }
            catch (Exception ex)
            {
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult CategoryPost(UserViewModel model)
        {
            CoreResponseModel response = new CoreResponseModel();
            try
            {
                if (model.cat_name == null)
                {
                    return Json(CoreResponseServices.ReturnBadRequest("Category name is required"));
                }

                category category = new category();
                category.cat_name = model.cat_name;
                category.comp_id = model.cat_comp_id;


                using (var client = new CategoryRepository())
                {
                    client.Create(category);
                    return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_INSERTED, model));
                }


            }
            catch (Exception ex)
            {
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }


        public ActionResult AddPatient()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                ViewBag.Permission = GetLoggedInUserPermission();


                PatientViewModel model = new PatientViewModel();
                return View(model);
            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }
        }

        public ActionResult EditPatient(int? id)
        {

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

                LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
                ViewBag.UserFirstName = loginUserViewModel.name;
            ViewBag.Comp_Id = loginUserViewModel.comp_id;
            ViewBag.Permission = GetLoggedInUserPermission();
                SmartDocScanEntities db = new SmartDocScanEntities();
                PatientViewModel model = new PatientViewModel();
            model.comp_id = loginUserViewModel.comp_id;

            var patientModel = db.patients.Where(p => p.patient_id == id).FirstOrDefault();
                if(patientModel!=null)
                {
                    model.box = patientModel.box;
                   // model.comp_id = patientModel.comp_id;
                    model.dob = patientModel.dob;
                    model.first_name = patientModel.first_name;
                    model.gender = patientModel.gender;
                    model.last_name = patientModel.last_name;
                    model.patient_id = patientModel.patient_id;
                    model.pext_id = patientModel.pext_id;
                    model.physician = patientModel.physician;
                    model.ssn = patientModel.ssn;               
                }
          
                return View(model);
            
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult PatientPost(patient model)
        {
            CoreResponseModel response = new CoreResponseModel();
            try
            {
                if (ModelState.IsValid)
                {
                    using (var client = new PatientRepository())
                    {
                        client.Create(model);
                        return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_INSERTED, model));
                    }
                }
                return Json(CoreResponseServices.ReturnBadRequest(CoreMessageModel.COMMON_BADREQUEST));
            }
            catch (Exception ex)
            {
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult EditPatientPost(patient model)
        {
            CoreResponseModel response = new CoreResponseModel();
            try
            {
                if (ModelState.IsValid)
                {
                    using (var client = new PatientRepository())
                    {
                        client.Edit(model);
                        return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_INSERTED, model));
                    }
                }
                return Json(CoreResponseServices.ReturnBadRequest(CoreMessageModel.COMMON_BADREQUEST));
            }
            catch (Exception ex)
            {
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }


        public ActionResult FindPatient()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;

            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));
                ViewBag.Permission = GetLoggedInUserPermission();
                return View();
            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }

        }

        public ActionResult MyAccount()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;

            using (var client = new UserRepository())
            {
                usersinfo modelUser = new usersinfo();
                modelUser = client.GetByUserName(loginUserViewModel.username);
                ViewBag.Permission = modelUser;
            }

            return View();
        }

        public ActionResult Settings()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;

            using (var client = new UserRepository())
            {
                usersinfo modelUser = new usersinfo();
                modelUser = client.GetByUserName(loginUserViewModel.username);
                ViewBag.Permission = modelUser;
            }

            return View();
        }

        public ActionResult ChangePassword()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;

            using (var client = new UserRepository())
            {
                usersinfo modelUser = new usersinfo();
                modelUser = client.GetByUserName(loginUserViewModel.username);
                ViewBag.Permission = modelUser;
            }

            return View();
        }



        [HttpPost]
        public JsonResult ChangePassword(ChangePasswordViewModel model)
        {

            SmartDocScanEntities db = new SmartDocScanEntities();

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();

            string res = "";



            var objpass = (from c in db.usersinfoes
                           where c.username == loginUserViewModel.username
                           select c).FirstOrDefault();
            if (objpass != null)
            {
                model.oldPassword = model.oldPassword.ToLower();

                if (model.oldPassword.ToLower() == objpass.password.ToLower())
                {
                    if (res.ToString().Trim() == "")
                    {
                        objpass.password = model.NewPassword.Trim();
                        db.SaveChanges();
                        res = "success";
                    }
                }
                else
                {
                    res = "invalid_oldpasswd";
                }
            }
            else
            {
                res = "invalid";
            }
            return Json(new { res = res, userId = model.Id }, JsonRequestBehavior.AllowGet);
        }

        #region FindPatient
        [HttpGet]
        public JsonResult FindPatientBindGrid()
        {
            int companyId = Int32.Parse(Request.Params["companyId"]);

            List<patient> list;

            using (var client = new PatientRepository())
            {
                list = client.GetAllByCompanyId(companyId);
            }

            var jsonData = new
            {
                data = list
            };
            return Json(jsonData, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region Add User
        [HttpGet]
        public JsonResult AddUserBindGrid()
        {
            List<usersinfo> list;

            int companyId = Int32.Parse(Request.Params["companyId"]);

            using (var client = new UserRepository())
            {
                list = client.GetAllByCompanyId(companyId);
            }

            var jsonData = new
            {
                data = list
            };
            return Json(jsonData, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult AddUserCategoryBindGrid()
        {
            List<category> list;

            int companyId = Int32.Parse(Request.Params["companyId"]);

            using (var client = new CategoryRepository())
            {
                list = client.GetAllByCompanyId(companyId);
            }

            var jsonData = new
            {
                data = list
            };
            return Json(jsonData, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult AddUserDocReportBindGrid()
        {
            List<DocReportViewModel> list;

            int companyId = Int32.Parse(Request.Params["companyId"]);
            DateTime fromDate = Convert.ToDateTime(Request.Params["fromDate"]);
            DateTime toDate = Convert.ToDateTime(Request.Params["toDate"]);

            using (var client = new DocumentRepository())
            {
                list = client.GetAllByCompanyId(companyId);
            }

            var jsonData = new
            {
                data = list
            };
            return Json(jsonData, JsonRequestBehavior.AllowGet);
        }

        #endregion

        public ActionResult ScanDocument()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));
                ViewBag.CategoryList = GetCategoryListByCompanyId(Convert.ToInt32(Request.QueryString["id"]));
                ViewBag.Permission = GetLoggedInUserPermission();

                if (Convert.ToInt32(Request.QueryString["pid"]) > 0)
                {
                    ViewBag.PatientName = GetPatientNamebyId(Convert.ToInt32(Request.QueryString["pid"]));
                    return View();
                }
                else
                {
                    return RedirectToAction("findpatient", "admin", new { id = Request.QueryString["id"] });
                }

            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }
        }


        public ActionResult UploadDocument()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                ViewBag.Permission = GetLoggedInUserPermission();

                ViewBag.CategoryList = GetCategoryListByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                if (Convert.ToInt32(Request.QueryString["pid"]) > 0)
                {
                    ViewBag.PatientName = GetPatientNamebyId(Convert.ToInt32(Request.QueryString["pid"]));
                    document document = new document();
                    document.comp_id = Convert.ToInt32(Request.QueryString["id"]);
                    document.patient_id = (Convert.ToInt32(Request.QueryString["pid"]));
                    return View(document);
                }
                else
                {
                    return RedirectToAction("findpatient", "admin", new { id = Request.QueryString["id"] });
                }

            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult UploadDocumentPost(HttpPostedFileBase doc_name, document model)
        {
            try
            {
                if (doc_name != null)
                {
                    string _FileName = Path.GetFileName(doc_name.FileName); // Added by sudhir
                    model.doc_name = Path.GetFileName(doc_name.FileName);//doc_name.FileName;
                    string company = "/Store/" + model.comp_id;

                    if (!Directory.Exists(Server.MapPath(company)))
                    {
                        Directory.CreateDirectory(Server.MapPath(company));
                    }


                    string patient = company + "/" + model.patient_id;

                    if (!Directory.Exists(Server.MapPath(patient)))
                    {
                        Directory.CreateDirectory(Server.MapPath(patient));
                    }

                    //model.url = model.comp_id + "/" + model.patient_id + "/" + model.cat_id + "_" + doc_name.FileName;
                    //Change by Sudhir
                    model.url = model.comp_id + "/" + model.patient_id + "/" + model.cat_id + "_" + _FileName;

                    string fullpath = "/store/" + model.url;

                    string originalPath = Path.Combine(Server.MapPath(patient), Server.MapPath(fullpath));
                    doc_name.SaveAs(originalPath);



                    PdfReader pdfReader = new PdfReader(originalPath);
                    int numberOfPages = pdfReader.NumberOfPages;

                    model.num_pages = numberOfPages;
                    model.date = DateTime.UtcNow;

                    using (var client = new DocumentRepository())
                    {
                        client.Create(model);
                    }
                    return Json(CoreResponseServices.ReturnSuccess("Document uploaded successfully.", model));
                }
                else
                {
                    return Json(CoreResponseServices.ReturnNotFoud("Please upload document"));
                }

            }
            catch (Exception ex)
            {
                return Json(CoreResponseServices.ReturnError(ex));
            }

        }

        public ActionResult Barcode()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                ViewBag.Permission = GetLoggedInUserPermission();

                if (Convert.ToInt32(Request.QueryString["pid"]) > 0)
                {
                    ViewBag.PatientName = GetPatientNamebyId(Convert.ToInt32(Request.QueryString["pid"]));
                    ViewBag.CategoryList = GetCategoryListByCompanyId(Convert.ToInt32(Request.QueryString["id"]));
                    return View();
                }
                else
                {
                    return RedirectToAction("findpatient", "admin", new { id = Request.QueryString["id"] });
                }

            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }

        }

        public ActionResult Generatebarcode()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            ViewBag.Permission = GetLoggedInUserPermission();

            if (Convert.ToInt32(Request.QueryString["pid"]) > 0)
            {
                ViewBag.PatientName = GetPatientNamebyId(Convert.ToInt32(Request.QueryString["pid"]));


                Bitmap bitmap = new Bitmap(1240, 600);
                Graphics graphic = Graphics.FromImage(bitmap);
                MemoryStream memoryStream = new MemoryStream();
                //Image image = new Image();
                graphic.Clear(Color.White);
                string item = Request.QueryString["pid"];
                string str = Request.QueryString["cid"];
                string str1 = string.Concat(item, "-", str);
                if ((item == null ? false : str != null))
                {
                    BarcodeSettings barcodeSetting = new BarcodeSettings()
                    {
                        Data = str1,
                        Type = BarCodeType.Code39,
                        Unit = GraphicsUnit.Millimeter,
                        X = 1.1f
                    };
                    Image image1 = (new BarCodeGenerator(barcodeSetting)).GenerateImage();
                    int width = (1240 - image1.Width) / 2;
                    graphic.DrawImage(image1, width, 100);
                    string str2 = ViewBag.PatientName;
                    string str3 = GetCategoryNameByCategoryId(Convert.ToInt32(Request.QueryString["cid"]));

                    Font font = new Font("Courier New", 12f);
                    SolidBrush solidBrush = new SolidBrush(Color.Black);
                    PointF pointF = new PointF(300f, 300f);
                    graphic.DrawString(string.Concat("Patient ID : ", item), font, solidBrush, pointF);
                    pointF = new PointF(300f, 320f);
                    graphic.DrawString(string.Concat("Patient Name : ", str2), font, solidBrush, pointF);
                    pointF = new PointF(300f, 340f);
                    graphic.DrawString(string.Concat("Category : ", str3), font, solidBrush, pointF);
                    bitmap.Save(memoryStream, ImageFormat.Jpeg);
                    string base64String = Convert.ToBase64String(memoryStream.ToArray());
                    ViewBag.base64String = "data:image/jpg;base64," + base64String;
                    //image.ImageUrl = string.Concat("data:image/jpg;base64,", base64String);
                    //image.CssClass = "barcodeSep";
                    //this.pPage.Controls.Add(image);
                    bitmap.Dispose();
                    graphic.Dispose();
                }

                return View();
            }
            else
            {
                return RedirectToAction("findpatient", "admin", new { id = Request.QueryString["id"] });
            }

        }

        public ActionResult ListDocument()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                ViewBag.Permission = GetLoggedInUserPermission();

                if (Convert.ToInt32(Request.QueryString["pid"]) > 0)
                {
                    ViewBag.PatientName = GetPatientNamebyId(Convert.ToInt32(Request.QueryString["pid"]));


                    using (var client = new DocumentRepository())
                    {
                        ViewBag.DocumentList = client.GetCategoryDocumentCounter(Convert.ToInt32(Request.QueryString["id"]), Convert.ToInt32(Request.QueryString["pid"]));

                        ViewBag.Documents = client.GetAllByComapanyIdPatientId(Convert.ToInt32(Request.QueryString["id"]), Convert.ToInt32(Request.QueryString["pid"]));
                    }

                    return View();
                }
                else
                {
                    return RedirectToAction("findpatient", "admin", new { id = Request.QueryString["id"] });
                }

            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }
        }


        public ActionResult ThumbDocument()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }
            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;


            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                ViewBag.Permission = GetLoggedInUserPermission();

                if (Convert.ToInt32(Request.QueryString["pid"]) > 0)
                {
                    ViewBag.PatientName = GetPatientNamebyId(Convert.ToInt32(Request.QueryString["pid"]));
                    ViewBag.PatientId = Convert.ToInt32(Request.QueryString["pid"]);
                    ViewBag.Permission = GetLoggedInUserPermission();

                    using (var client = new DocumentRepository())
                    {
                        ViewBag.DocumentList = client.GetCategoryDocumentCounter(Convert.ToInt32(Request.QueryString["id"]), Convert.ToInt32(Request.QueryString["pid"]));

                        ViewBag.Documents = client.GetAllByComapanyIdPatientId(Convert.ToInt32(Request.QueryString["id"]), Convert.ToInt32(Request.QueryString["pid"]));
                    }

                    return View();
                }
                else
                {
                    return RedirectToAction("findpatient", "admin", new { id = Request.QueryString["id"] });
                }

            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }
        }


        [HttpGet]
        public JsonResult PatientBindGrid()
        {
            int companyId = Int32.Parse(Request.Params["companyId"]);
            string boxId = Request.Params["boxId"];

            List<patient> list;

            using (var client = new PatientRepository())
            {
                list = client.GetAllByCompanyIdAndBoxId(companyId, boxId);
            }

            var jsonData = new
            {
                data = list
            };
            return Json(jsonData, JsonRequestBehavior.AllowGet);
        }

        public ActionResult LogOut()
        {
            Session.Abandon();
            // UserLogOut();
            // clear authentication cookie
            HttpCookie cookie1 = new HttpCookie(FormsAuthentication.FormsCookieName, "") { Expires = DateTime.Now.AddYears(-1) };
            Response.Cookies.Add(cookie1);

            Response.Cookies.Remove(FormsAuthentication.FormsCookieName);
            FormsAuthentication.SignOut();

            // clear session cookie (not necessary for your current problem but i would recommend you do it anyway)
            HttpCookie cookie2 = new HttpCookie("ASP.NET_SessionId", "");
            cookie2.Expires = DateTime.Now.AddYears(-1);
            Response.Cookies.Add(cookie2);

            FormsAuthentication.RedirectToLoginPage();
            return RedirectToAction("index", "home");
        }



        #region Boxes
        public ActionResult AddBox()
        {
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }

            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
            ViewBag.UserFirstName = loginUserViewModel.name;

            if (Convert.ToInt32(Request.QueryString["id"]) > 0)
            {
                ViewBag.Company = GetCompanyByCompanyId(Convert.ToInt32(Request.QueryString["id"]));

                ViewBag.Permission = GetLoggedInUserPermission();


                BoxViewModel model = new BoxViewModel();
                return View(model);
            }
            else
            {
                return RedirectToAction("selectcompany", "admin");
            }
        }


        [HttpGet]
        public JsonResult AddBoxBindGrid()
        {
            int companyId = Int32.Parse(Request.Params["companyId"]);

            List<box> list;

            using (var client = new BoxRepository())
            {
                list = client.GetAllByCompanyId(companyId);
            }

            var jsonData = new
            {
                data = list
            };
            return Json(jsonData, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult BoxPost(BoxViewModel model)
        {
            CoreResponseModel response = new CoreResponseModel();
            try
            {
                if (ModelState.IsValid)
                {
                    using (var client = new BoxRepository())
                    {
                        box box = new box
                        {
                            comp_id = model.comp_id,
                            box_ext_id = model.box_ext_id,
                            box_name = model.box_name,
                            aisle = model.aisle,
                            section = model.section,
                            brow = model.brow,
                            bcolumn = model.bcolumn
                        };

                        if (IsBoxIdAlreadyExist(box))
                        {
                            return Json(CoreResponseServices.ReturnExist(false, CoreMessageModel.BOX_ID_EXIST, CoreCommonServices.EmptyJson));
                        }

                        client.Create(box);
                        return Json(CoreResponseServices.ReturnSuccess(CoreMessageModel.COMMON_INSERTED, model));
                    }
                }
                return Json(CoreResponseServices.ReturnBadRequest(CoreMessageModel.COMMON_BADREQUEST));
            }
            catch (Exception ex)
            {
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }
        #endregion


        #region Private

        public bool IsUsernameAlreadyExists(UserViewModel model)
        {
            using (var client = new UserRepository())
            {
                return client.IsUsernameAlreadyExists(model);
            }
        }
        public bool IsBoxIdAlreadyExist(box model)
        {
            using (var client = new BoxRepository())
            {
                return client.IsBoxIdAlreadyExist(model);
            }
        }
        public string GetCompanyByCompanyId(int companyId)
        {
            using (var client = new CompanyRepository())
            {
                return client.GetByCompanyId(companyId).comp_name;
            }
        }
        public usersinfo GetLoggedInUserPermission()
        {
            LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();

            using (var client = new UserRepository())
            {
                return client.GetByUserName(loginUserViewModel.username);
            }
        }

        public string GetPatientNamebyId(int id)
        {
            using (var client = new PatientRepository())
            {
                patient patient = new patient();

                patient = client.GetPatientNameById(id);

                return patient.last_name + ", " + patient.first_name;
            }
        }

        public List<category> GetCategoryListByCompanyId(int companyId)
        {
            using (var client = new CategoryRepository())
            {
                return client.GetAllByCompanyId(companyId);
            }
        }

        public string GetCategoryNameByCategoryId(int categoryId)
        {
            using (var client = new CategoryRepository())
            {
                return client.GetCategoryById(categoryId).cat_name;
            }
        }
        #endregion

        #region Delete document
        [HttpPost]
        [ValidateInput(false)]
        public JsonResult DeleteDocuments(document[] model)
        {
            try
            {
                foreach (document d in model)
                {
                    string[] doc = d.doc_name.Split('/');
                    string fullpath = "/store/" + d.doc_name;
                    if (System.IO.File.Exists(Server.MapPath(fullpath)))
                    {
                        GC.Collect();
                        System.IO.File.Delete(Server.MapPath(fullpath));
                        using (var client = new DocumentRepository())
                        {
                            client.deleteDocument(Convert.ToInt32(doc[1]), Convert.ToInt32(doc[0]),d.url);
                        }
                       // return Json(CoreResponseServices.ReturnSuccess("Document deleted successfully.", model));
                    }
                    else
                    {
                        int i = 0;
                        using (var client = new DocumentRepository())
                        {
                            i = client.deleteDocument(Convert.ToInt32(doc[1]), Convert.ToInt32(doc[0]), d.url);
                        }
                        if (i >= 1)
                        {
                            continue;
                           
                        }
                        else
                        { return Json(CoreResponseServices.ReturnError("File is in use OR not exists.")); }
                      
                    }

                }
            }
            catch (Exception ex)
            {
                return Json(CoreResponseServices.ReturnError("File is in use OR not exists."));
            }
            return Json(CoreResponseServices.ReturnSuccess("Document deleted successfully.", model));
        }

        /*Upload Scan document  patient wise*/
        public ActionResult UploadScanDocument(string id, string pid, HttpPostedFileBase doc_name, string Cat_id)
        {
            try
            {
                String strImageName;
                document model = new document();
                HttpFileCollection files = System.Web.HttpContext.Current.Request.Files;
                HttpPostedFile uploadfile = files["RemoteFile"];
                if (uploadfile != null)
                {
                    string Category = Cat_id;
                    //   string Category = ConfigurationManager.AppSettings["ScanCategory"].ToString();
                    strImageName = uploadfile.FileName;
                    LoginUserViewModel loginUserViewModel = CommonService.GetLoginUserData();
                    model.doc_name = strImageName;
                    model.comp_id = Convert.ToInt32(id);
                    model.cat_id = Convert.ToInt32(Category);
                    string company = "/Store/" + id;
                    if (!Directory.Exists(Server.MapPath(company)))
                    {
                        Directory.CreateDirectory(Server.MapPath(company));
                    }
                    string patient = company + "/" + pid;

                    if (!Directory.Exists(Server.MapPath(patient)))
                    {
                        Directory.CreateDirectory(Server.MapPath(patient));
                    }
                    model.url = id + "/" + pid + "/" + Category + "_" + strImageName;
                    model.patient_id = Convert.ToInt32(pid);
                    string fullpath = "/store/" + model.url;
                    string originalPath = Path.Combine(Server.MapPath(patient), Server.MapPath(fullpath));
                    uploadfile.SaveAs(originalPath);
                    PdfReader pdfReader = new PdfReader(originalPath);
                    int numberOfPages = pdfReader.NumberOfPages;
                    model.num_pages = numberOfPages;
                    model.date = DateTime.UtcNow;

                    using (var client = new DocumentRepository())
                    {
                        client.Create(model);
                    }
                }

            }
            catch (Exception ex)
            {

            }
            return Content("<script>alert('Document scanned done successfully.')</script>");
        }
        /*END*/

        #endregion

        #region Edit User Details on 7 Nov 2019

        public ActionResult EditUser(string username, string id)
        {
            usersinfo model = new usersinfo();
            if (!Request.IsAuthenticated)
            {
                return RedirectToAction("index", "home");
            }
            if (Request.QueryString["username"] != "" && Request.QueryString["username"] != null)
            {
                using (var client = new UserRepository())
                {
                    model = client.GetUserDetails(Request.QueryString["username"]);

                }

            }
            return RedirectToAction("adduser", "admin", model);
        }
        #endregion
    }
}
