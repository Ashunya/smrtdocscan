$(document).ready(function () {
   
});


function ResetForm(form) {
    $(form).trigger("reset");
}

function forceLogout() {
    NotifyError("Invalid request, please login again!");

    setTimeout(
        function () {
            window.location = "/Home";
        }, 2000);
}

function showLoader() {
    $('.loading').show();
}

function hideLoader() {
    $('.loading').hide();
}

toastr.options = {
    "closeButton": false,
    "debug": false,
    "newestOnTop": false,
    "progressBar": false,
    "positionClass": "toast-top-right",
    "preventDuplicates": false,
    "onclick": null,
    "showDuration": "300",
    "hideDuration": "1000",
    "timeOut": "5000",
    "extendedTimeOut": "1000",
    "showEasing": "swing",
    "hideEasing": "linear",
    "showMethod": "fadeIn",
    "hideMethod": "fadeOut"
}

function NotifySuccess(message) {
    toastr["success"](message);
}

function NotifyInfo(message) {
    toastr["info"](message);
}

function NotifyWarning(message) {
    toastr["warning"](message)
}

function NotifyError(message) {
    toastr["error"](message)
}
function commonChangeStatus(url, data, jsDataTable) {
    showLoader();
    $.ajax({
        url: url,
        data: data,
        type: 'POST',
        success: function (data) {
            hideLoader();
            commonReloadJsDataTable(jsDataTable, false);
            if (data.Status === true) {
                showToasterMsg(data.Message, 1);
            } else {
                showToasterMsg(data.Message, 4);
            }
        }
    });
}
function commonReloadJsDataTable(jsDataTable, isSPDatatable) {

    if (isSPDatatable == true) {
        jsDataTable.fnDraw();
    }
    else {
        $(jsDataTable).DataTable().ajax.reload(null, false);
    }
}
function commonDelete(text, url, data, jsDataTable, fnCallBack) {
    commonConfirmation(
        confirmationText = text,
        callBackFunction = function () {
            showLoader();
            $.ajax({
                url: url,
                data: data,
                type: 'POST',
                success: function (data) {
                    if (data.url != null) {
                        window.location = data.url;
                    }
                    hideLoader();
                    commonReloadJsDataTable(jsDataTable, false);
                    if (data.Status === true) {
                        showToasterMsg(data.Message, 1);
                    } else {
                        showToasterMsg(data.Message, 4);
                    }

                    if (fnCallBack != null) {
                        fnCallBack();
                    }
                }
            });
        }
    );
}
function commonConfirmation(confirmationText, callBackFunction) {
    swal({
        title: "Are you sure?",
        text: confirmationText,
        type: "warning",
        showCancelButton: true,
        cancelButtonClass: 'btn-default btn-md waves-effect',
        confirmButtonClass: 'btn-success btn-md waves-effect waves-light',
        confirmButtonText: 'Confirm!'
    }, function () {
        if (callBackFunction) {
            callBackFunction();
        }
    });
}
function getActionSection(isEdit, isDelete, id) {
    var str = "";
    if (isEdit > 0) {
        str = '<span class="fa fa-pencil cursor-pointer" title="Edit" onclick="displayPopup(' + id + ')" ></span>';
    }
    if (isDelete > 0) {
        str = str + '<span class="fa fa-trash-o cursor-pointer m-l-10" title="Delete" onclick="deleteItem(' + id + ')" ></span>';
    }
    else {
        str = str + '<span>Deleted</span>';
    }
    return str;
}

function showToasterMsg(msg, msgtype) {
    //success = 1
    //error   = 2
    //info    = 3
    //warning  = 4

    var msgCls = "success";

    if (msgtype === 1) {
        msgCls = "success";
    }

    else if (msgtype === 2) {
        msgCls = "error";
    }

    else if (msgtype === 3) {
        msgCls = "info";
    }

    else if (msgtype === 4) {
        msgCls = "warning";
    }

    Command: toastr[msgCls](msg)

    toastr.options = {
        "closeButton": true,
        "debug": false,
        "newestOnTop": true,
        "progressBar": false,
        "positionClass": "toast-top-right",
        "preventDuplicates": false,
        "onclick": null,
        "showDuration": "300",
        "hideDuration": "1000",
        "timeOut": "5000",
        "extendedTimeOut": "1000",
        "showEasing": "swing",
        "hideEasing": "linear",
        "showMethod": "fadeIn",
        "hideMethod": "fadeOut"
    }

    return false;
}
function getParameterByName(name, url) {
    if (!url)
        url = window.location.href;
    name = name.replace(/[\[\]]/g, "\\$&");
    var regex = new RegExp("[?&]" + name + "(=([^&#]*)|&|#|$)"),
        results = regex.exec(url);
    if (!results) return null;
    if (!results[2]) return '';
    return decodeURIComponent(results[2].replace(/\+/g, " "));
}