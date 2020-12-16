using SmartDocScan.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SmartDocScan.Repository
{
    public class CompanyRepository : IDisposable
    {
        #region Global Declaration
        SmartDocScanEntities _db = new SmartDocScanEntities();
        #endregion


        public List<company> GetAll()
        {
            var entity = (from item in _db.companies
                          select item).ToList();

            return entity;
        }
        /*Add for getting company by company id */

        public List<company> GetAll(int Comp_id)
        {
            var entity = (from item in _db.companies
                          where item.comp_id == Comp_id
                          select item).ToList();

            return entity;
        }

        public company Create(company user)
        {
            _db.companies.Add(user);
            _db.SaveChanges();

            return user;
        }

        public company GetByCompanyId(int companyId)
        {
            var entity = (from item in _db.companies
                          where item.comp_id == companyId
                          select item).FirstOrDefault();

            return entity;
        }

        public void Dispose()
        {

        }


    }
}