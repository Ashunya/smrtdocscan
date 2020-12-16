using System;
using System.Collections.Generic;
using System.Linq;
using SmartDocScan.Data;
using SmartDocScan.Models;
using SmartDocScan.Service;

namespace SmartDocScan.Repository
{
    public class UserRepository : IDisposable
    {
        #region Global Declaration
        SmartDocScanEntities _db = new SmartDocScanEntities();
        #endregion
        public usersinfo Login(string userName, string password)
        {
            usersinfo user = null;

            try {
                user = (from u in _db.usersinfoes
                        where u.username == userName && u.password == password
                        select u).FirstOrDefault();
                if (user == null) {
                    return new usersinfo();
                }
                return user;
            } catch (Exception e) {
                CommonService.SendErrorToText2(e.InnerException.Message.ToString());
                string s = e.Message.ToString();
                return user;
            }
        }

        public List<usersinfo> GetAllByCompanyId(int companyId)
        {
            var entity = (from item in _db.usersinfoes
                          where item.comp_id == companyId
                          select item).ToList();

            return entity;
        }


        public usersinfo GetByUserName(string username)
        {
            var entity = (from item in _db.usersinfoes
                          where item.username == username
                          select item).FirstOrDefault();

            return entity;
        }

        public usersinfo Create(usersinfo model)
        {
            _db.usersinfoes.Add(model);
            _db.SaveChanges();

            return model;
        }

        public bool IsUsernameAlreadyExists(UserViewModel user)
        {
            return _db.usersinfoes.Count(e => e.username == user.username) > 0;
        }

        public void Dispose()
        {

        }
        /*Add method for Geting user details for edit on 7 nov 2019 */
        public usersinfo GetUserDetails(string username)
        {
            var entity = (from item in _db.usersinfoes
                          where item.username == username
                          select item).FirstOrDefault();

            return entity;
        }

        public bool IsUserExists(UserViewModel user)
        {
            return _db.usersinfoes.Count(e => e.username == user.username) > 0;
        }

        public usersinfo UpdateUser(usersinfo model)
        {
            _db.Entry(model).State = System.Data.Entity.EntityState.Modified;
            _db.SaveChanges();
            return model;
        }
        /*END*/
    }
}