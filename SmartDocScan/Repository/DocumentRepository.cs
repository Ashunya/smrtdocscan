using SmartDocScan.Data;
using SmartDocScan.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SmartDocScan.Repository
{
    public class DocumentRepository : IDisposable
    {
        #region Global Declaration
        SmartDocScanEntities _db = new SmartDocScanEntities();
        #endregion


        public List<DocReportViewModel> GetAllByCompanyId(int companyId)
        {
            var entity = (from item in _db.patients
                          join  doc in _db.documents on item.patient_id equals doc.patient_id 
                          where item.comp_id  == companyId
                          select new DocReportViewModel(){
                              FirstName = item.first_name,
                              DocumentName = doc.doc_name,
                              NoOfPages = doc.num_pages
                          }).ToList();

            return entity;
        }

        public document Create(document user)
        {
            _db.documents.Add(user);
            _db.SaveChanges();

            return user;
        }


        public List<CategoryDocumentCounterViewModel> GetCategoryDocumentCounter(int companyId,int patientId)
        {
            var entity = (from T1 in _db.documents
                          join T2 in _db.categories on T1.cat_id equals T2.cat_id
                          where T1.patient_id == patientId && T1.comp_id == companyId
                          group T2 by new { T2.cat_id,T2.cat_name,T1.patient_id} into g
                          select new CategoryDocumentCounterViewModel
                          {
                              cat_name = g.Key.cat_name,
                              cat_id = g.Key.cat_id,
                              counter = g.Count()
                          }).ToList();

            return entity;
        }


        public List<document> GetAllByComapanyIdPatientId(int companyId, int patientId)
        {
            var entity = (from dc in _db.documents 
                          where dc.comp_id == companyId && dc.patient_id == patientId 
                          orderby dc.date descending
                          select dc).ToList();

            return entity;
        }

        public void Dispose()
        {

        }
        /*Add method for delete document*/
        public int deleteDocument(int patient_id, int companyId,string Url)
        {
            var entity = (from dc in _db.documents
                         where dc.comp_id == companyId && dc.patient_id == patient_id && dc.url== Url select dc).FirstOrDefault();
            if (entity != null)
            {  
                _db.documents.Remove(entity);
                _db.SaveChangesAsync();
            }
            return 1;
            
        }
        /*END*/

    }
}