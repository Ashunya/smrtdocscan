var dTableUser;
var dTableCategory;


var jsDataTableUser = '#tblUser';
var jsDataTableCategory = '#tblCategory';



$(document).ready(function () {
    dTableUser = $(jsDataTableUser).DataTable({
        "sPaginationType": "full_numbers",
        "pageLength": 100,
        "bProcessing": true,
        "sAjaxSource": "/Admin/AddUserBindGrid",
        oLanguage: { sProcessing: showLoader() },
        "fnDrawCallback": function (oSettings) {
            setTimeout(
                function () {
                    hideLoader();
                }, 1000);
        },
        "fnServerParams": function (aoData) {
            aoData.push({ "name": "companyId", "value": getParameterByName('id') });
        },
        aoColumns: [
            { "data": "username", "sClass": "text-center" },
            { "data": "name", "sClass": "text-center" },
            { "data": "password", "sClass": "text-center" },/*Add Password column to show user password*/
        ]
    });
    /*Add code display User data in Edit mode on 7 Nov 2019*/
    $('#tblUser tbody').on('click', 'tr', function () {
        debugger;
        var username = dTableUser.row(this).data().username;     
        window.location = "/admin/adduser?username=" + username+"&id=" + getParameterByName("id");   
    });

/*END*/
    dTableCategory = $(jsDataTableCategory).DataTable({
        "sPaginationType": "full_numbers",
        "pageLength": 100,
        "bProcessing": true,
        "sAjaxSource": "/Admin/AddUserCategoryBindGrid",
        oLanguage: { sProcessing: showLoader() },
        "fnDrawCallback": function (oSettings) {
            setTimeout(
                function () {
                    hideLoader();
                }, 1000);
        },
        "fnServerParams": function (aoData) {
            aoData.push({ "name": "companyId", "value": getParameterByName('id') });
        },
        aoColumns: [
            { "data": "cat_name", "sClass": "text-center" },
            {
                "bSearchable": false,
                "bSortable": false,
                "sClass": "text-center",
                "sWidth": "10%",
                "mRender": function (data, type, full) {
                        var str = '<div class="">Restrict</div>';
                        return str;
                }
            },
        ]
    });

});


function onSuccess(data) {
    if (data.StatusCode == 300) {
        showToasterMsg(data.Message, 2);
    }
    else {
        showToasterMsg(data.Message, 1);
        setTimeout(
            function () {
                window.location = '/admin/adduser?id=' + getParameterByName('id');
            }, 2000);
    }
}

function onFailure(data) {
   
}
$(function () {
    $('#formUser').submit(function (e) {
        if ($(this).valid()) {
            var form = e.target;
            if (form.dataset.ajax) {
                e.preventDefault();
                e.stopImmediatePropagation();
                var xhr = new XMLHttpRequest();
                xhr.open(form.method, form.action);
                xhr.onreadystatechange = function () {
                    if (xhr.readyState === 4 && xhr.status === 200) {
                        if (form.dataset.ajaxUpdate) {
                            var updateTarget = document.querySelector(form.dataset.ajaxUpdate);
                            if (updateTarget) {
                                updateTarget.innerHTML = xhr.responseText;
                            }
                        }
                        var res = $.parseJSON(xhr.responseText);
                        onSuccess(res);
                    }
                };
                xhr.send(new FormData(form));
            }
        }
    });
});



function onSuccessCategory(data) {
    if (data.StatusCode == 400) {
        showToasterMsg(data.Message, 2);
       
    }
    else {
        showToasterMsg(data.Message, 1);

        setTimeout(
            function () {
                window.location.reload();
            }, 2000);
    }

}

function onFailureCategory(data) {
    showToasterMsg(data.Message, 2);
}
$(function () {
    $('#formCategory').submit(function (e) {
            var form = e.target;
            if (form.dataset.ajax) {
                e.preventDefault();
                e.stopImmediatePropagation();
                var xhr = new XMLHttpRequest();
                xhr.open(form.method, form.action);
                xhr.onreadystatechange = function () {
                    if (xhr.readyState === 4 && xhr.status === 200) {
                        if (form.dataset.ajaxUpdate) {
                            var updateTarget = document.querySelector(form.dataset.ajaxUpdate);
                            if (updateTarget) {
                                updateTarget.innerHTML = xhr.responseText;
                            }
                        }
                        var res = $.parseJSON(xhr.responseText);
                        onSuccessCategory(res);
                    }
                };
                xhr.send(new FormData(form));
            }
    });
});
$(document).ready(function () {
    $('#comp_id').val(getParameterByName('id'));
    $('#cat_comp_id').val(getParameterByName('id'));
});


function bindReportGrid() {

    var fromDate = $('#txtFromDate').val();
    var toDate = $('#txtFromDate').val();


    if (!Date.parse(fromDate)) {
        showToasterMsg('Please enter valid From Date', 2);
        return;
    }

    if (!Date.parse(toDate)) {
        showToasterMsg('Please enter valid To Date', 2);
        return;
    }

    
    var dTableDocReport;
    var jsDataTableDocReport = '#tblDocReport';
    
    dTableDocReport = $(jsDataTableDocReport).DataTable({
        "sPaginationType": "full_numbers",
        "pageLength": 100,
        "bProcessing": true,
        "sAjaxSource": "/Admin/AddUserDocReportBindGrid",
        oLanguage: { sProcessing: showLoader() },
        "fnDrawCallback": function (oSettings) {
            setTimeout(
                function () {
                    hideLoader();
                }, 1000);
        },
        "fnServerParams": function (aoData) {
            aoData.push({ "name": "companyId", "value": getParameterByName('id') });
            aoData.push({ "name": "fromDate", "value": fromDate });
            aoData.push({ "name": "toDate", "value": toDate });
        },
        aoColumns: [
            { "data": "FirstName", "sClass": "text-center"},
            { "data": "DocumentName", "sClass": "text-center"},
            { "data": "NoOfPages", "sClass": "text-center" },
        ]
    });

   

}