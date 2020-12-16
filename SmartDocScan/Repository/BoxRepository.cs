using SmartDocScan.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SmartDocScan.Repository
{
    public class BoxRepository : IDisposable
    {
        #region Global Declaration
        SmartDocScanEntities _db = new SmartDocScanEntities();
        #endregion


        public List<box> GetAllByCompanyId(int companyId)
        {
            var entity = (from item in _db.boxes
                          where item.comp_id  == companyId
                          orderby (item.box_id) ascending
                          select item).Take(1000).ToList();

            return entity;
        }

        public box Create(box model)
        {
            _db.boxes.Add(model);
            _db.SaveChanges();

            return model;
        }

        public bool IsBoxIdAlreadyExist(box model)
        {
            return _db.boxes.Count(e => e.box_ext_id == model.box_ext_id) > 0;
        }

        public void Dispose()
        {

        }


    }
}