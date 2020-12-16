using SmartDocScan.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace SmartDocScan.Repository
{
    public class PatientRepository : IDisposable
    {
        #region Global Declaration
        SmartDocScanEntities _db = new SmartDocScanEntities();
        #endregion


        public List<patient> GetAllByCompanyId(int companyId)
        {
            var entity = (from item in _db.patients
                          where item.comp_id  == companyId
                          orderby (item.patient_id) ascending
                          select item).Take(1000).ToList();

            return entity;
        }

        public patient Create(patient user)
        {
            _db.patients.Add(user);
            _db.SaveChanges();

            return user;
        }

        public patient Edit(patient user)
        {
           
                _db.Entry(user).State = EntityState.Modified;               
            _db.SaveChanges();

            return user;
        }

        public List<patient> GetAllByCompanyIdAndBoxId(int companyId, string boxId)
        {
            var entity = (from item in _db.patients
                          where item.comp_id == companyId && item.box.Contains(boxId)
                          orderby (item.patient_id) ascending
                          select item).Take(1000).ToList();

            return entity;
        }

        public patient GetPatientNameById(int id)
        {
            var entity = (from item in _db.patients
                          where item.patient_id == id
                          select item).FirstOrDefault();

            return entity;
        }

        public void Dispose()
        {

        }


    }
}