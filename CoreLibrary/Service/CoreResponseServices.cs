using System;
using System.Net;
using CoreLibrary.Model;

namespace CoreLibrary.Service
{
    public static class CoreResponseServices
    {
        #region Methods
        public static CoreResponseModel ReturnBadRequest(string message)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = false,
                Message = message,
                Data = CoreCommonServices.EmptyJson,
                StatusCode = HttpStatusCode.BadRequest,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true
            };
            return response;
        }

        public static CoreResponseModel ReturnError(Exception ex)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = false,
                Message = CoreMessageModel.COMMON_INTERNAL_SERVER_ERROR,
                Data = ex.Message,
                StatusCode = HttpStatusCode.InternalServerError,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true,
            };
            return response;
        }
        public static CoreResponseModel ReturnError(string message)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = false,
                Message = CoreMessageModel.COMMON_INTERNAL_SERVER_ERROR,
                Data = message,
                StatusCode = HttpStatusCode.InternalServerError,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true,
            };
            return response;
        }

        public static CoreResponseModel ReturnSuccess(string message, dynamic data)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = true,
                Message = message,
                Data = data,
                StatusCode = HttpStatusCode.OK,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true

            };
            return response;
        }

        public static CoreResponseModel ReturnExist(bool status, string message, dynamic data)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = status,
                Message = message,
                Data = data,
                StatusCode = HttpStatusCode.Ambiguous,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true
            };
            return response;
        }

        public static CoreResponseModel ReturnModel(dynamic model)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = true,
                Message = CoreMessageModel.COMMON_MODEL_STRUCTURE,
                Data = model,
                StatusCode = HttpStatusCode.OK,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true
            };
            return response;
        }

        public static CoreResponseModel ReturnNotFoud(string message)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = false,
                Message = message,
                Data = CoreCommonServices.EmptyJson,
                StatusCode = HttpStatusCode.NotFound,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = true
            };
            return response;
        }


        public static CoreResponseModel ReturnLoginSuccess(string accessToken, dynamic data)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = true,
                Message = CoreMessageModel.USER_LOGIN_SUCCESSFULLY,
                Data = data,
                StatusCode = HttpStatusCode.OK,
                Api = CoreCommonServices.GetRoute(),
                AccessToken = accessToken,
                AccessTokenStatus = true
            };
            return response;
        }


        public static CoreResponseModel ReturnAccessTokenStatus(string message)
        {
            CoreResponseModel response = new CoreResponseModel
            {
                Status = false,
                Message = message,
                Data = CoreCommonServices.EmptyJson,
                StatusCode = HttpStatusCode.BadRequest,
                Api = CoreCommonServices.GetRoute(),
                AccessTokenStatus = false
            };
            return response;
        }
        #endregion
    }
}