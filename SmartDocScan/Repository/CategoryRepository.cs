using SmartDocScan.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SmartDocScan.Repository
{
    public class CategoryRepository : IDisposable
    {
        #region Global Declaration
        SmartDocScanEntities _db = new SmartDocScanEntities();
        #endregion


        public List<category> GetAll()
        {
            var entity = (from item in _db.categories
                          where item.cat_id > 0
                          select item).ToList();

            return entity;
        }


        public category GetCategoryById(int categoryId)
        {
            var entity = (from item in _db.categories
                          where item.cat_id == categoryId
                          select item).FirstOrDefault();

            return entity;
        }

        public List<category> GetAllByCompanyId(int companyId)
        {
            var entity = (from item in _db.categories
                          where item.cat_id > 0 && item.comp_id == companyId
                          select item).ToList();

            return entity;
        }

        public category Create(category model)
        {
            _db.categories.Add(model);
            _db.SaveChanges();

            return model;
        }

        public void Dispose()
        {

        }


    }
}