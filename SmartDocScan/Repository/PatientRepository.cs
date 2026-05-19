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


        public List<patient> GetAllByCompanyId(int companyId, string searchText = null)
        {
            var query = _db.patients.Where(item => item.comp_id == companyId);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                searchText = searchText.Trim();
                var searchTerms = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in searchTerms)
                {
                    var currentTerm = term;
                    int patientId;
                    bool hasPatientId = int.TryParse(currentTerm, out patientId);

                    query = query.Where(item =>
                        (hasPatientId && item.patient_id == patientId)
                        || (item.pext_id != null && item.pext_id.Contains(currentTerm))
                        || (item.first_name != null && item.first_name.Contains(currentTerm))
                        || (item.last_name != null && item.last_name.Contains(currentTerm)));
                }
            }

            return query
                .OrderBy(item => item.patient_id)
                .Take(1000)
                .ToList();
        }

        public patient Create(patient user)
        {
            _db.patients.Add(user);
            _db.SaveChanges();

            return user;
        }

        public bool IsExternalPatientIdExists(int companyId, string externalPatientId, int currentPatientId = 0)
        {
            if (string.IsNullOrWhiteSpace(externalPatientId))
            {
                return false;
            }

            externalPatientId = externalPatientId.Trim();
            return _db.patients.Any(p => p.comp_id == companyId
                && p.pext_id == externalPatientId
                && p.patient_id != currentPatientId);
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
