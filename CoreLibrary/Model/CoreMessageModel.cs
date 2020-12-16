using System.Configuration;
namespace CoreLibrary.Model
{
    public static class CoreMessageModel
    {

        public static string COMMON_BADREQUEST = "Parameters or data missmatched.";
        public static string COMMON_INTERNAL_SERVER_ERROR = "Internal server error.";
        public static string COMMON_USER_NOT_FOUND = "Invalid username or password.";
        public static string COMMON_NOT_FOUND = "The specified record was not found";
        public static string COMMON_JWT_EXPIRED = "Token is expired.";
        public static string COMMON_JWT_INVALID = "Token is invalid.";
        public static string COMMON_JWT_MISSING = "Token is missing.";
        public static string COMMON_MODEL_STRUCTURE = "Token is missing.";
        public static string COMMON_ENTER_VALID_PASSWORD = "Please enter valid password.";
        public static string COMMON_PASSWORD_CHANGED_SUCCESSFULLY = "Password changed successfully.";
        public static string COMMON_STATUS_UPDATED = "Status updated successfully.";
        public static string COMMON_DELETED = "Deleted successfully.";
        public static string COMMON_INSERTED = "Added successfully.";
        public static string COMMON_UPDATED = "Updated successfully.";



        public static string USER_EMAIL_EXIST = "A user with the specified username is already exists.";
        public static string BOX_ID_EXIST = "Box Id already exists.";
        public static string USER_PHONE_NUMBER_EXIST = "A user with the specified phone no already exists.";
        public static string USER_USERNAME_EXIST = "A user with the specified username already exists.";



        public static string USER_REGISTER_SUCCESSFULLY = "User is registered successfully.";
        public static string USER_PROFILEPICTURE_EXTENSION_RESTRICTION = "Only .jpg,.jpeg and .png files is allowed.";
        public static string USER_PROFILEPICTURE_MISSING = "Profile picture is required.";
        public static string USER_LOGIN_SUCCESSFULLY = "Login successfully.";
        public static string USER_INVALID = "User is invalid.";
        public static string USER_GETUSERBYID_SUCCESS = "User details.";
        public static string USER_UPDATE_SUCCESS = "Profile updated successfully.";
        public static string CHECK_YOUR_EMAIL = "Please check your email.";
        public static string INVALID_PASSWORD = "Invalid password.";
        public static string USER_ROLES_SUCCESS = "All user roles.";
        public static string COUNTRY_SUCCESS = "Country Details.";
        public static string LANGUAGE_SUCCESS = "Language Details.";
        public static string STATE_SUCCESS = "State Details.";


        #region Constant strings
        public static string EMAILFROM = ConfigurationManager.AppSettings["EmailFrom"];
        public static string EMAIL_SITENAME = ConfigurationManager.AppSettings["EmailSiteName"];
        public static string BCCEMAIL = ConfigurationManager.AppSettings["BccEmail"];
        public static string MULTIPLEBCCEMAIL = ConfigurationManager.AppSettings["MultipleBccEmail"];
        public static string SMTP_SERVER = ConfigurationManager.AppSettings["SmtpServer"];
        public static string SMTP_PORT = ConfigurationManager.AppSettings["SmtpPort"];
        public static string SMTP_AUTH_EMAIL = ConfigurationManager.AppSettings["SmtpAuthEmail"];
        public static string SMTP_AUTH_PASSWORD = ConfigurationManager.AppSettings["SmtpAuthPassword"];
        #endregion
    }
}
