using System;
using System.Net;

namespace CoreLibrary.Model
{
    public class CoreResponseModel : IDisposable
    {
        public HttpStatusCode StatusCode { get; set; }

        public bool Status { get; set; }
        public string Api { get; set; }
        public string Message { get; set; }
        public string AccessToken { get; set; }
        public bool AccessTokenStatus { get; set; }
        public string ErrorLogId { get; set; }
        public dynamic Data { get; set; }
        public int UserId { get; set; }
        
        public void Dispose()
        {

        }
    }
}