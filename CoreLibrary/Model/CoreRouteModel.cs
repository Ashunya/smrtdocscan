using System;

namespace CoreLibrary.Model
{
   public class CoreRouteModel
    {
        public string Controller { get; set; }
        public string Action { get; set; }
        public string MethodName { get; set; }
        public string Route { get; set; }
        public Object Parameters { get; set; }
        public string RequestURI { get; set; }

        public string Request { get; set; }

    }
}
